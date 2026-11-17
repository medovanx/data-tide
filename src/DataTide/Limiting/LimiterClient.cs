using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;

namespace DataTide.Limiting;

/// <summary>
/// The UI's side of the speed limiter. Limiting needs the WinpkFilter driver and admin rights, so it runs in an elevated
/// copy of this exe (<see cref="LimiterHost"/>) that we start on demand and talk to over a private named pipe.
/// The pipe name is random per session, and we only talk to a pipe served by the helper process we started.
/// </summary>
public static class LimiterClient
{
    const int ConnectTimeoutMs = 1000;
    const int ReplyTimeoutMs = 5000;
    const int StartTimeoutMs = 15000;

    static readonly string PipeName = "DataTide.Limiter." + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
    static readonly SemaphoreSlim gate = new(1, 1);
    static Process? helper;
    static NamedPipeClientStream? pipe;
    static StreamReader? reader;
    static StreamWriter? writer;

    /// True when the WinpkFilter driver service is installed (check HKLM\SYSTEM\CurrentControlSet\Services\ndisrd exists; no admin needed).
    public static bool DriverInstalled
    {
        get
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\ndisrd");
                return key != null;
            }
            catch (Exception) { return false; }
        }
    }

    public const string DriverDownloadPage = "https://github.com/wiresock/ndisapi/releases/latest";

    public static bool HelperRunning
    {
        get
        {
            var h = helper;
            try { return h != null && !h.HasExited; }
            catch (Exception) { return false; }
        }
    }

    /// Starts the elevated helper if needed (UAC prompt via ProcessStartInfo Verb "runas", args "--limiter <ourPid> <pipe>"); false if the user declined or it failed.
    public static async Task<bool> StartAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (HelperRunning && await ConnectLockedAsync(ConnectTimeoutMs).ConfigureAwait(false)) return true;
            Drop();
            try
            {
                helper = Process.Start(HelperStartInfo());
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED: the user said no to UAC
            {
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
            if (helper == null) return false;

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < StartTimeoutMs)
            {
                if (!HelperRunning) return false;
                if (await ConnectLockedAsync(500).ConfigureAwait(false)) return true;
                await Task.Delay(200).ConfigureAwait(false);
            }
            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    static ProcessStartInfo HelperStartInfo()
    {
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Unknown exe path.");
        string args = $"--limiter {Environment.ProcessId} {PipeName}";
        // Under "dotnet DataTide.dll" the process is dotnet.exe, so pass the app dll first.
        if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && Assembly.GetEntryAssembly()?.Location is { Length: > 0 } dll)
            args = $"\"{dll}\" {args}";
        return new ProcessStartInfo(exe, args) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
    }

    /// Sends the rules (starts nothing by itself); false if the helper is not reachable.
    public static async Task<bool> ApplyAsync(LimitPlan plan)
    {
        string? line = await RequestAsync(new LimiterRequest { Cmd = "apply", Rules = [.. plan.Rules], Others = plan.Others }).ConfigureAwait(false);
        return line != null && Parse<LimiterReply>(line)?.Ok == true;
    }

    /// <summary>Asks the helper to start per-program monitoring from kernel network events; null on success, else the reason.</summary>
    public static async Task<string?> MonitorAsync()
    {
        string? line = await RequestAsync(new LimiterRequest { Cmd = "monitor" }).ConfigureAwait(false);
        if (line == null) return "The helper is not reachable.";
        var r = Parse<LimiterReply>(line);
        return r?.Ok == true ? null : r?.Error ?? "Monitoring could not start.";
    }

    public static async Task<LimiterStatus?> StatusAsync()
    {
        string? line = await RequestAsync(new LimiterRequest { Cmd = "status" }).ConfigureAwait(false);
        return line == null ? null : Parse<LimiterStatus>(line);
    }

    public static async Task StopAsync()
    {
        await RequestAsync(new LimiterRequest { Cmd = "stop" }).ConfigureAwait(false);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Drop();
            var h = helper;
            helper = null;
            if (h != null)
            {
                using var wait = new CancellationTokenSource(3000);
                try { await h.WaitForExitAsync(wait.Token).ConfigureAwait(false); }
                catch (Exception) { }
                h.Dispose();
            }
        }
        finally
        {
            gate.Release();
        }
    }

    static T? Parse<T>(string line) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(line, LimiterJson.Options); }
        catch (JsonException) { return null; }
    }

    /// <summary>One request, one reply line. Null when the helper is gone or did not answer in time.</summary>
    static async Task<string?> RequestAsync(LimiterRequest request)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!HelperRunning || !await ConnectLockedAsync(ConnectTimeoutMs).ConfigureAwait(false)) return null;
            using var cts = new CancellationTokenSource(ReplyTimeoutMs);
            string json = JsonSerializer.Serialize(request, LimiterJson.Options);
            await writer!.WriteLineAsync(json.AsMemory(), cts.Token).ConfigureAwait(false);
            await writer.FlushAsync(cts.Token).ConfigureAwait(false);
            string? line = await reader!.ReadLineAsync(cts.Token).ConfigureAwait(false);
            if (line == null) Drop();
            return line;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            Drop();
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    static async Task<bool> ConnectLockedAsync(int timeoutMs)
    {
        if (pipe is { IsConnected: true }) return true;
        Drop();
        var h = helper;
        if (h == null) return false;
        var p = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await p.ConnectAsync(timeoutMs).ConfigureAwait(false);
            // Only the helper we launched may answer; another program squatting the name is refused.
            if (!LimiterNative.GetNamedPipeServerProcessId(p.SafePipeHandle, out int serverPid) || serverPid != h.Id)
            {
                p.Dispose();
                return false;
            }
            pipe = p;
            reader = new StreamReader(p, LimiterJson.Utf8, false, 4096, leaveOpen: true);
            writer = new StreamWriter(p, LimiterJson.Utf8, 4096, leaveOpen: true) { NewLine = "\n" };
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            p.Dispose();
            return false;
        }
    }

    static void Drop()
    {
        // Disposing a writer on a broken pipe tries to flush and throws; the pipe is gone either way.
        try { writer?.Dispose(); }
        catch (Exception) { }
        reader?.Dispose();
        pipe?.Dispose();
        reader = null;
        writer = null;
        pipe = null;
    }
}
