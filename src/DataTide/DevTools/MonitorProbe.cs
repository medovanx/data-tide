namespace DataTide.DevTools;

/// <summary>--monitor-probe OUT_FILE SECONDS (run elevated): writes the busiest programs from kernel network events each second.</summary>
static class MonitorProbe
{
    public static int Run(string outPath, int seconds)
    {
        using var w = new StreamWriter(outPath) { AutoFlush = true };
        using var m = new EtwNetworkMonitor();
        w.WriteLine($"start ok={m.Start()} error={m.Error ?? "-"}");
        for (int i = 0; i < seconds; i++)
        {
            Thread.Sleep(1000);
            var top = m.Snapshot().Take(4).Select(p => $"{Path.GetFileName(p.ExePath)} down={p.DownBytesPerSecond / 1024:0}KB/s up={p.UpBytesPerSecond / 1024:0}KB/s");
            w.WriteLine($"{i + 1}s " + string.Join(" | ", top));
        }
        return 0;
    }
}
