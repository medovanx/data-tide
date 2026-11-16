namespace DataTide.Reports;

/// <summary>One horizontal slice of a report (header, chart, a table row...) that is never split across pages.</summary>
sealed record ReportBlock(int Height, Action<Graphics, int> Draw, bool IsTableRow = false, Action<Graphics, int>? RepeatHeader = null, int RepeatHeaderHeight = 0);
