using DataTide.Core.Network;

namespace DataTide.Core.Tests;

public class PingStatsTests
{
    [Fact]
    public void EmptyIsZero()
    {
        var (last, avg, jitter, loss, n) = new PingStats().Summary();
        Assert.Null(last);
        Assert.Equal(0, avg);
        Assert.Equal(0, n);
    }

    [Fact]
    public void AverageJitterAndLoss()
    {
        var p = new PingStats(10);
        foreach (var v in new double?[] { 20, 30, null, 20 }) p.Add(v);
        var (last, avg, jitter, loss, n) = p.Summary();
        Assert.Equal(20, last);
        Assert.Equal(70.0 / 3, avg, 6);
        Assert.Equal(10, jitter, 6); // |30-20| and |20-30| over the replies
        Assert.Equal(25, loss, 6);
        Assert.Equal(4, n);
    }

    [Fact]
    public void WindowDropsOldest()
    {
        var p = new PingStats(3);
        foreach (var v in new double?[] { null, null, 10, 10, 10 }) p.Add(v);
        Assert.Equal(0, p.Summary().lossPercent);
        Assert.Equal(3, p.Samples().Count);
    }

    [Fact]
    public void LastIsNullWhenLatestLost()
    {
        var p = new PingStats();
        p.Add(15);
        p.Add(null);
        Assert.Null(p.Summary().last);
    }
}
