using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace DataTide.Limiting;

/// <summary>
/// One-click install of the Windows Packet Filter driver without bundling it: fetch the official MSI from
/// NT Kernel's own GitHub release, check it against the SHA-256 GitHub publishes for that file, then run it
/// with msiexec, which asks for administrator rights. Nothing is installed without that prompt.
/// </summary>
static class DriverInstaller
{
    const string Repo = "wiresock/ndisapi";

    public enum Result { Installed, Declined, Failed }

    public sealed record Package(string Version, string Name, string Url, string Sha256, long Size);

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"DataTide/{Updater.CurrentText}");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    static string ArchSuffix => RuntimeInformation.OSArchitecture == Architecture.Arm64 ? ".ARM64.msi" : ".x64.msi";

    /// <summary>The latest release's MSI for this PC, or null when none (or no published digest) is found.</summary>
    public static async Task<Package?> FindAsync()
    {
        using var doc = JsonDocument.Parse(await Http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest"));
        var r = doc.RootElement;
        string tag = r.GetProperty("tag_name").GetString() ?? "";
        foreach (var a in r.GetProperty("assets").EnumerateArray())
        {
            string name = a.GetProperty("name").GetString() ?? "";
            if (!name.StartsWith("Windows.Packet.Filter.", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(ArchSuffix, StringComparison.OrdinalIgnoreCase)) continue;
            // Without GitHub's digest there is nothing to check the file against: refuse rather than run it unchecked.
            if (!a.TryGetProperty("digest", out var d) || d.GetString() is not { } ds || !ds.StartsWith("sha256:")) return null;
            return new Package(tag.TrimStart('v'), name, a.GetProperty("browser_download_url").GetString()!, ds["sha256:".Length..], a.GetProperty("size").GetInt64());
        }
        return null;
    }

    /// <summary>Downloads, verifies and installs; progress reports a status line.</summary>
    public static async Task<Result> InstallAsync(IProgress<string> progress)
    {
        string? msi = null;
        try
        {
            progress.Report(L.T("DriverFinding"));
            var pkg = await FindAsync();
            if (pkg == null) { progress.Report(L.T("DriverNoPackage")); return Result.Failed; }

            progress.Report(L.F("DriverDownloading", pkg.Version, ByteFormat.Size(pkg.Size)));
            string dir = Path.Combine(Path.GetTempPath(), "DataTide");
            Directory.CreateDirectory(dir);
            msi = Path.Combine(dir, pkg.Name);
            using (var resp = await Http.GetAsync(pkg.Url, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                await using var fs = File.Create(msi);
                await resp.Content.CopyToAsync(fs);
            }
            string actual;
            await using (var fs = File.OpenRead(msi))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(fs)).ToLowerInvariant();
            if (actual != pkg.Sha256.ToLowerInvariant()) { progress.Report(L.T("DriverBadHash")); return Result.Failed; }

            progress.Report(L.T("DriverInstalling"));
            var psi = new ProcessStartInfo("msiexec.exe", $"/i \"{msi}\" /passive /norestart") { UseShellExecute = true, Verb = "runas" };
            Process? p;
            try { p = Process.Start(psi); }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { progress.Report(L.T("DriverDeclined")); return Result.Declined; }
            if (p == null) return Result.Failed;
            await p.WaitForExitAsync();
            // 0 = done, 3010 = done but Windows wants a restart, 1602 = the user cancelled the installer.
            if (p.ExitCode == 1602) { progress.Report(L.T("DriverDeclined")); return Result.Declined; }
            if (p.ExitCode is not (0 or 3010) || !LimiterClient.DriverInstalled)
            {
                progress.Report(L.F("DriverInstallFailed", p.ExitCode));
                return Result.Failed;
            }
            progress.Report(p.ExitCode == 3010 ? L.T("DriverRestart") : L.T("DriverInstalled"));
            return Result.Installed;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            progress.Report(L.T("DriverInstallError"));
            return Result.Failed;
        }
        finally
        {
            try { if (msi != null && File.Exists(msi)) File.Delete(msi); } catch { /* still open by msiexec */ }
        }
    }
}
