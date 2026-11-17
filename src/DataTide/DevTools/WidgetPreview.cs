using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace DataTide.DevTools;

/// <summary>
/// --widget-preview DIR: renders candidate widget layouts side by side on a taskbar-coloured sheet, from demo
/// numbers, so a layout can be picked before it is built into the real widget. Nothing here ships as a setting.
/// </summary>
static class WidgetPreview
{
    const float S = 1.5f;
    static readonly int H = (int)(48 * S);

    public static int Run(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (bool dark in new[] { true, false })
        {
            var tiles = new List<(string caption, Bitmap bmp)>
            {
                ("Now: speeds", TaskbarWidget.Paint("478 KB/s", "6.52 MB/s", false, false, false, dark, S, H)),
                ("Speeds + ping", Speeds(dark, ping: true)),
                ("One line", OneLine(dark)),
                ("Today and month", Totals(dark)),
                ("Ping only", PingOnly(dark)),
                ("Accent colour", Speeds(dark, pill: Accent())),
                ("No background", Speeds(dark, pill: Color.Empty)),
            };
            Sheet(tiles, dark, Path.Combine(dir, $"widget-preview-{(dark ? "dark" : "light")}.png"));
            foreach (var (_, b) in tiles) b.Dispose();
        }
        return 0;
    }

