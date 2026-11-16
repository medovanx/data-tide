using System.Diagnostics;
using System.Net;
using DataTide.Core.SpeedTest;

namespace DataTide.SpeedTest;

/// <summary>
/// Measures ping, download and upload against Cloudflare's public speed-test endpoints (the ones speed.cloudflare.com
/// uses; no account or key). Several parallel streams fill the line the way browser speed tests do. Only test data is
/// sent: random-looking bytes up, nothing about the user or this PC.
/// </summary>
static class SpeedTester
{
    const string Host = "https://speed.cloudflare.com";
    const int DownStreams = 6, UpStreams = 4;
    static readonly TimeSpan DownTime = TimeSpan.FromSeconds(10), UpTime = TimeSpan.FromSeconds(8);

    static HttpClient NewClient() => new(new SocketsHttpHandler
    {
        MaxConnectionsPerServer = DownStreams + 2,
        AutomaticDecompression = DecompressionMethods.None,
        PooledConnectionLifetime = TimeSpan.FromMinutes(1),
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "DataTide" } },
    };

    public static async Task<SpeedTestResult> RunAsync(IProgress<SpeedTestProgress> progress, CancellationToken token)
    {
        using var http = NewClient();
        var (ping, jitter) = await PingAsync(http, progress, token);
        double down = await MeasureAsync(DownStreams, DownTime, SpeedTestPhase.Download, ping, progress, token,
            (count, t) => DownloadLoop(http, count, t));
        double up = await MeasureAsync(UpStreams, UpTime, SpeedTestPhase.Upload, ping, progress, token,
            (count, t) => UploadLoop(http, count, t));
        var result = new SpeedTestResult(DateTime.Now, ping, jitter, down, up);
        progress.Report(new SpeedTestProgress(SpeedTestPhase.Done, 0, ping, 1));
        return result;
    }

    static async Task<(double ping, double jitter)> PingAsync(HttpClient http, IProgress<SpeedTestProgress> progress, CancellationToken token)
    {
        // The first request opens the connection (DNS, TCP, TLS); only the round trips after it count.
        using (await http.GetAsync($"{Host}/__down?bytes=0", HttpCompletionOption.ResponseHeadersRead, token)) { }
        var trips = new List<double>();
        for (int i = 0; i < 10; i++)
        {
            var sw = Stopwatch.StartNew();
            using (var r = await http.GetAsync($"{Host}/__down?bytes=0", HttpCompletionOption.ResponseHeadersRead, token)) r.EnsureSuccessStatusCode();
            trips.Add(sw.Elapsed.TotalMilliseconds);
            var (p, _) = SpeedMath.Latency(trips);
            progress.Report(new SpeedTestProgress(SpeedTestPhase.Ping, 0, p, (i + 1) / 10.0));
        }
        return SpeedMath.Latency(trips);
    }

    /// <summary>Runs <paramref name="streams"/> loops that add to one counter, sampling it every 250 ms until time runs out.</summary>
    static async Task<double> MeasureAsync(int streams, TimeSpan duration, SpeedTestPhase phase, double ping,
        IProgress<SpeedTestProgress> progress, CancellationToken token, Func<Action<int>, CancellationToken, Task> loop)
    {
        long total = 0;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var tasks = Enumerable.Range(0, streams).Select(_ => Task.Run(() => loop(n => Interlocked.Add(ref total, n), stop.Token))).ToList();
        var samples = new List<SpeedSample>();
        var sw = Stopwatch.StartNew();
        long last = 0;
        double lastT = 0;
        while (sw.Elapsed < duration)
        {
            await Task.Delay(250, token);
            double now = sw.Elapsed.TotalSeconds;
            long cur = Interlocked.Read(ref total);
            samples.Add(new SpeedSample(now - lastT, cur - last));
            last = cur;
            lastT = now;
            progress.Report(new SpeedTestProgress(phase, SpeedMath.BitsPerSecond(samples, 0.75), ping, Math.Min(1, now / duration.TotalSeconds)));
            if (tasks.All(t => t.IsCompleted)) break; // every stream failed: stop early
        }
        stop.Cancel();
        try { await Task.WhenAll(tasks); } catch { /* cancelled streams end with exceptions */ }
        token.ThrowIfCancellationRequested();
        if (Interlocked.Read(ref total) == 0)
            throw new HttpRequestException(L.T(phase == SpeedTestPhase.Download ? "SpeedDownFailed" : "SpeedUpFailed"));
        return SpeedMath.BitsPerSecond(samples);
    }

    static async Task DownloadLoop(HttpClient http, Action<int> got, CancellationToken token)
    {
        var buffer = new byte[64 * 1024];
        while (!token.IsCancellationRequested)
        {
            using var r = await http.GetAsync($"{Host}/__down?bytes=50000000", HttpCompletionOption.ResponseHeadersRead, token);
            r.EnsureSuccessStatusCode();
            await using var s = await r.Content.ReadAsStreamAsync(token);
            int n;
            while ((n = await s.ReadAsync(buffer, token)) > 0) got(n);
        }
    }

    static async Task UploadLoop(HttpClient http, Action<int> sent, CancellationToken token)
    {
        var chunk = new byte[64 * 1024];
        Random.Shared.NextBytes(chunk); // incompressible, so nothing on the way can shrink it
        while (!token.IsCancellationRequested)
        {
            using var content = new CountingContent(chunk, 160, sent); // 10 MB per request
            using var r = await http.PostAsync($"{Host}/__up", content, token);
        }
    }
}
