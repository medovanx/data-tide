using DataTide.Core.Usage;

namespace DataTide.Core.Tests;

public class CounterDeltaTests
{
    [Fact]
    public void NormalGrowth() => Assert.Equal(1000, CounterDelta.Compute(5000, 6000, 1));

    [Fact]
    public void ResetCountsWhatMovedSince() => Assert.Equal(300, CounterDelta.Compute(5_000_000, 300, 1));

    [Fact]
    public void ImpossibleJumpIsIgnored() => Assert.Equal(0, CounterDelta.Compute(0, 50_000_000_000_000, 1));

    [Fact]
    public void NegativeReadingIsIgnored() => Assert.Equal(0, CounterDelta.Compute(10, -1, 1));
}
