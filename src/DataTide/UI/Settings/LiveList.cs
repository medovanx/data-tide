using System.Drawing.Drawing2D;

namespace DataTide.UI.Settings;

/// <summary>
/// The busiest programs right now, drawn as a table: icon and name, download and upload speed with the
/// widget's arrows, whether a rule already covers it, and a + to add one. Errors show as a banner on top.
/// </summary>
sealed class LiveList : Control
{
    public sealed record Row(string ExePath, string Name, double Down, double Up, string? Rule);

    public Theme Theme { get; set; } = Theme.Current;
    public event Action<string>? AddRequested;

    IReadOnlyList<Row> rows = [];
    string? banner, empty;
    bool bannerIsError;
    readonly Dictionary<Rectangle, string> addButtons = [];
    Point hoverPt = new(-1, -1);
    Func<double, string> rate = v => ByteFormat.Rate(v);

    public LiveList()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Width = 494;
    }

    float S => DeviceDpi / 96f;
    int Px(float v) => (int)Math.Round(v * S);
    int RowH => Px(34);
    int BannerH => banner == null ? 0 : Px(40);

    /// <summary>Shows rows (or <paramref name="emptyText"/> when there are none) under an optional banner.</summary>
    public void Show(IReadOnlyList<Row> items, string? bannerText, bool isError, string? emptyText, Func<double, string> format)
    {
        rows = items;
        banner = bannerText;
        bannerIsError = isError;
        empty = emptyText;
        rate = format;
        Height = BannerH + (rows.Count == 0 ? Px(34) : Px(26) + rows.Count * RowH);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        hoverPt = e.Location;
        Cursor = addButtons.Keys.Any(r => r.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e) { hoverPt = new(-1, -1); Invalidate(); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        foreach (var (r, path) in addButtons)
            if (r.Contains(e.Location)) { AddRequested?.Invoke(path); return; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Styler.Backdrop(this, Theme.Surface));
        var t = Theme;
        bool rtl = RightToLeft == RightToLeft.Yes;
        var near = rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left;
        var far = rtl ? TextFormatFlags.Left : TextFormatFlags.Right;
        using var f = new Font("Segoe UI", 9.25f);
        using var fHead = new Font("Segoe UI Semibold", 8.25f);
        using var fTag = new Font("Segoe UI Semibold", 7.75f);
        addButtons.Clear();
        int y = 0;

        if (banner != null)
        {
            var br = new Rectangle(0, 0, Width - 1, Px(34));
            Color c = bannerIsError ? Color.FromArgb(232, 72, 85) : t.Accent;
            Art.FillRound(g, br, Px(6), Color.FromArgb(t.Dark ? 40 : 26, c));
            using (var p = Art.RoundRect(br, Px(6))) using (var pen = new Pen(Color.FromArgb(110, c))) g.DrawPath(pen, p);
            TextRenderer.DrawText(g, banner, f, Rectangle.Inflate(br, -Px(10), 0), t.Text, near | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            y = BannerH;
        }

        if (rows.Count == 0)
        {
            TextRenderer.DrawText(g, empty ?? "", f, new Rectangle(Px(4), y, Width - Px(8), Px(34)), t.Dim, near | TextFormatFlags.VerticalCenter);
            return;
        }

        // Columns from the start edge: name (rest), download 96, upload 96, add 30.
        int addW = Px(30), speedW = Px(96), nameW = Width - addW - speedW * 2;
        int X(int fromStart, int w) => rtl ? Width - fromStart - w : fromStart;
        var head = new Rectangle(0, y, Width, Px(22));
        TextRenderer.DrawText(g, L.T("Program"), fHead, new Rectangle(X(Px(8), nameW), y, nameW - Px(8), head.Height), t.Dim, near | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, L.T("Download"), fHead, new Rectangle(X(nameW, speedW), y, speedW - Px(6), head.Height), t.Dim, far | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, L.T("Upload"), fHead, new Rectangle(X(nameW + speedW, speedW), y, speedW - Px(6), head.Height), t.Dim, far | TextFormatFlags.VerticalCenter);
        y += Px(26);

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rr = new Rectangle(0, y, Width - 1, RowH - Px(4));
            if (i % 2 == 0) Art.FillRound(g, rr, Px(6), Color.FromArgb(t.Dark ? 16 : 9, t.Text));

            int icon = Px(16);
            var ir = new Rectangle(X(Px(8), icon), y + (rr.Height - icon) / 2, icon, icon);
            if (ProgramIcons.For(row.ExePath) is { } img)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, ir);
            }
            int textStart = Px(8) + icon + Px(8);
            int tagW = 0;
            if (row.Rule != null)
            {
                Size ts = TextRenderer.MeasureText(row.Rule, fTag);
                tagW = ts.Width + Px(10);
                var tr = new Rectangle(X(nameW - tagW - Px(6), tagW), y + (rr.Height - Px(18)) / 2, tagW, Px(18));
                Color tc = row.Rule == L.T("ModeBlock") ? Color.FromArgb(232, 72, 85) : t.Up;
                Art.FillRound(g, tr, Px(9), Color.FromArgb(t.Dark ? 50 : 36, tc));
                TextRenderer.DrawText(g, row.Rule, fTag, tr, tc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            var nr = new Rectangle(X(textStart, nameW - textStart - tagW - Px(10)), y, nameW - textStart - tagW - Px(10), rr.Height);
            TextRenderer.DrawText(g, row.Name, f, nr, t.Text, near | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            void Speed(int col, bool up, double v)
            {
                var box = new Rectangle(X(nameW + col * speedW, speedW), y, speedW - Px(6), rr.Height);
                int a = Px(9);
                Size vs = TextRenderer.MeasureText(rate(v), f);
                // Value right-aligned (numbers stay left to right), arrow just before it.
                int vx = rtl ? box.Left : box.Right - vs.Width;
                Art.Arrow(g, new RectangleF(rtl ? vx + vs.Width + Px(3) : vx - a - Px(3), y + (rr.Height - a) / 2f, a, a), up, up ? t.Up : t.Down);
                TextRenderer.DrawText(g, rate(v), f, new Rectangle(vx, y, vs.Width + 2, rr.Height), v > 0 ? t.Text : t.Dim, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
            Speed(0, false, row.Down);
            Speed(1, true, row.Up);

            if (row.Rule == null)
            {
                var ab = new Rectangle(X(nameW + speedW * 2 + Px(3), Px(24)), y + (rr.Height - Px(24)) / 2, Px(24), Px(24));
                if (ab.Contains(hoverPt)) Art.FillRound(g, ab, Px(5), Color.FromArgb(t.Dark ? 50 : 30, t.Accent));
                Art.GlyphCentered(g, Art.IconAdd, 11 * S, ab, t.Accent);
                addButtons[ab] = row.ExePath;
            }
            y += RowH;
        }
    }
}
