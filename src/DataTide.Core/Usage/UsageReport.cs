namespace DataTide.Core.Usage;

/// <summary>
/// A usage report for a date range: one row per day and interface, totals per interface and overall.
/// Interfaces are always kept apart and labelled; nothing is summed across them except the grand total.
/// </summary>
public sealed class UsageReport
{
    public DateOnly From { get; }
    public DateOnly To { get; }
    public IReadOnlyList<DailyUsage> Rows { get; }
    public IReadOnlyList<InterfaceTotal> Interfaces { get; }
    public IReadOnlyList<DateOnly> Days { get; }
    /// <summary>Data per program over the range, busiest first; empty when the Programs list was never on.
    /// Not split by interface: Windows reports a program's traffic without the adapter it went through.</summary>
    public IReadOnlyList<ProgramTotal> Programs { get; private init; } = [];

    public long Received => Interfaces.Sum(i => i.Received);
    public long Sent => Interfaces.Sum(i => i.Sent);
    public long Total => Received + Sent;

    UsageReport(DateOnly from, DateOnly to, IReadOnlyList<DailyUsage> rows)
    {
        From = from;
        To = to;
        Rows = rows;
        Interfaces = rows.GroupBy(r => r.InterfaceId)
            .Select(g => new InterfaceTotal(g.Key, g.Last().InterfaceName, g.Sum(r => r.Received), g.Sum(r => r.Sent)))
            .OrderByDescending(i => i.Total).ToList();
        var days = new List<DateOnly>();
        for (var d = from; d <= to; d = d.AddDays(1)) days.Add(d);
        Days = days;
    }

    /// <summary>Builds a report; <paramref name="interfaceId"/> null means every interface.</summary>
    public static UsageReport Build(UsageLedger ledger, DateOnly from, DateOnly to, string? interfaceId = null, ProgramLedger? programs = null)
    {
        if (to < from) (from, to) = (to, from);
        var rows = ledger.Days(from, to).Where(r => interfaceId == null || r.InterfaceId == interfaceId).ToList();
        return new UsageReport(from, to, rows) { Programs = programs?.Range(from, to) ?? [] };
    }

    /// <summary>Totals for one day on one interface (zero when nothing was logged).</summary>
    public (long rx, long tx) For(DateOnly day, string interfaceId)
    {
        var r = Rows.FirstOrDefault(x => x.Date == day && x.InterfaceId == interfaceId);
        return r == null ? (0, 0) : (r.Received, r.Sent);
    }

    public long DayTotal(DateOnly day) => Rows.Where(r => r.Date == day).Sum(r => r.Total);

    /// <summary>First and last day of a preset range, relative to <paramref name="today"/>.</summary>
    public static (DateOnly from, DateOnly to) Resolve(ReportRange range, DateOnly today, DateOnly? firstLogged = null) => range switch
    {
        ReportRange.Today => (today, today),
        ReportRange.Last7Days => (today.AddDays(-6), today),
        ReportRange.Last30Days => (today.AddDays(-29), today),
        ReportRange.ThisMonth => (new DateOnly(today.Year, today.Month, 1), today),
        ReportRange.LastMonth => (new DateOnly(today.Year, today.Month, 1).AddMonths(-1), new DateOnly(today.Year, today.Month, 1).AddDays(-1)),
        _ => (firstLogged is { } f && f < today ? f : today, today),
    };
}
