namespace DataTide.DevTools;

/// <summary>--speedtest-probe OUT_FILE: runs one speed test and writes the progress and result, to check it from a console.</summary>
static class SpeedTestProbe
{
    public static int Run(string outPath)
    {
        using var w = new StreamWriter(outPath) { AutoFlush = true };
        var progress = new SyncProgress(p => w.WriteLine($"{p.Phase} {p.Fraction:P0} ping={p.PingMs:0.0}ms speed={p.BitsPerSecond / 1e6:0.00}Mbps"));
        try
        {
            var r = SpeedTester.RunAsync(progress, CancellationToken.None).GetAwaiter().GetResult();
            w.WriteLine($"RESULT ping={r.PingMs:0.0}ms jitter={r.JitterMs:0.0}ms down={r.DownBitsPerSecond / 1e6:0.00}Mbps up={r.UpBitsPerSecond / 1e6:0.00}Mbps");
            return 0;
        }
        catch (Exception ex) { w.WriteLine("FAILED " + ex); return 1; }
    }

    /// <summary>Reports on the calling thread (a console has no UI thread to post to).</summary>
    sealed class SyncProgress(Action<SpeedTestProgress> report) : IProgress<SpeedTestProgress>
    {
        public void Report(SpeedTestProgress value) => report(value);
    }
}
