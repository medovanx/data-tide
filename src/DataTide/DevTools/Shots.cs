using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DataTide.DevTools;

/// <summary>
/// --shots &lt;dir&gt;: renders the widget, flyout, settings pages and report to PNGs for the README,
/// from <see cref="DemoData"/> only. Everything is drawn off-screen (DrawToBitmap), so no other window
/// and none of the user's own data can end up in an image.
/// </summary>
static class Shots
{
    public static int Run(string dir)
    {
        Directory.CreateDirectory(dir);
        ApplicationConfiguration.Initialize();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var ledger = DemoData.Ledger(today);
        var programLog = DemoData.ProgramLog(today);
        foreach (var lang in new[] { "en", "ar" })
        {
            var app = new DataTideApp(DemoData.Settings(lang), ledger);
            app.Demo(DemoData.Interfaces[0], DemoData.Minute());

            WidgetStrip(Path.Combine(dir, $"widget-{lang}.png"), app.Rate(app.Up), app.Rate(app.Down), lang == "ar");
            WidgetStrip(Path.Combine(dir, $"widget-limited-{lang}.png"), app.Rate(app.Up), app.Rate(app.Down), lang == "ar", limited: true);
            app.RefreshTotals();
            foreach (var dk in new[] { true, false })
            {
                var pick = new InterfacePicker([
                    new(AppSettings.AutoInterface, L.T("Auto"), L.T("AutoShort"), false),
                    new("demo-eth", DemoData.Interfaces[0], null, true),
                    new("demo-wifi", DemoData.Interfaces[1], null, false)], AppSettings.AutoInterface, new Theme(dk))
                    { RightToLeft = lang == "ar" ? RightToLeft.Yes : RightToLeft.No };
                pick.Size = pick.Preferred(250);
                using var pb = new Bitmap(pick.Width, pick.Height);
                pick.DrawToBitmap(pb, new Rectangle(Point.Empty, pick.Size));
                pb.Save(Path.Combine(dir, $"picker-{lang}-{(dk ? "dark" : "light")}.png"), ImageFormat.Png);
                pick.Dispose();
            }
            {
                var acts = new PopupList([
                    new("limit:2097152", L.F("LimitTo", L.Ltr(ByteFormat.Rate(2097152))), Current: true, Glyph: Art.IconLimit),
                    new("limit:524288", L.F("LimitTo", L.Ltr(ByteFormat.Rate(524288))), Glyph: Art.IconLimit),
                    new("limit:131072", L.F("LimitTo", L.Ltr(ByteFormat.Rate(131072))), Glyph: Art.IconLimit),
                    PopupList.Item.Separator,
                    new("block", L.T("BlockAll"), Danger: true, Glyph: ""),
                    new("pause", L.T("PauseRule"), Glyph: ""),
                    new("remove", L.T("RemoveLimit"), Glyph: Art.IconDelete),
                    PopupList.Item.Separator,
                    new("more", L.T("MoreOptions"), Glyph: Art.IconSettings)], new Theme(true), "Example Launcher")
                    { RightToLeft = lang == "ar" ? RightToLeft.Yes : RightToLeft.No };
                acts.Size = acts.Preferred(230);
                using var ab = new Bitmap(acts.Width, acts.Height);
                acts.DrawToBitmap(ab, new Rectangle(Point.Empty, acts.Size));
                ab.Save(Path.Combine(dir, $"actions-{lang}.png"), ImageFormat.Png);
                acts.Dispose();
            }
            var tip = new HoverTip();
            tip.ShowForShot(app, true);
            SaveControl(tip, Path.Combine(dir, $"hover-{lang}.png"), rounded: true);
            tip.Dispose();

            foreach (var dark in new[] { true, false })
            {
                var flyout = new TrafficFlyout(app);
                flyout.ShowForShot(dark);
                SaveControl(flyout, Path.Combine(dir, $"flyout-{lang}-{(dark ? "dark" : "light")}.png"), rounded: true);
                flyout.Dispose();
            }

            var speed = new SpeedTestForm(app) { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, -4000) };
            speed.Show();
            speed.ShowForShot(new SpeedTestResult(DateTime.Now.AddMinutes(-3), 18, 2.4, 94.6e6, 38.2e6));
            SaveClient(speed, Path.Combine(dir, $"speedtest-{lang}.png"));
            speed.Close();

            var settings = new SettingsForm(app) { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, -4000) };
            settings.Show();
            for (int i = 0; i < settings.PageCount; i++)
            {
                settings.ShowPage(i);
                SaveClient(settings, Path.Combine(dir, $"settings-{i}-{lang}.png"));
            }
            settings.ShowPage(SettingsForm.UpdatesPage);
            app.DemoUpdate(true, 0.42, L.F("UpdateDownloading", "9.9.9", L.Ltr($"42%  {ByteFormat.Size(34_400_000)} / {ByteFormat.Size(81_920_000)}")));
            SaveClient(settings, Path.Combine(dir, $"settings-updating-{lang}.png"));
            app.DemoUpdate(true, null, L.T("UpdateVerifying"));
            SaveClient(settings, Path.Combine(dir, $"settings-verifying-{lang}.png"));
            app.DemoUpdate(false, null, "");
            settings.Close();

