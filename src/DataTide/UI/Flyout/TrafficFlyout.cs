using System.Drawing.Drawing2D;

namespace DataTide.UI.Flyout;

/// <summary>
/// The popup above the taskbar: live speeds of the shown interface, a one-minute graph,
/// today's usage per interface and shortcuts to the report, limits and settings.
/// </summary>
sealed class TrafficFlyout : Form
{
    const int W = 360, HeaderH = 132, GraphH = 96, RowH = 30, MaxRows = 6;

    readonly DataTideApp app;
    readonly IconButton btnReport = new(), btnSpeed = new(), btnLimits = new(), btnSettings = new();
    /// <summary>Turns speed limits on or off right here, without opening Settings.</summary>
    readonly ToggleSwitch tglLimits = new();
    readonly ToolTip tip = new();
    bool syncingToggle;
    bool dark, activated;
    public DateTime LastHidden { get; private set; }

    public TrafficFlyout(DataTideApp app)
    {
        this.app = app;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        KeyPreview = true;
        Text = L.T("AppName");
        Icon = app.AppIcon;
        btnReport.Glyph = Art.IconChart;
        btnLimits.Glyph = Art.IconLimit;
        btnSettings.Glyph = Art.IconSettings;
        btnSpeed.Glyph = Art.IconSpeed;
        Controls.AddRange([btnReport, btnSpeed, btnLimits, btnSettings, tglLimits]);
        btnSpeed.Click += (_, _) => { Hide(); app.ShowSpeedTest(); };
        tip.SetToolTip(btnSettings, L.T("Settings"));
        tglLimits.CheckedChanged += (_, _) => { if (!syncingToggle) _ = ToggleLimitsAsync(tglLimits.Checked); };
        btnReport.Click += (_, _) => { Hide(); app.ShowReport(); };
        btnLimits.Click += (_, _) => { Hide(); app.ShowSettings(SettingsForm.LimitsPage); };
        btnSettings.Click += (_, _) => { Hide(); app.ShowSettings(); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Hide(); };
        app.Ticked += () => { if (Visible) { if (tglLimits.Enabled) SyncToggle(); Invalidate(); } };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x20000;   // CS_DROPSHADOW
            cp.ExStyle |= 0x80;         // WS_EX_TOOLWINDOW
            return cp;
        }
    }

    float S => DeviceDpi / 96f;
    int Px(float v) => (int)Math.Round(v * S);

    IReadOnlyList<DailyUsage> TodayRows()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        return app.Ledger.Days(today, today)
            .Where(d => app.S.LogVirtual || !app.Monitor.IsVirtual(d.InterfaceId))
            .OrderByDescending(d => d.Total).Take(MaxRows).ToList();
    }

    int ContentHeight(int rows) => HeaderH + 14 + GraphH + 18 + 26 + 34 + Math.Max(1, rows) * RowH + 4 + 28 + 10 + 34 + 16;

    public void ShowNear(Rectangle anchor)
    {
        app.RefreshTotals();
        dark = Native.SystemDark();
        var theme = new Theme(dark);
        RightToLeft = L.Ar ? RightToLeft.Yes : RightToLeft.No;
        BackColor = theme.Window;
        btnReport.Text = L.T("Report");
        btnLimits.Text = L.T("Limits");
        btnSpeed.Text = L.T("SpeedTestShort");
        btnSettings.Text = "";
        foreach (var b in new[] { btnReport, btnSpeed, btnLimits, btnSettings }) { b.Theme = theme; b.RightToLeft = RightToLeft; b.Invalidate(); }
        btnReport.Primary = true;
        tglLimits.Theme = theme;
        SyncToggle();

        var wa = Screen.FromRectangle(anchor).WorkingArea;
        Size = new Size(Px(W), Px(ContentHeight(TodayRows().Count)));
        int x = anchor.Left + anchor.Width / 2 - Width / 2;
        x = Math.Clamp(x, wa.Left + Px(8), wa.Right - Width - Px(8));
        Location = new Point(x, wa.Bottom - Height - Px(10));
        LayoutButtons();

        activated = false;
        Show();
        Native.RoundCorners(Handle, dark);
        Activate();
        Native.SetForegroundWindow(Handle);
    }

    /// <summary>Lays the flyout out off-screen for --shots, without showing or focusing it.</summary>
    public void ShowForShot(bool darkTheme)
    {
        dark = darkTheme;
        var theme = new Theme(dark);
        RightToLeft = L.Ar ? RightToLeft.Yes : RightToLeft.No;
        BackColor = theme.Window;
        btnReport.Text = L.T("Report");
        btnLimits.Text = L.T("Limits");
        btnSpeed.Text = L.T("SpeedTestShort");
        btnSettings.Text = "";
        foreach (var b in new[] { btnReport, btnSpeed, btnLimits, btnSettings }) { b.Theme = theme; b.RightToLeft = RightToLeft; }
        btnReport.Primary = true;
        tglLimits.Theme = theme;
        SyncToggle();
        Location = new Point(-4000, -4000);
        Size = new Size(Px(W), Px(ContentHeight(TodayRows().Count)));
        LayoutButtons();
        ShowInactiveForShot();
    }

    void ShowInactiveForShot()
    {
        // Shown far off-screen without focus, so its children get handles and draw.
        Show();
        Location = new Point(-4000, -4000);
    }

    protected override bool ShowWithoutActivation => app.Headless;

    /// <summary>Top of the speed limits line: just above the buttons.</summary>
    int LimitsLineY => Height - Px(88);

    void SyncToggle()
    {
        syncingToggle = true;
        tglLimits.Checked = app.S.LimitsEnabled && HasRules;
        syncingToggle = false;
    }

    bool HasRules => app.S.Limits.Any(l => l.Enabled && (l.DownBytesPerSecond > 0 || l.UpBytesPerSecond > 0));

    async Task ToggleLimitsAsync(bool on)
    {
        // Nothing to switch on yet: take the user to where programs and the driver are set up.
        if (on && (!HasRules || !LimiterClient.DriverInstalled))
        {
            SyncToggle();
            Hide();
            app.ShowSettings(SettingsForm.LimitsPage);
            return;
        }
        tglLimits.Enabled = false;
        await app.SetLimitsEnabledAsync(on); // turning on shows the admin prompt once per session
        tglLimits.Enabled = true;
        SyncToggle();
        Invalidate();
    }

    void LayoutButtons()
    {
        int pad = Px(16), gap = Px(8), bh = Px(34);
        // Three labelled buttons and a square icon-only Settings button at the far end.
        int sq = bh, bw = (Width - pad * 2 - gap * 3 - sq) / 3, by = Height - pad - bh;
        var wide = new[] { btnReport, btnSpeed, btnLimits };
        for (int i = 0; i < 3; i++)
        {
            int x = pad + i * (bw + gap);
            wide[i].Bounds = new Rectangle(L.Ar ? Width - x - bw : x, by, bw, bh);
        }
        int sx = pad + 3 * (bw + gap);
        btnSettings.Bounds = new Rectangle(L.Ar ? Width - sx - sq : sx, by, sq, bh);
        var ts = new Size(Px(44), Px(22));
        tglLimits.Bounds = new Rectangle(L.Ar ? pad : Width - pad - ts.Width, LimitsLineY + (Px(28) - ts.Height) / 2, ts.Width, ts.Height);
        tglLimits.RightToLeft = L.Ar ? RightToLeft.Yes : RightToLeft.No;
    }

    /// <summary>Keeps the height in step with the number of interfaces that moved data today.</summary>
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        int want = Px(ContentHeight(TodayRows().Count));
        if (Height != want)
        {
            int bottom = Bottom;
            Height = want;
            Top = bottom - Height;
            LayoutButtons();
        }
        base.OnPaintBackground(e);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        activated = true;
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (activated) Hide();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) LastHidden = DateTime.Now;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var t = new Theme(dark);
        bool rtl = L.Ar;
        var near = rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left;
        var far = rtl ? TextFormatFlags.Left | TextFormatFlags.RightToLeft : TextFormatFlags.Right;
        int pad = Px(16);

        // Header: sea gradient with waves, the interface and the two big speeds.
        var head = new Rectangle(0, 0, Width, Px(HeaderH));
        using (var grad = new LinearGradientBrush(head, Art.SeaLight, Art.SeaDark, LinearGradientMode.ForwardDiagonal))
            g.FillRectangle(grad, head);
        Art.Waves(g, head, Px(34), Color.FromArgb(22, 255, 255, 255));

        using var fSmall = new Font("Segoe UI", 9f);
        using var fName = new Font("Segoe UI Semibold", 10.5f);
        using var fBig = new Font("Segoe UI Semibold", 17f);
        using var fLabel = new Font("Segoe UI", 8.5f);
        // Brand on the start side (mark and app name, like Salaty Reminder's card), the interface as a pill on the far side.
        int markS = Px(28);
        var markR = new Rectangle(rtl ? Width - pad - markS : pad, Px(10), markS, markS);
        Art.Mark(g, markR);
        using var fBrand = new Font("Segoe UI Semibold", 11.5f);
        string auto = app.S.InterfaceId == AppSettings.AutoInterface ? $" ({L.T("Auto")})" : "";
        string iface = app.ShownInterfaceName + auto;
        Size ps = TextRenderer.MeasureText(iface, fSmall);
        int pillW = Math.Min(ps.Width + Px(18), Width / 2 - pad);
        var pillR = new Rectangle(rtl ? pad : Width - pad - pillW, markR.Top + (markS - Px(22)) / 2, pillW, Px(22));
        Art.FillRound(g, pillR, Px(11), Color.FromArgb(46, 255, 255, 255));
        TextRenderer.DrawText(g, iface, fSmall, Rectangle.Inflate(pillR, -Px(8), 0), Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (rtl ? TextFormatFlags.RightToLeft : 0));
        int brandW = Width - pad * 2 - markS - Px(8) - pillW - Px(8);
        var brandR = rtl ? new Rectangle(markR.Left - Px(8) - brandW, markR.Top, brandW, markS) : new Rectangle(markR.Right + Px(8), markR.Top, brandW, markS);
        TextRenderer.DrawText(g, L.T("AppName"), fBrand, brandR, Color.White, near | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        int half = (Width - pad * 2) / 2;
        void Speed(int col, bool up, string label, string value, Color c)
        {
            int x0 = pad + (rtl ? (1 - col) : col) * half;
            int a = Px(16);
            int ax = rtl ? x0 + half - a : x0;
            Art.Arrow(g, new RectangleF(ax, Px(62), a, a), up, c);
            var lr = rtl ? new Rectangle(x0, Px(54), half - a - Px(6), Px(18)) : new Rectangle(x0 + a + Px(6), Px(54), half - a - Px(6), Px(18));
            TextRenderer.DrawText(g, label, fLabel, lr, Color.FromArgb(215, 235, 245), near);
            var vr = rtl ? new Rectangle(x0, Px(72), half - a - Px(6), Px(36)) : new Rectangle(x0 + a + Px(6), Px(72), half - a - Px(6), Px(36));
            TextRenderer.DrawText(g, value, fBig, vr, Color.White, (rtl ? TextFormatFlags.Right : TextFormatFlags.Left) | TextFormatFlags.NoPadding); // numbers stay left to right
        }
        Speed(0, false, L.T("Download"), app.Rate(app.Down), Art.DownGlow);
        Speed(1, true, L.T("Upload"), app.Rate(app.Up), Art.UpGlow);

        // One-minute graph.
        var gr = new Rectangle(pad, head.Bottom + Px(14), Width - pad * 2, Px(GraphH));
        Art.FillRound(g, gr, Px(10), t.Surface);
        using (var bp = Art.RoundRect(gr, Px(10))) using (var pen = new Pen(t.Border)) g.DrawPath(pen, bp);
        DrawGraph(g, Rectangle.FromLTRB(gr.Left + Px(8), gr.Top + Px(24), gr.Right - Px(8), gr.Bottom - Px(8)), t, rtl);
        string peak = $"{L.T("Peak")} \u200e{app.Rate(app.History.Peak)}"; // the mark keeps "6.98 MB/s" in order in Arabic
        TextRenderer.DrawText(g, peak, fLabel, new Rectangle(gr.Left + Px(10), gr.Top + Px(4), gr.Width - Px(20), Px(16)), t.Dim, far);

        // This month, all logged interfaces together.
        int y = gr.Bottom + Px(18);
        {
            var mr = new Rectangle(pad, y, Width - pad * 2, Px(30));
            Art.FillRound(g, mr, Px(6), Color.FromArgb(t.Dark ? 34 : 22, t.Accent));
            var inner = Rectangle.Inflate(mr, -Px(10), 0);
            int amountW = Px(170);
            var labelR = rtl ? new Rectangle(inner.Right - (inner.Width - amountW), inner.Top, inner.Width - amountW, inner.Height) : new Rectangle(inner.Left, inner.Top, inner.Width - amountW, inner.Height);
            string label = $"{L.T("ThisMonth")}  \u200E{ByteFormat.Size(app.MonthTotal.rx + app.MonthTotal.tx)}";
            TextRenderer.DrawText(g, label, fName, labelR, t.Text, near | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            var amt = rtl ? new Rectangle(inner.Left, inner.Top, amountW, inner.Height) : new Rectangle(inner.Right - amountW, inner.Top, amountW, inner.Height);
            DrawAmounts(g, amt, app.MonthTotal.rx, app.MonthTotal.tx, fSmall, t, rtl);
            y += Px(34);
        }

        // Today per interface.
        TextRenderer.DrawText(g, L.T("TodayUsage"), fName, new Rectangle(pad, y, Width - pad * 2, Px(22)), t.Text, near);
        y += Px(26);
        var rows = TodayRows();
        if (rows.Count == 0)
        {
            TextRenderer.DrawText(g, app.S.LogUsage ? L.T("NothingYet") : L.T("LoggingOff"), fSmall, new Rectangle(pad, y, Width - pad * 2, Px(RowH)), t.Dim, near | TextFormatFlags.VerticalCenter);
            y += Px(RowH);
        }
        foreach (var r in rows)
        {
            var rr = new Rectangle(pad, y, Width - pad * 2, Px(RowH) - Px(4));
            Art.FillRound(g, rr, Px(6), t.Surface);
            var inner = Rectangle.Inflate(rr, -Px(10), 0);
            int amountW = Px(170);
            var nameRect = rtl ? new Rectangle(inner.Right - (inner.Width - amountW), inner.Top, inner.Width - amountW, inner.Height)
                               : new Rectangle(inner.Left, inner.Top, inner.Width - amountW, inner.Height);
            TextRenderer.DrawText(g, r.InterfaceName, fSmall, nameRect, t.Text, near | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            var amt = rtl ? new Rectangle(inner.Left, inner.Top, amountW, inner.Height) : new Rectangle(inner.Right - amountW, inner.Top, amountW, inner.Height);
            DrawAmounts(g, amt, r.Received, r.Sent, fSmall, t, rtl);
            y += Px(RowH);
        }

        // Limits line, with its on/off switch at the far edge.
        y = LimitsLineY;
        int limitedCount = app.S.Limits.Count(l => l.Enabled && (l.DownBytesPerSecond > 0 || l.UpBytesPerSecond > 0));
        string lim = !app.S.LimitsEnabled || limitedCount == 0 ? L.T("LimitsOff")
                   : LimiterClient.HelperRunning ? L.F(limitedCount == 1 ? "LimitsOnOne" : "LimitsOn", limitedCount) : L.T("LimitsPaused");
        Color limC = app.S.LimitsEnabled && LimiterClient.HelperRunning && limitedCount > 0 ? t.Up : t.Dim;
        using var fIcon = new Font(Art.IconFontName, 13f);
        int iconW = Px(24);
        var ir = new Rectangle(rtl ? Width - pad - iconW : pad, y, iconW, Px(28));
        TextRenderer.DrawText(g, Art.IconLimit, fIcon, ir, limC, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        int toggleW = Px(54);
        var lr2 = rtl ? new Rectangle(pad + toggleW, y, Width - pad * 2 - iconW - Px(6) - toggleW, Px(28)) : new Rectangle(pad + iconW + Px(6), y, Width - pad * 2 - iconW - Px(6) - toggleW, Px(28));
        TextRenderer.DrawText(g, lim, fSmall, lr2, limC, near | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    static void DrawAmounts(Graphics g, Rectangle r, long rx, long tx, Font f, Theme t, bool rtl)
    {
        int half = r.Width / 2, a = (int)(9 * g.DpiX / 96f);
        void One(int col, bool up, long v, Color c)
        {
            int x0 = r.Left + (rtl ? 1 - col : col) * half;
            Art.Arrow(g, new RectangleF(x0 + (rtl ? half - a - 2 : 2), r.Top + (r.Height - a) / 2f, a, a), up, c);
            var tr = rtl ? new Rectangle(x0, r.Top, half - a - 6, r.Height) : new Rectangle(x0 + a + 6, r.Top, half - a - 6, r.Height);
            TextRenderer.DrawText(g, ByteFormat.Size(v), f, tr, t.Text, TextFormatFlags.VerticalCenter | (rtl ? TextFormatFlags.Right : TextFormatFlags.Left));
        }
        One(0, false, rx, t.Down);
        One(1, true, tx, t.Up);
    }

    void DrawGraph(Graphics g, Rectangle r, Theme t, bool rtl)
    {
        var items = app.History.Items;
        int cap = app.History.Capacity;
        double peak = Math.Max(app.History.Peak, 1024);
        PointF P(int i, double v)
        {
            float x = r.Left + r.Width * (cap - items.Count + i) / (float)(cap - 1);
            if (rtl) x = r.Right - (x - r.Left);
            return new PointF(x, r.Bottom - (float)(v / peak) * r.Height);
        }
        using (var grid = new Pen(Color.FromArgb(t.Dark ? 30 : 22, t.Text)) { DashStyle = DashStyle.Dot })
            for (int k = 1; k <= 3; k++) g.DrawLine(grid, r.Left, r.Bottom - r.Height * k / 4f, r.Right, r.Bottom - r.Height * k / 4f);
        if (items.Count < 2) return;
        var down = items.Select((v, i) => P(i, v.down)).ToArray();
        var up = items.Select((v, i) => P(i, v.up)).ToArray();
        using (var area = new GraphicsPath())
        {
            area.AddLines(down);
            area.AddLine(down[^1], new PointF(down[^1].X, r.Bottom));
            area.AddLine(new PointF(down[^1].X, r.Bottom), new PointF(down[0].X, r.Bottom));
            area.CloseFigure();
            using var fill = new LinearGradientBrush(new RectangleF(r.Left, r.Top - 1, r.Width, r.Height + 2),
                Color.FromArgb(120, t.Down), Color.FromArgb(10, t.Down), LinearGradientMode.Vertical);
            g.FillPath(fill, area);
        }
        using (var dp = new Pen(t.Down, 1.6f * S)) g.DrawLines(dp, down);
        using (var upPen = new Pen(t.Up, 1.6f * S)) g.DrawLines(upPen, up);
    }
}
