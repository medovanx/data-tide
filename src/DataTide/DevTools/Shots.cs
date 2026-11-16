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
        foreach (var lang in new[] { "en", "ar" })
        {
            var app = new DataTideApp(DemoData.Settings(lang), ledger);
            app.Demo(DemoData.Interfaces[0], DemoData.Minute());

            WidgetStrip(Path.Combine(dir, $"widget-{lang}.png"), app.Rate(app.Up), app.Rate(app.Down), lang == "ar");

            foreach (var dark in new[] { true, false })
            {
                var flyout = new TrafficFlyout(app);
                flyout.ShowForShot(dark);
                SaveControl(flyout, Path.Combine(dir, $"flyout-{lang}-{(dark ? "dark" : "light")}.png"), rounded: true);
                flyout.Dispose();
            }

            var settings = new SettingsForm(app) { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, -4000) };
            settings.Show();
            for (int i = 0; i < settings.PageCount; i++)
            {
                settings.ShowPage(i);
                SaveClient(settings, Path.Combine(dir, $"settings-{i}-{lang}.png"));
            }
            settings.Close();

            var (from, to) = UsageReport.Resolve(ReportRange.Last30Days, today);
            var report = UsageReport.Build(ledger, from, to);
            ReportExporter.SavePng(report, L.T("AllInterfaces"), Path.Combine(dir, $"report-{lang}.png"));
            var (wf, wt) = UsageReport.Resolve(ReportRange.Last7Days, today);
            DarkReport(UsageReport.Build(ledger, wf, wt), Path.Combine(dir, $"report-week-{lang}-dark.png"));
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
    static void WidgetStrip(string path, string up, string down, bool rtl)
    {
        const float s = 1.5f;
        int h = (int)(48 * s);
        using var widget = TaskbarWidget.Paint(up, down, false, rtl, false, true, s, h);
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
