using System.Text.Json;

namespace DataTide.DevTools;

/// <summary>
/// --limiter-probe EXE BYTES_PER_SECOND SECONDS OUT_FILE [others] (run elevated): runs the real limiter engine with one rule
/// and writes its status every second, so limiting can be checked end to end against the installed driver.
/// With "others", EXE is a game instead: every other program shares the limit (game mode) and EXE stays free.
/// </summary>
static class LimiterProbe
{
    public static int Run(string[] a)
    {
        if (a.Length < 5) return 2;
        string exe = a[1], outPath = a[4];
        long rate = long.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture);
        int seconds = int.Parse(a[3], System.Globalization.CultureInfo.InvariantCulture);
        using var w = new StreamWriter(outPath) { AutoFlush = true };
        using var engine = new LimiterEngine();
        try
        {
            w.WriteLine($"start {DateTime.Now:HH:mm:ss} running={engine.Start()} driver={engine.DriverVersion}");
            if (a.Length > 5 && a[5] == "others")
                engine.Apply([], new CatchAllLimit { DownBytesPerSecond = rate, UpBytesPerSecond = rate, Except = [exe] });
            else
                engine.Apply([new LimitRule { ExePath = exe, DownBytesPerSecond = rate, UpBytesPerSecond = rate }]);
            for (int i = 0; i < seconds; i++)
            {
                Thread.Sleep(1000);
                var st = engine.Status();
                var mine = st.Processes.Where(p => string.Equals(p.ExePath, exe, StringComparison.OrdinalIgnoreCase));
                w.WriteLine($"{i + 1}s running={st.Running} error={st.Error ?? "-"} target={JsonSerializer.Serialize(mine)} all={JsonSerializer.Serialize(st.Processes.Where(p => p.DownBytesPerSecond > 20000).Select(p => new { n = Path.GetFileName(p.ExePath), d = (int)p.DownBytesPerSecond }))}");
            }
        }
        catch (Exception ex) { w.WriteLine("EXCEPTION " + ex); }
        finally
        {
            engine.Stop();
            w.WriteLine($"stopped {DateTime.Now:HH:mm:ss}");
        }
        return 0;
    }
}
