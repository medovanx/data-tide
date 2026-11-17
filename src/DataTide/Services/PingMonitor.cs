using System.Net.NetworkInformation;

namespace DataTide.Services;

/// <summary>Pings one host once a second in the background and keeps the last minute in <see cref="Stats"/>.</summary>
sealed class PingMonitor : IDisposable
{
    CancellationTokenSource? cts;
    string? host;

    public PingStats Stats { get; } = new(60);
    public string? Host => host;
    public bool Running => cts != null;

    /// <summary>Starts, stops or retargets the pings; a new host starts a fresh minute.</summary>
    public void Configure(bool enabled, string target)
    {
        target = target.Trim();
        if (!enabled || target.Length == 0) { Stop(); return; }
        if (cts != null && string.Equals(host, target, StringComparison.OrdinalIgnoreCase)) return;
        Stop();
        host = target;
        Stats.Clear();
        cts = new CancellationTokenSource();
        _ = LoopAsync(target, cts.Token);
    }

    public void Stop()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    async Task LoopAsync(string target, CancellationToken ct)
    {
        using var ping = new Ping();
        while (!ct.IsCancellationRequested)
        {
            var started = DateTime.UtcNow;
            double? ms = null;
            try
            {
                var reply = await ping.SendPingAsync(target, TimeSpan.FromSeconds(1), null, null, ct).ConfigureAwait(false);
                if (reply.Status == IPStatus.Success) ms = reply.RoundtripTime;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception) { /* no network or the name doesn't resolve: counts as lost */ }
            if (ct.IsCancellationRequested) return;
            Stats.Add(ms);
            var wait = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - started);
            try { if (wait > TimeSpan.Zero) await Task.Delay(wait, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    public void Dispose() => Stop();
}