    /// <summary>The Windows accent colour (what Start and the taskbar use), or a teal fallback.</summary>
    static Color Accent()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (k?.GetValue("AccentColor") is int abgr)
                return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
        }
        catch { }
        return Color.FromArgb(0, 120, 212);
    }

    static Font Big() => new("Segoe UI Semibold", 13.5f * S, GraphicsUnit.Pixel);
    static Font Small() => new("Segoe UI", 10.5f * S, GraphicsUnit.Pixel);

    static (Bitmap bmp, Graphics g) Canvas(int w, bool dark, Color? pill)
    {
        var bmp = new Bitmap(w, H, PixelFormat.Format32bppArgb);
        var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.FromArgb(1, 0, 0, 0));
        var box = new RectangleF(1, 4 * S, w - 2, H - 8 * S);
        using var path = Art.RoundRect(box, 6 * S);
        if (pill is { IsEmpty: true }) return (bmp, g);
        Color a = pill ?? Art.SeaLight, b = pill.HasValue ? ControlPaint.Dark(pill.Value, 0.3f) : Art.SeaDark;
        using var bg = new LinearGradientBrush(box, Color.FromArgb(dark ? 55 : 40, a), Color.FromArgb(dark ? 30 : 22, b), LinearGradientMode.ForwardDiagonal);
        g.FillPath(bg, path);
        using var edge = new Pen(Color.FromArgb(dark ? 40 : 30, dark ? Color.White : Art.SeaDark), Math.Max(1f, S));
        g.DrawPath(edge, path);
        return (bmp, g);
    }

    static Color Fg(bool dark) => dark ? Color.White : Color.FromArgb(20, 20, 20);
    static Color DimC(bool dark) => dark ? Color.FromArgb(170, 255, 255, 255) : Color.FromArgb(150, 20, 20, 20);
    static Color UpC(bool dark) => dark ? Art.UpGlow : Color.FromArgb(200, 110, 0);
    static Color DownC(bool dark) => dark ? Art.DownGlow : Color.FromArgb(0, 118, 180);
    static readonly Color Good = Color.FromArgb(52, 199, 120);

    static Bitmap Speeds(bool dark, bool ping = false, Color? pill = null)
    {
        using var f = Big();
        using var fs = Small();
        int pad = (int)(9 * S), arrow = (int)(11 * S), gap = (int)(6 * S), textW = (int)(84 * S), pingW = ping ? (int)(52 * S) : 0;
        int w = pad + arrow + gap + textW + (ping ? (int)(10 * S) + pingW : 0) + pad;
        var (bmp, g) = Canvas(w, dark, pill);
        using (g)
        {
            float lineH = f.GetHeight(g), total = lineH * 2 - 2 * S, y1 = (H - total) / 2, y2 = y1 + lineH - 2 * S;
            Art.Arrow(g, new RectangleF(pad, y1 + (lineH - arrow) / 2, arrow, arrow), true, UpC(dark));
            Art.Arrow(g, new RectangleF(pad, y2 + (lineH - arrow) / 2, arrow, arrow), false, DownC(dark));
            using var sf = new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap };
            using var b = new SolidBrush(Fg(dark));
            g.DrawString("478 KB/s", f, b, new RectangleF(pad + arrow + gap, y1, textW, lineH), sf);
            g.DrawString("6.52 MB/s", f, b, new RectangleF(pad + arrow + gap, y2, textW, lineH), sf);
            if (ping)
            {
                float x = pad + arrow + gap + textW + 10 * S;
                using var sep = new Pen(Color.FromArgb(dark ? 50 : 40, Fg(dark)), Math.Max(1, S));
                g.DrawLine(sep, x - 5 * S, 12 * S, x - 5 * S, H - 12 * S);
                using var dim = new SolidBrush(DimC(dark));
                using var center = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString("18", f, b, new RectangleF(x, y1, pingW, lineH), center);
                float d = 6 * S;
                var msSize = g.MeasureString("ms", fs);
                float mx = x + (pingW - (msSize.Width + d + 3 * S)) / 2;
                using (var gb = new SolidBrush(Good)) g.FillEllipse(gb, mx, y2 + (lineH - d) / 2 + 1 * S, d, d);
                g.DrawString("ms", fs, dim, mx + d + 3 * S, y2 + (lineH - msSize.Height) / 2 + 1 * S);
            }
        }
        return bmp;
    }

    static Bitmap OneLine(bool dark)
    {
        using var f = Big();
        int pad = (int)(10 * S), arrow = (int)(11 * S), gap = (int)(5 * S), tw = (int)(80 * S);
        int w = pad + (arrow + gap + tw) * 2 + (int)(10 * S) + pad;
        var (bmp, g) = Canvas(w, dark, null);
        using (g)
        {
            float lineH = f.GetHeight(g), y = (H - lineH) / 2;
            using var b = new SolidBrush(Fg(dark));
            float x = pad;
            Art.Arrow(g, new RectangleF(x, y + (lineH - arrow) / 2, arrow, arrow), false, DownC(dark));
            g.DrawString("6.52 MB/s", f, b, x + arrow + gap, y);
            x += arrow + gap + tw + 10 * S;
            Art.Arrow(g, new RectangleF(x, y + (lineH - arrow) / 2, arrow, arrow), true, UpC(dark));
            g.DrawString("478 KB/s", f, b, x + arrow + gap, y);
        }
        return bmp;
    }

    static Bitmap Totals(bool dark)
    {
        using var f = Big();
        using var fs = Small();
        int pad = (int)(9 * S), labelW = (int)(46 * S), tw = (int)(70 * S);
        int w = pad + labelW + tw + pad;
        var (bmp, g) = Canvas(w, dark, null);
        using (g)
        {
            float lineH = f.GetHeight(g), total = lineH * 2 - 2 * S, y1 = (H - total) / 2, y2 = y1 + lineH - 2 * S;
            using var b = new SolidBrush(Fg(dark));
            using var dim = new SolidBrush(DimC(dark));
            using var far = new StringFormat { Alignment = StringAlignment.Far };
            float ly = (lineH - fs.GetHeight(g)) / 2 + 1 * S;
            g.DrawString("Today", fs, dim, pad, y1 + ly);
            g.DrawString("Month", fs, dim, pad, y2 + ly);
            g.DrawString("2.31 GB", f, b, new RectangleF(pad + labelW, y1, tw, lineH), far);
            g.DrawString("41.6 GB", f, b, new RectangleF(pad + labelW, y2, tw, lineH), far);
        }
        return bmp;
    }

    static Bitmap PingOnly(bool dark)
    {
        using var f = Big();
        using var fs = Small();
        int pad = (int)(10 * S), w = pad * 2 + (int)(78 * S);
        var (bmp, g) = Canvas(w, dark, null);
        using (g)
        {
            float lineH = f.GetHeight(g), total = lineH * 2 - 2 * S, y1 = (H - total) / 2, y2 = y1 + lineH - 2 * S;
            using var b = new SolidBrush(Fg(dark));
            using var dim = new SolidBrush(DimC(dark));
            float d = 7 * S;
            using (var gb = new SolidBrush(Good)) g.FillEllipse(gb, pad, y1 + (lineH - d) / 2, d, d);
            g.DrawString("18 ms", f, b, pad + d + 5 * S, y1);
            g.DrawString("Jitter 2  Loss 0%", fs, dim, pad, y2 + 2 * S);
        }
        return bmp;
    }

    static void Sheet(List<(string caption, Bitmap bmp)> tiles, bool dark, string path)
    {
        int gap = (int)(18 * S), capH = (int)(22 * S);
        int w = tiles.Sum(t => t.bmp.Width) + gap * (tiles.Count + 1);
        using var sheet = new Bitmap(w, H + capH + gap);
        using var g = Graphics.FromImage(sheet);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(dark ? Color.FromArgb(28, 28, 28) : Color.FromArgb(238, 240, 243));
        using var f = new Font("Segoe UI", 9.5f * S, GraphicsUnit.Pixel);
        using var b = new SolidBrush(dark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(90, 90, 90));
        using var center = new StringFormat { Alignment = StringAlignment.Center };
        int x = gap;
        foreach (var (caption, bmp) in tiles)
        {
            g.DrawImage(bmp, x, gap / 2);
            g.DrawString(caption, f, b, new RectangleF(x - gap / 2f, gap / 2 + H, bmp.Width + gap, capH), center);
            x += bmp.Width + gap;
        }
        sheet.Save(path, ImageFormat.Png);
    }
}
