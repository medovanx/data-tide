using DataTide.Core.Usage;

namespace DataTide.Core.Tests;

public class InterfaceSamplerTests
{
    static NetInterface If(string id, long rx, long tx) => new(id, "Wi-Fi " + id, "", rx, tx);

    [Fact]
    public void FirstReadingOnlySetsTheBaseline()
    {
        var s = new InterfaceSampler();
        Assert.Empty(s.Sample([If("a", 1_000_000, 500)], new DateTime(2026, 1, 1, 10, 0, 0)));
    }

    [Fact]
    public void DeltasAndSpeedsPerInterface()
    {
        var s = new InterfaceSampler();
        var t = new DateTime(2026, 1, 1, 10, 0, 0);
        s.Sample([If("a", 1000, 100), If("b", 0, 0)], t);
        var r = s.Sample([If("a", 3000, 300), If("b", 500, 50)], t.AddSeconds(2));
        var a = r.Single(x => x.InterfaceId == "a");
        Assert.Equal(2000, a.ReceivedDelta);
        Assert.Equal(200, a.SentDelta);
        Assert.Equal(1000, a.DownBytesPerSecond);
        Assert.Equal(100, a.UpBytesPerSecond);
        Assert.Equal(500, r.Single(x => x.InterfaceId == "b").ReceivedDelta);
    }

    [Fact]
    public void InterfaceThatComesBackStartsFresh()
    {
        var s = new InterfaceSampler();
        var t = new DateTime(2026, 1, 1, 10, 0, 0);
        s.Sample([If("a", 1000, 0)], t);
        s.Sample([], t.AddSeconds(1));                       // unplugged
        Assert.Empty(s.Sample([If("a", 9000, 0)], t.AddSeconds(2))); // back: baseline only
    }
}
