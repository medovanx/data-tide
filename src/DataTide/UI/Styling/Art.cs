using System.Drawing.Drawing2D;

namespace DataTide.UI.Styling;

/// <summary>Shared drawing: brand colours, arrows, the wave mark and the wave background pattern.</summary>
static class Art
{
    public static readonly Color SeaLight = Color.FromArgb(22, 140, 196);
    public static readonly Color SeaDark = Color.FromArgb(9, 40, 84);
    public static readonly Color Foam = Color.FromArgb(236, 248, 255);
    public static readonly Color DownGlow = Color.FromArgb(56, 189, 248);
    public static readonly Color UpGlow = Color.FromArgb(251, 191, 36);

    // Windows icon font: Segoe Fluent Icons (Win11) or Segoe MDL2 Assets (Win10) share these code points.
    public const string IconSettings = "\uE713", IconChart = "\uE9D2", IconSpeed = "\uEC4A", IconNetwork = "\uE968",
                        IconUpdate = "\uE895", IconGeneral = "\uE771", IconAdd = "\uE710", IconDelete = "\uE74D",
                        IconSave = "\uE74E", IconPicture = "\uEB9F", IconDocument = "\uE8A5", IconShield = "\uEA18",
                        IconOpen = "\uE8A7", IconApp = "\uECAA",
                        // A funnel for speed limits (throttling); the speedometer belongs to the speed test.
                        IconLimit = "\uE71C";

    public static readonly string IconFontName =
        new System.Drawing.Text.InstalledFontCollection().Families.Any(f => f.Name == "Segoe Fluent Icons")
            ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

    /// <summary>
    /// Draws an icon-font glyph centred on its real shape inside <paramref name="box"/>. Icon-font glyphs
    /// (the "more" dots, the plus) sit off-centre inside their text box, so centring the box looks lopsided.
    /// </summary>
    public static void GlyphCentered(Graphics g, string glyph, float emPixels, RectangleF box, Color c)
    {
        using var path = new GraphicsPath();
        using var family = new FontFamily(IconFontName);
        path.AddString(glyph, family, (int)FontStyle.Regular, emPixels, PointF.Empty, StringFormat.GenericTypographic);
        var b = path.GetBounds();
        if (b.Width <= 0 || b.Height <= 0) return;
        using (var m = new Matrix())
        {
            m.Translate(box.Left + (box.Width - b.Width) / 2f - b.Left, box.Top + (box.Height - b.Height) / 2f - b.Top);
            path.Transform(m);
        }
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(c);
        g.FillPath(brush, path);
        g.SmoothingMode = old;
    }

    public static GraphicsPath RoundRect(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, RectangleF r, float rad, Color c)
    {
        using var p = RoundRect(r, rad);
        using var b = new SolidBrush(c);
        g.FillPath(b, p);
    }

    /// <summary>A solid arrow head with a short stem, pointing up or down, centred in <paramref name="box"/>.</summary>
    public static void Arrow(Graphics g, RectangleF box, bool up, Color c)
    {
        float n = Math.Min(box.Width, box.Height);
        float cx = box.Left + box.Width / 2, top = box.Top + (box.Height - n) / 2;
        float head = n * 0.62f, stemW = n * 0.32f;
        PointF[] pts = up
            ? [new(cx, top + n * 0.06f), new(cx + head / 2 + n * 0.08f, top + head), new(cx + stemW / 2, top + head),
               new(cx + stemW / 2, top + n * 0.94f), new(cx - stemW / 2, top + n * 0.94f), new(cx - stemW / 2, top + head),
               new(cx - head / 2 - n * 0.08f, top + head)]
            : [new(cx, top + n * 0.94f), new(cx + head / 2 + n * 0.08f, top + n - head), new(cx + stemW / 2, top + n - head),
               new(cx + stemW / 2, top + n * 0.06f), new(cx - stemW / 2, top + n * 0.06f), new(cx - stemW / 2, top + n - head),
               new(cx - head / 2 - n * 0.08f, top + n - head)];
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var b = new SolidBrush(c);
        g.FillPolygon(b, pts);
        g.SmoothingMode = old;
    }

    /// <summary>One sine wave across <paramref name="r"/>, as an open path.</summary>
    public static GraphicsPath WavePath(RectangleF r, float amplitude, int crests, float phase = 0)
    {
        var p = new GraphicsPath();
        int steps = Math.Max(24, (int)r.Width / 2);
        var pts = new PointF[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float x = r.Left + r.Width * i / steps;
            double a = phase + Math.PI * 2 * crests * i / steps;
            pts[i] = new PointF(x, r.Top + r.Height / 2 - amplitude * (float)Math.Sin(a));
        }
        p.AddLines(pts);
        return p;
    }

    /// <summary>Rows of faint waves as a background pattern (the "tide").</summary>
    public static void Waves(Graphics g, Rectangle area, float cell, Color c)
    {
        using var pen = new Pen(c, Math.Max(1f, cell / 16f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var st = g.Save();
        g.SetClip(area);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int row = 0;
        for (float y = area.Top - cell / 2; y < area.Bottom + cell; y += cell * 0.55f, row++)
        {
            var band = new RectangleF(area.Left - cell, y, area.Width + cell * 2, cell / 2);
            using var w = WavePath(band, cell * 0.12f, Math.Max(1, (int)(band.Width / cell)), row % 2 == 0 ? 0 : (float)Math.PI);
            g.DrawPath(pen, w);
        }
        g.Restore(st);
    }

    /// <summary>The brand mark: a down arrow (cyan) and an up arrow (amber) riding a white wave.</summary>
    public static void Mark(Graphics g, RectangleF box, bool simple = false)
    {
        float n = Math.Min(box.Width, box.Height);
        float ox = box.Left + (box.Width - n) / 2, oy = box.Top + (box.Height - n) / 2;
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float aw = n * 0.36f, ah = n * (simple ? 0.62f : 0.5f);
        float ay = oy + n * (simple ? 0.14f : 0.1f);
        Arrow(g, new RectangleF(ox + n * 0.12f, ay, aw, ah), false, DownGlow);
        Arrow(g, new RectangleF(ox + n * 0.52f, ay, aw, ah), true, UpGlow);
        if (!simple)
        {
            using var pen = new Pen(Foam, n * 0.075f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var w = WavePath(new RectangleF(ox + n * 0.1f, oy + n * 0.66f, n * 0.8f, n * 0.2f), n * 0.065f, 2);
            g.DrawPath(pen, w);
        }
        g.SmoothingMode = old;
    }

    /// <summary>Distinct colours for interfaces in charts, in a fixed order.</summary>
    public static Color Series(int i, bool dark)
    {
        Color[] light = [Color.FromArgb(2, 132, 199), Color.FromArgb(217, 119, 6), Color.FromArgb(5, 150, 105),
                         Color.FromArgb(124, 58, 237), Color.FromArgb(219, 39, 119), Color.FromArgb(71, 85, 105),
                         Color.FromArgb(202, 138, 4), Color.FromArgb(14, 116, 144)];
        Color[] darkSet = [Color.FromArgb(56, 189, 248), Color.FromArgb(251, 191, 36), Color.FromArgb(52, 211, 153),
                           Color.FromArgb(167, 139, 250), Color.FromArgb(244, 114, 182), Color.FromArgb(148, 163, 184),
                           Color.FromArgb(250, 204, 21), Color.FromArgb(45, 212, 191)];
        var set = dark ? darkSet : light;
        return set[((i % set.Length) + set.Length) % set.Length];
    }
}
