namespace DataTide.UI.Report;

/// <summary>Draws a report at the control's width in the app theme; its height follows the content.</summary>
sealed class ReportView : Control
{
    ReportRenderer? renderer;
    public Theme Theme { get; set; } = Theme.Current;
    public ReportChart Chart { get; set; }

    public ReportView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }

    UsageReport? report;
    string filter = "";

    public void Show(UsageReport r, string filterName)
    {
        report = r;
        filter = filterName;
        Relayout();
    }

    public void Relayout()
    {
        if (report == null || Width <= 0) return;
        renderer = new ReportRenderer(report, Theme, RightToLeft == RightToLeft.Yes, DeviceDpi / 96f, Width, filter, Chart);
        if (Height != renderer.TotalHeight) Height = renderer.TotalHeight;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Window);
        renderer?.DrawAll(e.Graphics, screen: true);
    }
}
