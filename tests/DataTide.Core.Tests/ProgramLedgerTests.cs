using DataTide.Core.Usage;

namespace DataTide.Core.Tests;

public class ProgramLedgerTests
{
    static readonly DateOnly D1 = new(2026, 3, 1), D2 = new(2026, 3, 2), D3 = new(2026, 3, 3);

    [Fact]
    public void SmallSamplesAddUp()
    {
        var l = new ProgramLedger();
        for (int i = 0; i < 10; i++) l.Add(D1, "Chat", @"C:\chat.exe", 0.5, 0.25);
        Assert.Equal(7, l.DayTotal(D1, "Chat")); // 5 + 2.5, kept as fractions until read
    }

    [Fact]
    public void RangeSumsDaysAndMergesNamesIgnoringCase()
    {
        var l = new ProgramLedger();
        l.Add(D1, "Browser", @"C:\a\browser.exe", 100, 10);
        l.Add(D2, "browser", @"C:\b\browser.exe", 50, 5);
        l.Add(D2, "Sync", @"C:\sync.exe", 10, 400);
        l.Add(D3, "Browser", @"C:\b\browser.exe", 999, 999);
        var r = l.Range(D1, D2);
        Assert.Equal(2, r.Count);
        Assert.Equal("Sync", r[0].Name); // 410 beats 165
        var b = r[1];
        Assert.Equal(150, b.Received);
        Assert.Equal(15, b.Sent);
        Assert.Equal(@"C:\b\browser.exe", b.Path); // the latest path
    }

    [Fact]
    public void CsvRoundTripsAndSkipsBadLines()
    {
        var l = new ProgramLedger();
        l.Add(D1, "Mail, Calendar", @"C:\mail.exe", 1234, 56);
        l.Add(D2, "Chat", @"C:\chat.exe", 7, 8);
        var w = new StringWriter();
        l.Write(w);
        Assert.False(l.Dirty);
        var back = new ProgramLedger();
        int n = back.Read(new StringReader(w + "garbage\n2026-13-01,X,,1,1\n"));
        Assert.Equal(2, n);
        var r = back.Range(D1, D2);
        Assert.Contains(r, p => p.Name == "Mail, Calendar" && p.Received == 1234 && p.Sent == 56);
        Assert.Contains(r, p => p.Name == "Chat" && p.Total == 15);
    }

    [Fact]
    public void DailyTotalsPerDayAndName()
    {
        var l = new ProgramLedger();
        l.Add(D1, "Chat", "", 10, 5);
        l.Add(D2, "Chat", "", 1, 1);
        l.Add(D3, "Chat", "", 99, 99);
        var d = l.DailyTotals(D1, D2);
        Assert.Equal(2, d.Count);
        Assert.Equal(15, d[(D1, "Chat")]);
        var rep = UsageReport.Build(new UsageLedger(), D1, D2, programs: l);
        Assert.Equal(2, rep.ProgramOn(D2, "Chat"));
        Assert.Equal(0, rep.ProgramOn(D3, "Chat"));
    }

    [Fact]
    public void PruneDropsOldDays()
    {
        var l = new ProgramLedger();
        l.Add(D1, "Chat", "", 1, 1);
        l.Add(D3, "Chat", "", 2, 2);
        l.Prune(D2);
        Assert.Equal(0, l.DayTotal(D1, "Chat"));
        Assert.Equal(4, l.DayTotal(D3, "Chat"));
    }

    [Fact]
    public void ReportCarriesProgramsForItsRange()
    {
        var usage = new UsageLedger();
        usage.Add(new DateTime(2026, 3, 2, 9, 0, 0), "eth", "Ethernet", 100, 10);
        var progs = new ProgramLedger();
        progs.Add(D1, "Old", "", 1, 1);
        progs.Add(D2, "Chat", "", 60, 6);
        var rep = UsageReport.Build(usage, D2, D3, programs: progs);
        Assert.Single(rep.Programs);
        Assert.Equal("Chat", rep.Programs[0].Name);
        Assert.Empty(UsageReport.Build(usage, D2, D3).Programs);
    }
}
