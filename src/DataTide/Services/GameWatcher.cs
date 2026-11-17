using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DataTide.Services;

/// <summary>
/// Notices when a game from the game mode list starts or quits, every few seconds. A process counts when its exe
/// name matches and its full path is the game's path, read with the least access Windows has (which elevated games
/// allow too). When Windows refuses even that, the name match alone decides, so a game is never missed.
/// </summary>
sealed class GameWatcher : IDisposable
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 3000 };
    readonly Func<IReadOnlyList<string>> games;
    bool busy;

    /// <summary>The exe path of the game that is running, or null.</summary>
    public string? Running { get; private set; }
    public event Action? Changed;

    public GameWatcher(Func<IReadOnlyList<string>> games)
    {
        this.games = games;
        timer.Tick += async (_, _) => await CheckAsync();
    }

    public void Start() { timer.Start(); _ = CheckAsync(); }

    public async Task CheckAsync()
    {
        if (busy) return;
        busy = true;
        try
        {
            var list = games();
            string? found = list.Count == 0 ? null : await Task.Run(() => Find(list));
            if (!string.Equals(found, Running, StringComparison.OrdinalIgnoreCase))
            {
                Running = found;
                Changed?.Invoke();
            }
        }
        catch (Exception ex) { Log.Error(ex); }
        finally { busy = false; }
    }

    static string? Find(IReadOnlyList<string> games)
    {
        var byName = games.GroupBy(g => Path.GetFileNameWithoutExtension(g), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        string? found = null;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (found != null || !byName.TryGetValue(p.ProcessName, out var candidates)) continue;
                string? path = ImagePath(p.Id);
                found = path == null
                    ? candidates[0] // no access at all: trust the name
                    : candidates.FirstOrDefault(g => SamePath(g, path));
            }
            catch { /* exited meanwhile */ }
            finally { p.Dispose(); }
        }
        return found;
    }

    static bool SamePath(string a, string b)
    {
        try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    }

    const uint ProcessQueryLimitedInformation = 0x1000;

    /// <summary>The full exe path of a process, or null when Windows won't say.</summary>
    static string? ImagePath(int pid)
    {
        var h = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString(0, size) : null;
        }
        finally { CloseHandle(h); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);

    /// <summary>For --game-probe: the game that would count as running now, or null.</summary>
    public static string? FindNow(IReadOnlyList<string> games) => Find(games);

    public void Dispose() => timer.Dispose();
}
