namespace DataTide.UI.Taskbar;

/// <summary>
/// Everything the widget may show, already formatted. <see cref="PingMs"/> is null while there is no reply
/// (or ping is off); <see cref="PingQuality"/> colours its dot.
/// </summary>
sealed record WidgetContent(string Up, string Down, string Today, string Month, string? PingMs, string PingDetail, Color PingQuality)
{
    public static readonly WidgetContent Empty = new("0 B/s", "0 B/s", "0 B", "0 B", null, "", Color.Gray);
}
