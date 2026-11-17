using System.Drawing.Drawing2D;

namespace DataTide.UI.Taskbar;

/// <summary>
/// A small card above the widget after the mouse rests on it: today's and this month's usage on the logged
/// interfaces, split into download and upload. It never takes focus and hides as soon as the mouse leaves.
/// </summary>
sealed class HoverTip : Form
{
    readonly System.Windows.Forms.Timer delay = new() { Interval = 450 };
    DataTideApp? app;
    Rectangle? anchor;
    Func<bool>? suppressed;
    bool dark;

    public HoverTip()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        delay.Tick += (_, _) => { delay.Stop(); ShowNow(); };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x20000;            // CS_DROPSHADOW
            cp.ExStyle |= 0x80 | 0x08000000 | 0x20; // TOOLWINDOW | NOACTIVATE | TRANSPARENT (clicks pass through)
            return cp;
        }
    }

    float S => DeviceDpi / 96f;
    int Px(float v) => (int)Math.Round(v * S);

    /// <param name="suppress">Checked again when the delay ends: the card must never cover or close the open flyout.</param>
    public void ShowSoon(DataTideApp owner, Rectangle? widget, Func<bool>? suppress = null)
    {
        app = owner;
        anchor = widget;
        suppressed = suppress;
        delay.Stop();
        delay.Start();
    }

    public void Cancel()
    {
        delay.Stop();
        Hide();
    }

    /// <summary>Lays the card out off-screen for --shots.</summary>
    public void ShowForShot(DataTideApp owner, bool darkTheme)
    {
        app = owner;
        dark = darkTheme;
        RightToLeft = L.Ar ? RightToLeft.Yes : RightToLeft.No;
        BackColor = new Theme(dark).Surface;
        Size = new Size(Px(250), Px(app.PingSummary() == null ? 92 : 118));
        Location = new Point(-4000, -4000);
        Show();
    }

    void ShowNow()
    {
        if (app == null || anchor is not { } a || suppressed?.Invoke() == true) return;
        dark = Native.SystemDark();
        RightToLeft = L.Ar ? RightToLeft.Yes : RightToLeft.No;
        BackColor = new Theme(dark).Surface;
        Size = new Size(Px(250), Px(app.PingSummary() == null ? 92 : 118));
        var wa = Screen.FromRectangle(a).WorkingArea;
        int x = Math.Clamp(a.Left + a.Width / 2 - Width / 2, wa.Left + Px(6), wa.Right - Width - Px(6));
        Location = new Point(x, wa.Bottom - Height - Px(8));
        Invalidate();
        // SW_SHOWNOACTIVATE: appear without taking focus, so whatever is active stays active.
        Native.ShowWindow(Handle, 4);
        Visible = true;
        Native.RoundCorners(Handle, dark);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (app == null) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var t = new Theme(dark);
        bool rtl = L.Ar;
        var near = rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left;
        using var fLabel = new Font("Segoe UI", 9f);
        using var fTotal = new Font("Segoe UI Semibold", 10f);
        using var fSmall = new Font("Segoe UI", 8.5f);
        int pad = Px(12);
        void Line(int y, string label, (long rx, long tx) v)
        {
            int labelW = Px(84);
            var lr = rtl ? new Rectangle(Width - pad - labelW, y, labelW, Px(36)) : new Rectangle(pad, y, labelW, Px(36));
            TextRenderer.DrawText(g, label, fLabel, new Rectangle(lr.X, lr.Y, lr.Width, Px(18)), t.Dim, near);
            TextRenderer.DrawText(g, ByteFormat.Size(v.rx + v.tx), fTotal, new Rectangle(lr.X, lr.Y + Px(16), lr.Width, Px(20)), t.Text,
                rtl ? TextFormatFlags.Right : TextFormatFlags.Left);
            int x0 = rtl ? pad : pad + labelW, w = Width - pad * 2 - labelW, a = Px(9);
            void Amount(int row, bool up, long bytes, Color c)
            {
                int ry = y + Px(3) + row * Px(16);
                int ax = rtl ? x0 + w - a : x0;
                Art.Arrow(g, new RectangleF(ax, ry + Px(4), a, a), up, c);
                var tr = rtl ? new Rectangle(x0, ry, w - a - Px(6), Px(16)) : new Rectangle(x0 + a + Px(6), ry, w - a - Px(6), Px(16));
                TextRenderer.DrawText(g, ByteFormat.Size(bytes), fSmall, tr, t.Text, rtl ? TextFormatFlags.Right : TextFormatFlags.Left);
            }
            Amount(0, false, v.rx, t.Down);
            Amount(1, true, v.tx, t.Up);
        }
        Line(Px(8), L.T("Today"), app.TodayTotal);
        using (var pen = new Pen(t.Border)) g.DrawLine(pen, pad, Px(46), Width - pad, Px(46));
        Line(Px(50), L.T("ThisMonth"), app.MonthTotal);
        if (app.PingSummary() is var (pingText, quality))
        {
            using (var pen = new Pen(t.Border)) g.DrawLine(pen, pad, Px(88), Width - pad, Px(88));
            int d = Px(7), dx = rtl ? Width - pad - d : pad;
            using (var qb = new SolidBrush(quality)) g.FillEllipse(qb, dx, Px(92) + (Px(20) - d) / 2f, d, d);
            var pr = rtl ? new Rectangle(pad, Px(92), Width - pad * 2 - d - Px(6), Px(20)) : new Rectangle(pad + d + Px(6), Px(92), Width - pad * 2 - d - Px(6), Px(20));
            TextRenderer.DrawText(g, pingText, fSmall, pr, t.Text, near | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
