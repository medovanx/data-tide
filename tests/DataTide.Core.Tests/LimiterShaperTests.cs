using DataTide.Core.Limiting;

namespace DataTide.Core.Tests;

public class LimiterShaperTests
{
    const string App = @"C:\Games\Game.exe";
    const int Packet = 1000;

    // 10 000 B/s: the bucket starts with a burst of 2 * 1514 = 3028 bytes, so three 1000-byte packets pass at once,
    // and the queue holds up to half a second (5000 bytes) before dropping.
    static Shaper<int> Limited(long rate = 10_000)
    {
        var s = new Shaper<int>();
        s.SetLimit(App, Direction.Download, rate, 0);
        return s;
    }

    [Fact]
    public void Unlimited_programs_and_unknown_owners_pass()
    {
        var s = Limited();
        Assert.Equal(ShapeDecision.Pass, s.Offer(@"C:\Other.exe", Direction.Download, 1, Packet, 0));
        Assert.Equal(ShapeDecision.Pass, s.Offer(null, Direction.Download, 2, Packet, 0));
        Assert.Equal(ShapeDecision.Pass, s.Offer(App, Direction.Upload, 3, Packet, 0)); // only download is limited
    }

    [Fact]
    public void Passes_within_the_rate_then_queues_then_drops()
    {
        var s = Limited();
        var got = Enumerable.Range(0, 10).Select(i => s.Offer(App, Direction.Download, i, Packet, 0)).ToList();

        Assert.Equal(Enumerable.Repeat(ShapeDecision.Pass, 3), got.Take(3));
        Assert.Equal(Enumerable.Repeat(ShapeDecision.Queued, 5), got.Skip(3).Take(5));
        Assert.Equal(Enumerable.Repeat(ShapeDecision.Dropped, 2), got.Skip(8));
        Assert.Equal((3L * Packet, 2L * Packet), s.Stats(App, Direction.Download));
    }

    [Fact]
    public void Path_case_does_not_matter()
    {
        var s = Limited();
        for (int i = 0; i < 3; i++) s.Offer(App.ToLowerInvariant(), Direction.Download, i, Packet, 0);
        Assert.Equal(ShapeDecision.Queued, s.Offer(App.ToUpperInvariant(), Direction.Download, 3, Packet, 0));
    }

    [Fact]
    public void Queued_packets_drain_in_order_as_tokens_refill()
    {
        var s = Limited();
        for (int i = 0; i < 8; i++) s.Offer(App, Direction.Download, i, Packet, 0);

        Assert.Empty(s.Drain(0));
        // 28 bytes left after the burst; 972 more arrive in 0.0972 s.
        Assert.Equal(0.0972, s.NextDue(0)!.Value, 6);

        Assert.Equal([3], s.Drain(0.1).Select(p => p.packet));
        Assert.Equal([4, 5], s.Drain(0.3).Select(p => p.packet));
        var rest = s.Drain(10);
        Assert.Equal([6, 7], rest.Select(p => p.packet));
        Assert.All(rest, p => Assert.Equal(Direction.Download, p.dir));
        Assert.Null(s.NextDue(10));
    }

    [Fact]
    public void New_packets_do_not_overtake_waiting_ones()
    {
        var s = Limited();
        for (int i = 0; i < 4; i++) s.Offer(App, Direction.Download, i, Packet, 0); // 3 pass, packet 3 waits
        // Enough time for one packet: it must go to the one already waiting, not the newcomer.
        Assert.Equal(ShapeDecision.Queued, s.Offer(App, Direction.Download, 4, 100, 0.1));
        Assert.Equal([3], s.Drain(0.1).Select(p => p.packet));
    }

    [Fact]
    public void Clearing_a_limit_returns_the_waiting_packets()
    {
        var s = Limited();
        for (int i = 0; i < 6; i++) s.Offer(App, Direction.Download, i, Packet, 0);

        var released = s.SetLimit(App, Direction.Download, 0, 0.01);

        Assert.Equal([3, 4, 5], released.Select(p => p.packet));
        Assert.All(released, p => Assert.Equal(Direction.Download, p.dir));
        Assert.False(s.IsLimited(App, Direction.Download));
        Assert.Equal(ShapeDecision.Pass, s.Offer(App, Direction.Download, 6, Packet, 0.01));
    }

    [Fact]
    public void ClearAll_returns_every_waiting_packet_with_its_direction()
    {
        var s = Limited();
        s.SetLimit(App, Direction.Upload, 10_000, 0);
        for (int i = 0; i < 4; i++) s.Offer(App, Direction.Download, i, Packet, 0);
        for (int i = 10; i < 14; i++) s.Offer(App, Direction.Upload, i, Packet, 0);

        var all = s.ClearAll();

        Assert.Contains((3, Direction.Download), all);
        Assert.Contains((13, Direction.Upload), all);
        Assert.Equal(2, all.Count);
        Assert.Empty(s.Limited());
    }

    [Fact]
    public void Changing_the_rate_keeps_the_queue()
    {
        var s = Limited();
        for (int i = 0; i < 5; i++) s.Offer(App, Direction.Download, i, Packet, 0);
        Assert.Empty(s.SetLimit(App, Direction.Download, 1_000_000, 0));
        Assert.Equal([3, 4], s.Drain(0.01).Select(p => p.packet));
    }
}
