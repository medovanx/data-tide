using DataTide.Core.Formatting;

namespace DataTide.Core.Tests;

public class RateUnitTests
{
    [Fact]
    public void RoundTripsEachUnit()
    {
        Assert.Equal(512 * 1024, RateUnits.ToBytes(512, RateUnit.KBps));
        Assert.Equal(1536 * 1024, RateUnits.ToBytes(1.5m, RateUnit.MBps));
        Assert.Equal(1_250_000, RateUnits.ToBytes(10, RateUnit.Mbps));
        Assert.Equal(10m, RateUnits.FromBytes(1_250_000, RateUnit.Mbps));
        Assert.Equal(1.5m, RateUnits.FromBytes(1536 * 1024, RateUnit.MBps));
    }

    [Theory]
    [InlineData(256 * 1024, RateUnit.KBps)]
    [InlineData(2048 * 1024, RateUnit.MBps)]
    [InlineData(1536 * 1024, RateUnit.MBps)]
    [InlineData(1100 * 1024, RateUnit.KBps)]
    public void NaturalUnit(long bytes, RateUnit expected) => Assert.Equal(expected, RateUnits.Natural(bytes));
}
