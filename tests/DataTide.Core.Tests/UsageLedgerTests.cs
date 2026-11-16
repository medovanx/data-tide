using DataTide.Core.Usage;

namespace DataTide.Core.Tests;

public class UsageLedgerTests
{
    [Fact]
    public void AddsIntoHourBuckets()
    {
        var l = new UsageLedger();
        l.Add(new DateTime(2026, 3, 1, 10, 5, 0), "eth", "Ethernet", 100, 10);
        l.Add(new DateTime(2026, 3, 1, 10, 59, 59), "eth", "Ethernet", 50, 5);
        l.Add(new DateTime(2026, 3, 1, 11, 0, 0), "eth", "Ethernet", 1, 1);
        var h = l.Hours(new DateTime(2026, 3, 1), new DateTime(2026, 3, 2));
        Assert.Equal(2, h.Count);
        Assert.Equal(150, h[0].Received);
        Assert.Equal(15, h[0].Sent);
    }

    [Fact]
    public void DaysKeepInterfacesApart()
    {
        var l = new UsageLedger();
        l.Add(new DateTime(2026, 3, 1, 9, 0, 0), "eth", "Ethernet", 100, 10);
        l.Add(new DateTime(2026, 3, 1, 22, 0, 0), "eth", "Ethernet", 100, 10);
        l.Add(new DateTime(2026, 3, 1, 12, 0, 0), "wifi", "Wi-Fi", 7, 3);
        l.Add(new DateTime(2026, 3, 2, 0, 30, 0), "eth", "Ethernet", 1, 1);
        var d = l.Days(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1));
        Assert.Equal(2, d.Count);
        Assert.Equal(200, d.Single(x => x.InterfaceId == "eth").Received);
        Assert.Equal(10, d.Single(x => x.InterfaceId == "wifi").Total);
    }

    [Fact]
    public void CsvRoundTripWithCommasAndQuotesInNames()
    {
        var l = new UsageLedger();
        l.Add(new DateTime(2026, 3, 1, 9, 0, 0), "{guid-1}", "Ethernet, \"Realtek\"", 123456789012, 42);
        var sw = new StringWriter();
        l.Write(sw);
        var back = new UsageLedger();
        Assert.Equal(1, back.Read(new StringReader(sw.ToString())));
        var h = back.Hours(DateTime.MinValue, DateTime.MaxValue).Single();
        Assert.Equal("Ethernet, \"Realtek\"", h.InterfaceName);
        Assert.Equal(123456789012, h.Received);
        Assert.Equal(new DateTime(2026, 3, 1, 9, 0, 0), h.Hour);
    }

    [Fact]
    public void BadLinesAreSkipped()
    {
        var l = new UsageLedger();
        int n = l.Read(new StringReader(UsageLedger.Header + "\nnot,a,row\n2026-03-01 09:00,x,X,1,2\n2026-13-01 09:00,x,X,1,2\n"));
        Assert.Equal(1, n);
    }

    [Fact]
    public void LatestNameWins()
    {
        var l = new UsageLedger();
        l.Add(new DateTime(2026, 3, 1, 9, 0, 0), "id", "Old name", 1, 0);
        l.Add(new DateTime(2026, 3, 2, 9, 0, 0), "id", "New name", 1, 0);
        Assert.Equal("New name", l.NameOf("id"));
        Assert.All(l.Days(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 2)), d => Assert.Equal("New name", d.InterfaceName));
    }

    [Fact]
    public void PruneDropsOldHours()
    {
        var l = new UsageLedger();
        l.Add(new DateTime(2025, 1, 1, 9, 0, 0), "a", "A", 1, 0);
        l.Add(new DateTime(2026, 1, 1, 9, 0, 0), "a", "A", 1, 0);
        l.Prune(new DateTime(2025, 6, 1));
        Assert.Single(l.Hours(DateTime.MinValue, DateTime.MaxValue));
    }

    [Fact]
    public void RemoveDropsOnlyThoseInterfaces()
    {
        var l = new UsageLedger();
        l.Add(new DateTime(2026, 3, 1, 9, 0, 0), "eth", "Ethernet", 10, 1);
        l.Add(new DateTime(2026, 3, 1, 9, 0, 0), "eth-wfp", "Ethernet-WFP 802.3 MAC Layer", 10, 1);
        l.Add(new DateTime(2026, 3, 1, 10, 0, 0), "eth-wfp", "Ethernet-WFP 802.3 MAC Layer", 10, 1);
        Assert.Equal(2, l.Remove(["eth-wfp"]));
        Assert.Equal("eth", Assert.Single(l.Interfaces()).id);
    }
}
