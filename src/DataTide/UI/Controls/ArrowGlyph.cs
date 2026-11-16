namespace DataTide.UI.Controls;

/// <summary>The widget's solid up or down arrow as a small control, in the download or upload colour.</summary>
sealed class ArrowGlyph : Control
{
    public bool Up { get; init; }
    public Theme Theme { get; set; } = Theme.Current;

    public ArrowGlyph()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(14, 30);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Styler.Backdrop(this, Theme.Surface));
        float s = DeviceDpi / 96f, n = 12 * s;
        Art.Arrow(e.Graphics, new RectangleF((Width - n) / 2, (Height - n) / 2, n, n), Up, Up ? Theme.Up : Theme.Down);
    }
}
