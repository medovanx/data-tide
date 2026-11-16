using System.Drawing.Drawing2D;

namespace DataTide.UI.Flyout;

/// <summary>
/// The interface list that drops down from the flyout header, drawn like the app's own dropdowns:
/// rounded, the current choice marked with an accent bar, and the adapter that carries the internet tagged.
/// </summary>
sealed class InterfacePicker : Control
{
    public sealed record Item(string Id, string Name, string? Note, bool Internet);

    readonly IReadOnlyList<Item> items;
    readonly string selectedId;
    readonly Theme t;
    int hover = -1;
    public event Action<string>? Picked;

    public InterfacePicker(IReadOnlyList<Item> items, string selectedId, Theme theme)
    {
        this.items = items;
        this.selectedId = selectedId;
        t = theme;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Cursor = Cursors.Hand;
    }

    float S => DeviceDpi / 96f;
    int Px(float v) => (int)Math.Round(v * S);
    int RowH(int i) => items[i].Note != null ? Px(46) : Px(34);

    public Size Preferred(int width) => new(width, Px(6) * 2 + Enumerable.Range(0, items.Count).Sum(RowH) + (items.Count > 1 ? Px(9) : 0));

    int RowTop(int i)
    {
        int y = Px(6);
        for (int k = 0; k < i; k++) y += RowH(k) + (k == 0 ? Px(9) : 0); // a gap after "Auto"
        return y;
    }

    int IndexAt(Point p)
    {
        for (int i = 0; i < items.Count; i++)
            if (p.Y >= RowTop(i) && p.Y < RowTop(i) + RowH(i)) return i;
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
        using var fSel = new Font("Segoe UI Semibold", 9.5f);
        using var fNote = new Font("Segoe UI", 8f);
        using var fTag = new Font("Segoe UI Semibold", 7.5f);

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var r = new Rectangle(Px(5), RowTop(i), Width - Px(10), RowH(i));
            bool sel = it.Id == selectedId;
            if (i == hover || sel)
                Art.FillRound(g, r, Px(6), i == hover ? Color.FromArgb(t.Dark ? 40 : 24, t.Accent) : Color.FromArgb(t.Dark ? 22 : 12, t.Text));
            if (sel)
                Art.FillRound(g, new Rectangle(rtl ? r.Right - Px(3) : r.Left, r.Top + Px(9), Px(3), r.Height - Px(18)), Px(1.5f), t.Accent);

            var inner = Rectangle.Inflate(r, -Px(12), 0);
            int tagW = 0;
            if (it.Internet)
            {
                string tag = L.T("Internet");
                tagW = TextRenderer.MeasureText(tag, fTag).Width + Px(12);
                var tr = new Rectangle(rtl ? inner.Left : inner.Right - tagW, r.Top + (Px(34) - Px(18)) / 2, tagW, Px(18));
                Art.FillRound(g, tr, Px(9), Color.FromArgb(t.Dark ? 50 : 34, t.Down));
                TextRenderer.DrawText(g, tag, fTag, tr, t.Down, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                tagW += Px(8);
            }
            var nameR = new Rectangle(rtl ? inner.Left + tagW : inner.Left, r.Top, inner.Width - tagW, Px(34));
            TextRenderer.DrawText(g, it.Name, sel ? fSel : f, nameR, sel ? t.Accent : t.Text, near | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (it.Note != null)
                TextRenderer.DrawText(g, it.Note, fNote, new Rectangle(inner.Left, r.Top + Px(25), inner.Width, Px(16)), t.Dim, near | TextFormatFlags.EndEllipsis);
            if (i == 0 && items.Count > 1)
            {
                using var pen = new Pen(t.Border);
                int y = r.Bottom + Px(4);
                g.DrawLine(pen, Px(12), y, Width - Px(12), y);
            }
        }
    }
}
