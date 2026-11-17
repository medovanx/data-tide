using System.Diagnostics;

namespace DataTide.Services;

/// <summary>
/// Notices when a game from the game mode list starts or quits, every few seconds. Games are matched by exe
/// name, which needs no access to the game's process (elevated and anti-cheat games refuse that).
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
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try { names.Add(p.ProcessName); }
            catch { /* exited meanwhile */ }
            finally { p.Dispose(); }
        }
        return games.FirstOrDefault(g => names.Contains(Path.GetFileNameWithoutExtension(g)));
    }

    public void Dispose() => timer.Dispose();
}
