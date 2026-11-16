using System.Globalization;

namespace DataTide.UI.SpeedTest;

/// <summary>
/// Speed test (ping, download, upload) and a download-time calculator. Detect fills the calculator with the last
/// measured download speed, or runs a test first when there is none.
/// </summary>
sealed class SpeedTestForm : Form
{
    readonly DataTideApp app;
    readonly Theme t = Theme.Current;
    readonly SpeedTiles tiles = new() { Width = 560, Height = 150, Margin = new Padding(0, 0, 0, 6) };
    readonly IconButton btnStart = new() { Width = 150, Height = 34, Primary = true, Glyph = "" };
    readonly Label lblNote = new() { AutoSize = true, Tag = "dim", MaximumSize = new Size(390, 0), Margin = new Padding(12, 2, 0, 0) };
    readonly ThemedNumber numSize = new() { Minimum = 0, Maximum = 1_000_000, DecimalPlaces = 2, Increment = 1, Width = 150 };
    readonly ThemedCombo cmbSize = new() { Width = 90 };
    readonly ThemedNumber numSpeed = new() { Minimum = 0, Maximum = 100_000, DecimalPlaces = 1, Increment = 5, Width = 150 };
    readonly ThemedCombo cmbSpeed = new() { Width = 90 };
    readonly IconButton btnDetect = new() { Width = 110, Height = 30, Glyph = "" };
    readonly Label lblResult = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 18f), Margin = new Padding(0, 10, 0, 0) };
    readonly Label lblResultSub = new() { AutoSize = true, Tag = "dim", Margin = new Padding(2, 0, 0, 4) };
    readonly ToolTip tip = new();
    CancellationTokenSource? running;
    bool detectAfterTest;

    public SpeedTestForm(DataTideApp app)
    {
        this.app = app;
        Text = $"{L.T("SpeedTest")} - {L.T("AppName")}";
        Icon = app.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(640, 520);
        Font = new Font("Segoe UI", 9.75f);
        BackColor = t.Window;
        ForeColor = t.Text;
        if (L.Ar) RightToLeft = RightToLeft.Yes;

        btnStart.Text = L.T("StartTest");
        btnDetect.Text = L.T("Detect");
        lblNote.Text = L.T("SpeedNote");
        tiles.Theme = t;
        tiles.RightToLeft = RightToLeft;
        foreach (var u in Enum.GetValues<SizeUnit>()) cmbSize.Items.Add(u.ToString());
        cmbSpeed.Items.AddRange(["Mbps", "MB/s"]);
        cmbSize.SelectedIndex = 1;   // GB
        cmbSpeed.SelectedIndex = 0;  // Mbps
        numSize.Value = 1;

        var testCard = Card(L.T("SpeedTest"), "", tiles, Flow(btnStart, lblNote));
        var calcCard = Card(L.T("DownloadTime"), "",
            Row(L.T("FileSize"), Flow(numSize, cmbSize)),
            Row(L.T("ConnectionSpeed"), Flow(numSpeed, cmbSpeed, btnDetect)),
            lblResult, lblResultSub);
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20, 20, 20, 0), AutoScroll = true };
        flow.Controls.AddRange([testCard, calcCard]);
        Controls.Add(flow);
        Native.DarkScrollbars(flow, t.Dark);
        Styler.Apply(this, t);
        foreach (var b in new[] { btnStart, btnDetect }) { b.Theme = t; b.RightToLeft = RightToLeft; }

        if (app.S.LastSpeedTest is { } last)
        {
            ShowResult(last);
            numSpeed.Value = (decimal)Math.Round(last.DownBitsPerSecond / 1e6, 1);
        }
        else tiles.Status = L.T("NoTestYet");

        btnStart.Click += async (_, _) => await StartOrStopAsync();
        btnDetect.Click += async (_, _) => await DetectAsync();
        numSize.ValueChanged += (_, _) => Recalc();
        numSpeed.ValueChanged += (_, _) => Recalc();
        cmbSize.SelectedIndexChanged += (_, _) => Recalc();
        cmbSpeed.SelectedIndexChanged += (_, _) => ConvertSpeedUnit();
        FormClosing += (_, _) => running?.Cancel();
        FormClosed += (_, _) => tip.Dispose();
        Recalc();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RoundCorners(Handle, t.Dark);
        int caption = ColorTranslator.ToWin32(t.Window);
        Native.DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int)); // DWMWA_CAPTION_COLOR
    }

    SpeedUnit SpeedUnitNow => cmbSpeed.SelectedIndex == 1 ? SpeedUnit.MBps : SpeedUnit.Mbps;
    SpeedUnit lastSpeedUnit = SpeedUnit.Mbps;

    /// <summary>Switching Mbps and MB/s keeps the same speed: 25 Mbps becomes 3.0 MB/s.</summary>
    void ConvertSpeedUnit()
    {
        var now = SpeedUnitNow;
        if (now != lastSpeedUnit)
        {
            double bps = DownloadTime.BytesPerSecond((double)numSpeed.Value, lastSpeedUnit);
            numSpeed.Value = (decimal)Math.Round(now == SpeedUnit.Mbps ? bps * 8 / 1e6 : bps / (1024 * 1024), 1);
            lastSpeedUnit = now;
        }
        Recalc();
    }

    void Recalc()
    {
        var unit = (SizeUnit)Math.Max(0, cmbSize.SelectedIndex);
        var est = DownloadTime.Estimate((double)numSize.Value, unit, (double)numSpeed.Value, SpeedUnitNow);
        if (est is not { } time)
        {
            lblResult.Text = "-";
            lblResultSub.Text = L.T("EnterSpeed");
            return;
        }
        string d = DownloadTime.Format(time, (L.T("UnitDays"), L.T("UnitHours"), L.T("UnitMinutes"), L.T("UnitSeconds")));
        lblResult.Text = L.F("AboutTime", d);
        double bytesPerSec = DownloadTime.BytesPerSecond((double)numSpeed.Value, SpeedUnitNow);
        string size = $"{numSize.Value.ToString("0.##", CultureInfo.InvariantCulture)} {unit}";
        // Left-to-right marks keep "4.7 GB" and "94.6 Mbps" whole and in order inside Arabic text.
        static string Ltr(string s) => "‎" + s + "‎";
        lblResultSub.Text = L.F("AtSpeed", Ltr(size), $"{Ltr(SpeedTiles.Mbps(bytesPerSec * 8))} ({Ltr(ByteFormat.Rate(bytesPerSec))})");
    }

    async Task DetectAsync()
    {
        if (app.S.LastSpeedTest is { } last)
        {
            UseMeasured(last);
            return;
        }
        detectAfterTest = true;
        await StartOrStopAsync();
    }

    void UseMeasured(SpeedTestResult r)
    {
        double value = SpeedUnitNow == SpeedUnit.Mbps ? r.DownBitsPerSecond / 1e6 : r.DownBitsPerSecond / 8 / (1024 * 1024);
        numSpeed.Value = (decimal)Math.Round(value, 1);
        tip.SetToolTip(btnDetect, L.F("MeasuredAt", r.When.ToString("g", L.Culture)));
        Recalc();
    }

    async Task StartOrStopAsync()
    {
        if (running != null) { running.Cancel(); return; }
        running = new CancellationTokenSource();
        btnStart.Text = L.T("StopTest");
        btnStart.Glyph = "";
        btnDetect.Enabled = false;
        tiles.PingMs = tiles.JitterMs = tiles.DownBps = tiles.UpBps = double.NaN;
        var progress = new Progress<SpeedTestProgress>(p =>
        {
            tiles.Active = p.Phase;
            tiles.Fraction = p.Fraction;
            if (p.Phase == SpeedTestPhase.Ping) tiles.PingMs = p.PingMs;
            if (p.Phase == SpeedTestPhase.Download) tiles.DownBps = p.BitsPerSecond;
            if (p.Phase == SpeedTestPhase.Upload) tiles.UpBps = p.BitsPerSecond;
            tiles.Status = L.T("Testing" + p.Phase);
            tiles.Invalidate();
        });
        try
        {
            var r = await SpeedTester.RunAsync(progress, running.Token);
            app.S.LastSpeedTest = r;
            app.S.Save();
            ShowResult(r);
            if (detectAfterTest) UseMeasured(r);
        }
        catch (OperationCanceledException)
        {
            tiles.Status = L.T("TestStopped");
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            tiles.Status = L.T("TestFailed") + " " + ex.Message;
        }
        finally
        {
            running.Dispose();
            running = null;
            detectAfterTest = false;
            tiles.Active = null;
            btnStart.Text = L.T("StartTest");
            btnStart.Glyph = "";
            btnDetect.Enabled = true;
            tiles.Invalidate();
        }
    }

    void ShowResult(SpeedTestResult r)
    {
        tiles.PingMs = r.PingMs;
        tiles.JitterMs = r.JitterMs;
        tiles.DownBps = r.DownBitsPerSecond;
        tiles.UpBps = r.UpBitsPerSecond;
        tiles.Status = L.F("LastTest", r.When.ToString("g", L.Culture));
        tiles.Invalidate();
    }

    /// <summary>Lays the window out with a finished made-up result, for --shots.</summary>
    public void ShowForShot(SpeedTestResult demo)
    {
        ShowResult(demo);
        UseMeasured(demo);
        numSize.Value = 4.7m;
        tiles.Status = L.F("LastTest", demo.When.ToString("g", L.Culture));
    }

    // ---------- layout helpers (same look as the settings window) ----------

    Section Card(string title, string glyph, params Control[] rows)
    {
        var sec = new Section(title) { Glyph = glyph, Theme = t, RightToLeft = RightToLeft, MinimumSize = new Size(592, 0), MaximumSize = new Size(592, 0), Margin = new Padding(0, 0, 0, 12) };
        var stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Location = new Point(16, 44), MinimumSize = new Size(560, 0), MaximumSize = new Size(560, 0), RightToLeft = RightToLeft };
        foreach (var r in rows) stack.Controls.Add(r);
        sec.Controls.Add(stack);
        return sec;
    }

    static TableLayoutPanel Row(string label, Control c)
    {
        var tl = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Width = 560, Height = Math.Max(40, c.PreferredSize.Height + 8), Margin = Padding.Empty };
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tl.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        c.Anchor = AnchorStyles.Right;
        tl.Controls.Add(c, 1, 0);
        return tl;
    }

    static FlowLayoutPanel Flow(params Control[] cs)
    {
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 4) };
        int tallest = cs.Max(c => c.Height);
        for (int i = 0; i < cs.Length; i++)
        {
            cs[i].Margin = new Padding(0, (tallest - cs[i].Height) / 2, i < cs.Length - 1 ? 8 : 0, 0);
            p.Controls.Add(cs[i]);
        }
        return p;
    }
}