            var (from, to) = UsageReport.Resolve(ReportRange.Last30Days, today);
            var report = UsageReport.Build(ledger, from, to, programs: programLog);
            ReportExporter.SavePng(report, L.T("AllInterfaces"), Path.Combine(dir, $"report-{lang}.png"));
            var (wf, wt) = UsageReport.Resolve(ReportRange.Last7Days, today);
            DarkReport(UsageReport.Build(ledger, wf, wt, programs: programLog), Path.Combine(dir, $"report-week-{lang}-dark.png"));
            if (lang == "en") ReportExporter.SavePdf(report, L.T("AllInterfaces"), Path.Combine(dir, "report-en.pdf"));
            if (lang == "ar") ReportExporter.SavePdf(report, L.T("AllInterfaces"), Path.Combine(dir, "report-ar.pdf"));
        }
        return 0;
    }

    static void DarkReport(UsageReport r, string path)
    {
        var t = new Theme(true);
        var rr = new ReportRenderer(r, t, L.Ar, 1.25f, 1250, L.T("AllInterfaces"));
        using var bmp = new Bitmap(1250, rr.TotalHeight);
        using (var g = Graphics.FromImage(bmp)) { g.Clear(t.Window); rr.DrawAll(g); }
        bmp.Save(path, ImageFormat.Png);
    }

    /// <summary>The widget on a dark taskbar-coloured strip, at 150% scale for a crisp README image.</summary>
    static void WidgetStrip(string path, string up, string down, bool rtl, bool limited = false)
    {
        const float s = 1.5f;
        int h = (int)(48 * s);
        using var widget = TaskbarWidget.Paint(up, down, false, rtl, limited, true, s, h);
        using var strip = new Bitmap(widget.Width + (int)(40 * s), h);
        using (var g = Graphics.FromImage(strip))
        {
            g.Clear(Color.FromArgb(28, 28, 28));
            g.DrawImage(widget, (int)(20 * s), 0);
        }
        strip.Save(path, ImageFormat.Png);
    }

    /// <summary>A borderless popup drawn off-screen, with its rounded corners cut out.</summary>
    static void SaveControl(Control c, string path, bool rounded)
    {
        c.Refresh();
        Application.DoEvents();
        using var bmp = new Bitmap(c.Width, c.Height);
        c.DrawToBitmap(bmp, new Rectangle(Point.Empty, c.Size));
        if (!rounded) { bmp.Save(path, ImageFormat.Png); return; }
        using var outBmp = new Bitmap(c.Width, c.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(outBmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var p = Art.RoundRect(new RectangleF(0, 0, c.Width - 1, c.Height - 1), 8 * c.DeviceDpi / 96f);
            using var tb = new TextureBrush(bmp);
            g.FillPath(tb, p);
        }
        outBmp.Save(path, ImageFormat.Png);
    }

    /// <summary>
    /// A window's client area (sidebar, page and buttons) without its title bar, drawn child by child off-screen.
    /// </summary>
    static void SaveClient(Form f, string path)
    {
        f.Refresh();
        for (int i = 0; i < 5; i++) Application.DoEvents();
        var size = f.ClientSize;
        using var bmp = new Bitmap(size.Width, size.Height);
        using (var g = Graphics.FromImage(bmp)) g.Clear(f.BackColor);
        foreach (Control c in f.Controls)
        {
            if (!c.Visible || c.Width <= 0 || c.Height <= 0) continue;
            using var part = new Bitmap(c.Width, c.Height);
            c.DrawToBitmap(part, new Rectangle(Point.Empty, c.Size));
            using var g = Graphics.FromImage(bmp);
            g.DrawImage(part, c.Location);
        }
        bmp.Save(path, ImageFormat.Png);
    }
}
