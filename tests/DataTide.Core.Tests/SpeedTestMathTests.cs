using DataTide.Core.SpeedTest;

namespace DataTide.Core.Tests;

public class SpeedTestMathTests
{
    [Fact]
    public void WarmupIsLeftOutAndExtremesTrimmed()
    {
        var s = new List<SpeedSample> { new(0.5, 10_000), new(0.5, 10_000), new(0.5, 10_000) }; // slow start
        for (int i = 0; i < 18; i++) s.Add(new(0.5, 1_562_500));                               // 25 Mb/s
        s.Add(new(0.5, 50_000_000));                                                              // one spike
        s.Add(new(0.5, 100));                                                                     // one stall
        Assert.Equal(25_000_000, SpeedMath.BitsPerSecond(s), 0);
    }

    [Fact]
    public void LatencyIsTheMedianAndJitterTheMeanStep()
    {
        var (ping, jitter) = SpeedMath.Latency([20, 22, 30, 21, 23]);
        Assert.Equal(22, ping);
        Assert.Equal((2 + 8 + 9 + 2) / 4.0, jitter);
    }

    [Fact]
    public void OneGigabyteAtTwentyFiveMegabits()
    {
        var t = DownloadTime.Estimate(1, SizeUnit.GB, 25, SpeedUnit.Mbps)!.Value;
        Assert.Equal(1024d * 1024 * 1024 / 3_125_000, t.TotalSeconds, 3); // about 5 min 44 s
        Assert.Equal("5 min 44 s", DownloadTime.Format(t, ("d", "h", "min", "s")));
    }

    [Theory]
    [InlineData(0.4, "< 1 s")]
    [InlineData(45.2, "46 s")]
    [InlineData(3 * 3600 + 5 * 60, "3 h 05 min")]
    [InlineData(2 * 86400 + 4 * 3600 + 59, "2 d 4 h")]
    public void Formats(double seconds, string expected) =>
        Assert.Equal(expected, DownloadTime.Format(TimeSpan.FromSeconds(seconds), ("d", "h", "min", "s")));

    [Fact]
    public void ZeroSpeedHasNoEstimate() => Assert.Null(DownloadTime.Estimate(5, SizeUnit.GB, 0, SpeedUnit.MBps));
}
