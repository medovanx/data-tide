using DataTide.Core.Usage;

namespace DataTide.Core.Tests;

public class UsageReportTests
{
    static UsageLedger Sample()
    {
        var l = new UsageLedger();
        l.Add(new DateTime(2026, 3, 1, 9, 0, 0), "eth", "Ethernet", 1000, 100);
        l.Add(new DateTime(2026, 3, 2, 9, 0, 0), "eth", "Ethernet", 2000, 200);
        l.Add(new DateTime(2026, 3, 2, 9, 0, 0), "wifi", "Wi-Fi", 50, 5);
        return l;
    }

    [Fact]
    public void TotalsPerInterfaceAndOverall()
    {
        var r = UsageReport.Build(Sample(), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 3));
        Assert.Equal(3, r.Days.Count);
        Assert.Equal(2, r.Interfaces.Count);
        Assert.Equal("eth", r.Interfaces[0].InterfaceId); // biggest first
        Assert.Equal(3300, r.Interfaces[0].Total);
        Assert.Equal(3355, r.Total);
        Assert.Equal((2000L, 200L), r.For(new DateOnly(2026, 3, 2), "eth"));
        Assert.Equal((0L, 0L), r.For(new DateOnly(2026, 3, 3), "eth"));
        Assert.Equal(2255, r.DayTotal(new DateOnly(2026, 3, 2)));
    }

    [Fact]
    public void FilterToOneInterface()
    {
        var r = UsageReport.Build(Sample(), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 2), "wifi");
        Assert.Single(r.Interfaces);
        Assert.Equal(55, r.Total);
    }

    [Theory]
    [InlineData(ReportRange.Today, "2026-03-15", "2026-03-15")]
    [InlineData(ReportRange.Last7Days, "2026-03-09", "2026-03-15")]
    [InlineData(ReportRange.ThisMonth, "2026-03-01", "2026-03-15")]
    [InlineData(ReportRange.LastMonth, "2026-02-01", "2026-02-28")]
    public void Ranges(ReportRange range, string from, string to)
    {
        var (f, t) = UsageReport.Resolve(range, new DateOnly(2026, 3, 15));
        Assert.Equal(DateOnly.Parse(from), f);
        Assert.Equal(DateOnly.Parse(to), t);
    }
}
