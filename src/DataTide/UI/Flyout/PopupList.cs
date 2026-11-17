using System.Drawing.Drawing2D;

namespace DataTide.UI.Flyout;

/// <summary>
/// A small list for popups in the app's own style: rows with optional notes, a marked current row,
/// red "danger" rows, and separators. Used for the per-program actions in the flyout.
/// </summary>
sealed class PopupList : Control
{
    /// <summary>A row; an empty Id is a separator. A null Note means a single-line row.</summary>
    public sealed record Item(string Id, string Text, string? Note = null, bool Current = false, bool Danger = false, string? Glyph = null)
    {
        public static readonly Item Separator = new("", "");
        public bool IsSeparator => Id.Length == 0;
    }

    readonly IReadOnlyList<Item> items;
    readonly Theme t;
    readonly string? title;
    int hover = -1;
    public event Action<string>? Picked;

    public PopupList(IReadOnlyList<Item> items, Theme theme, string? title = null)
    {
        this.items = items;
        this.title = title;
        t = theme;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Cursor = Cursors.Hand;
    }

    float S => DeviceDpi / 96f;
    int Px(float v) => (int)Math.Round(v * S);
    int TitleH => title == null ? 0 : Px(30);
    int RowH(int i) => items[i].IsSeparator ? Px(9) : items[i].Note != null ? Px(44) : Px(32);

    public Size Preferred(int width) => new(width, Px(6) * 2 + TitleH + Enumerable.Range(0, items.Count).Sum(RowH));

    int RowTop(int i)
    {
        int y = Px(6) + TitleH;
        for (int k = 0; k < i; k++) y += RowH(k);
        return y;
    }

    int IndexAt(Point p)
    {
        for (int i = 0; i < items.Count; i++)
            if (!items[i].IsSeparator && p.Y >= RowTop(i) && p.Y < RowTop(i) + RowH(i)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e) { int i = IndexAt(e.Location); if (i != hover) { hover = i; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }
    protected override void OnMouseClick(MouseEventArgs e) { int i = IndexAt(e.Location); if (i >= 0) Picked?.Invoke(items[i].Id); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(t.Surface);
        bool rtl = RightToLeft == RightToLeft.Yes;
        var near = rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left;
        using var f = new Font("Segoe UI", 9.5f);
        using var fCur = new Font("Segoe UI Semibold", 9.5f);
        using var fNote = new Font("Segoe UI", 8f);
        using var fTitle = new Font("Segoe UI Semibold", 9f);
        var danger = Color.FromArgb(232, 72, 85);

        if (title != null)
            TextRenderer.DrawText(g, title, fTitle, new Rectangle(Px(14), Px(6), Width - Px(28), TitleH), t.Dim, near | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            int top = RowTop(i);
            if (it.IsSeparator)
            {
                using var pen = new Pen(t.Border);
                g.DrawLine(pen, Px(12), top + RowH(i) / 2, Width - Px(12), top + RowH(i) / 2);
                continue;
            }
            var r = new Rectangle(Px(5), top, Width - Px(10), RowH(i));
            if (i == hover)
                Art.FillRound(g, r, Px(6), Color.FromArgb(t.Dark ? 40 : 24, it.Danger ? danger : t.Accent));
            else if (it.Current)
                Art.FillRound(g, r, Px(6), Color.FromArgb(t.Dark ? 22 : 12, t.Text));
            if (it.Current)
                Art.FillRound(g, new Rectangle(rtl ? r.Right - Px(3) : r.Left, r.Top + Px(8), Px(3), r.Height - Px(16)), Px(1.5f), t.Accent);

            var inner = Rectangle.Inflate(r, -Px(12), 0);
            Color fg = it.Danger ? danger : it.Current ? t.Accent : t.Text;
            int textStart = 0;
            if (it.Glyph != null)
            {
                int gw = Px(20);
                Art.GlyphCentered(g, it.Glyph, 13 * S, new RectangleF(rtl ? inner.Right - gw : inner.Left, r.Top, gw, Px(32)), it.Danger ? danger : t.Dim);
                textStart = gw + Px(8);
            }
            var tr = rtl ? new Rectangle(inner.Left, r.Top, inner.Width - textStart, Px(32)) : new Rectangle(inner.Left + textStart, r.Top, inner.Width - textStart, Px(32));
            TextRenderer.DrawText(g, it.Text, it.Current ? fCur : f, tr, fg, near | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (it.Note != null)
                TextRenderer.DrawText(g, it.Note, fNote, new Rectangle(tr.X, r.Top + Px(24), tr.Width, Px(16)), t.Dim, near | TextFormatFlags.EndEllipsis);
        }
    }
}
