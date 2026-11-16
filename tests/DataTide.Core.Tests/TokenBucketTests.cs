using DataTide.Core.Limiting;

namespace DataTide.Core.Tests;

public class TokenBucketTests
{
    [Fact]
    public void StartsFullThenRefillsAtRate()
    {
        var b = new TokenBucket(100_000, 0);  // burst 25,000
        Assert.True(b.TryTake(25_000, 0));
        Assert.False(b.TryTake(1000, 0));
        Assert.True(b.TryTake(1000, 0.011));   // 1,100 refilled
        Assert.Equal(0.1, b.WaitFor(10_000, 0.011), 2);
    }

    [Fact]
    public void BurstNeverBelowTwoFrames()
    {
        var b = new TokenBucket(100, 0);
        Assert.True(b.TryTake(1514, 0));
    }
}
