using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace DataTide.Reports;

/// <summary>
/// Lays out and draws a usage report: header, summary tiles, a stacked bar chart with one colour per interface,
/// a table per interface and a daily table. The same drawing is used on screen, for PNG and for PDF pages,
/// and it is mirrored for Arabic.
/// </summary>
sealed class ReportRenderer
{
    readonly UsageReport r;
    readonly Theme t;
    readonly bool rtl;
    readonly float s;
    readonly int width;
    readonly string filterName;
    readonly Dictionary<string, Color> colors = [];
    public List<ReportBlock> Blocks { get; } = [];

    public int Margin => P(28);
    int Content => width - Margin * 2;
    int P(float v) => (int)Math.Round(v * s);

    public ReportRenderer(UsageReport report, Theme theme, bool isRtl, float scale, int pixelWidth, string interfaceFilterName)
    {
        r = report; t = theme; rtl = isRtl; s = scale; width = pixelWidth; filterName = interfaceFilterName;
        // Colours follow the interfaces' order in the whole report so the chart, legend and tables agree.
        for (int i = 0; i < r.Interfaces.Count; i++) colors[r.Interfaces[i].InterfaceId] = Art.Series(i, t.Dark);
        Build();
    }

    public int TotalHeight => Blocks.Sum(b => b.Height);

    Font F(float px, bool bold = false) => new(bold ? "Segoe UI Semibold" : "Segoe UI", px * s, GraphicsUnit.Pixel);

    /// <param name="ltr">Numbers with units ("4.16 GB") stay left to right in Arabic too, or they read "GB 4.16".</param>
    void Text(Graphics g, string text, Font f, RectangleF box, Color c, StringAlignment align = StringAlignment.Near, bool ellipsis = true, bool ltr = false)
    {
        using var sf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None };
        if (rtl && !ltr) sf.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
        // Without the RTL flag, Near/Far are physical: mirror them so Arabic still starts on the right.
        if (rtl && ltr && align != StringAlignment.Center) align = align == StringAlignment.Near ? StringAlignment.Far : StringAlignment.Near;
        sf.Alignment = align;
        using var b = new SolidBrush(c);
        g.DrawString(text, f, b, box, sf);
    }

    /// <summary>x of a span measured from the start edge (left in English, right in Arabic).</summary>
    float X(float fromStart, float w) => rtl ? width - Margin - fromStart - w : Margin + fromStart;

    static void Prepare(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    /// <summary>Draws every block one under the other (screen and PNG).</summary>
    public void DrawAll(Graphics g, int top = 0)
    {
        Prepare(g);
        int y = top;
        foreach (var b in Blocks) { b.Draw(g, y); y += b.Height; }
    }

    /// <summary>Splits the blocks into pages of <paramref name="pageHeight"/>; a table continued on a new page repeats its header.</summary>
    public List<List<(ReportBlock block, int y)>> Paginate(int pageHeight, int topPad, int bottomPad)
    {
        var pages = new List<List<(ReportBlock, int)>>();
        var page = new List<(ReportBlock, int)>();
        int y = topPad;
        ReportBlock? lastHeaderOwner = null;
        foreach (var b in Blocks)
        {
            if (y + b.Height > pageHeight - bottomPad && page.Count > 0)
            {
                pages.Add(page);
                page = [];
                y = topPad;
                if (b.IsTableRow && lastHeaderOwner?.RepeatHeader != null)
                {
                    var h = lastHeaderOwner;
                    page.Add((new ReportBlock(h.RepeatHeaderHeight, h.RepeatHeader!), y));
                    y += h.RepeatHeaderHeight;
                }
            }
            if (b.RepeatHeader != null) lastHeaderOwner = b;
            else if (!b.IsTableRow) lastHeaderOwner = null;
            page.Add((b, y));
            y += b.Height;
        }
        if (page.Count > 0) pages.Add(page);
        return pages;
    }

    public static void DrawPage(Graphics g, List<(ReportBlock block, int y)> page)
    {
        Prepare(g);
        foreach (var (b, y) in page) b.Draw(g, y);
    }

    // ---------- blocks ----------

    void Build()
    {
        Blocks.Add(new ReportBlock(P(118), Header));
        Blocks.Add(new ReportBlock(P(104), Tiles));
        Blocks.Add(new ReportBlock(P(44), (g, y) => SectionTitle(g, y, L.T("UsageOverTime"))));
        Blocks.Add(new ReportBlock(P(300), Chart));
        BuildInterfaceTable();
        BuildProgramTable();
        BuildDailyTable();
        Blocks.Add(new ReportBlock(P(56), Footer));
    }

    void Header(Graphics g, int y)
    {
        var band = new Rectangle(Margin, y + P(20), Content, P(88));
        using (var path = Art.RoundRect(band, P(14)))
        {
            using var grad = new LinearGradientBrush(band, Art.SeaLight, Art.SeaDark, LinearGradientMode.ForwardDiagonal);
            g.FillPath(grad, path);
            var st = g.Save();
            g.SetClip(path);
            Art.Waves(g, band, P(36), Color.FromArgb(24, 255, 255, 255));
            g.Restore(st);
        }
        int mark = P(48);
        float mx = rtl ? band.Right - P(20) - mark : band.Left + P(20);
        Art.Mark(g, new RectangleF(mx, band.Top + (band.Height - mark) / 2f, mark, mark));
        float tx = P(20) + mark + P(14);
        using var fTitle = F(22, true);
        using var fSub = F(13);
        var titleBox = new RectangleF(X(tx, Content - tx - P(20)), band.Top + P(14), Content - tx - P(20), P(32));
        Text(g, L.T("ReportTitle"), fTitle, titleBox, Color.White);
        string range = r.From == r.To ? L.LongDate(r.From) : L.F("RangeText", L.LongDate(r.From), L.LongDate(r.To));
        var subBox = new RectangleF(titleBox.X, band.Top + P(48), titleBox.Width, P(24));
        Text(g, $"{range}  |  {filterName}", fSub, subBox, Color.FromArgb(220, 236, 248));
    }

    void Tiles(Graphics g, int y)
    {
        int days = Math.Max(1, r.Days.Count);
        var tiles = new (string label, string value, Color accent)[]
        {
            (L.T("TotalUsage"), ByteFormat.Size(r.Total), t.Accent),
            (L.T("Download"), ByteFormat.Size(r.Received), t.Down),
            (L.T("Upload"), ByteFormat.Size(r.Sent), t.Up),
            (L.T("DailyAverage"), ByteFormat.Size(r.Total / days), t.Dim),
        };
        int gap = P(12), tw = (Content - gap * 3) / 4;
        using var fLabel = F(12);
        using var fValue = F(22, true);
        for (int i = 0; i < tiles.Length; i++)
        {
            var box = new RectangleF(X(i * (tw + gap), tw), y + P(12), tw, P(80));
            Art.FillRound(g, box, P(12), t.Surface);
            using (var p = Art.RoundRect(box, P(12))) using (var pen = new Pen(t.Border, Math.Max(1, s))) g.DrawPath(pen, p);
            // No accent bars: download and upload carry the widget's arrow beside their label, the rest stay plain.
            var inner = new RectangleF(box.Left + P(18), box.Top, box.Width - P(36), box.Height);
            float labelShift = 0;
            if (i is 1 or 2)
            {
                float a = P(11);
                float ax = rtl ? inner.Right - a : inner.Left;
                Art.Arrow(g, new RectangleF(ax, box.Top + P(14) + (P(20) - a) / 2, a, a), i == 2, tiles[i].accent);
                labelShift = a + P(6);
            }
            var labelBox = rtl ? new RectangleF(inner.X, box.Top + P(14), inner.Width - labelShift, P(20))
                               : new RectangleF(inner.X + labelShift, box.Top + P(14), inner.Width - labelShift, P(20));
            Text(g, tiles[i].label, fLabel, labelBox, t.Dim);
            Text(g, tiles[i].value, fValue, new RectangleF(inner.X, box.Top + P(36), inner.Width, P(32)), t.Text, ltr: true);
        }
    }

    void SectionTitle(Graphics g, int y, string title)
    {
        using var f = F(16, true);
        Text(g, title, f, new RectangleF(Margin, y + P(14), Content, P(26)), t.Text);
    }

    /// <summary>Buckets for the chart: days, or months when the range is longer than about two months.</summary>
    List<(string label, DateOnly from, DateOnly to)> Buckets()
    {
        var list = new List<(string, DateOnly, DateOnly)>();
        if (r.Days.Count <= 62)
        {
            foreach (var d in r.Days) list.Add((L.ShortDay(d, r.Days.Count), d, d));
            return list;
        }
        for (var m = new DateOnly(r.From.Year, r.From.Month, 1); m <= r.To; m = m.AddMonths(1))
        {
            var end = m.AddMonths(1).AddDays(-1);
            list.Add((L.MonthLabel(m), m < r.From ? r.From : m, end > r.To ? r.To : end));
        }
        return list;
    }

    void Chart(Graphics g, int y) =>
        Stacked(g, y, r.Interfaces.Select(i => (i.InterfaceName, colors[i.InterfaceId])).ToList(),
            (from, to) => r.Interfaces.Select(i => r.Rows.Where(x => x.InterfaceId == i.InterfaceId && x.Date >= from && x.Date <= to).Sum(x => x.Total)).ToArray());

    /// <summary>Programs shown by name in the programs-over-time chart; the rest are summed as one series.</summary>
    const int ChartPrograms = 5;

    void ProgramChart(Graphics g, int y)
    {
        var top = r.Programs.Take(ChartPrograms).Select(p => p.Name).ToList();
        bool other = r.Programs.Count > ChartPrograms;
        var series = top.Select((n, i) => (n, Art.Series(i, t.Dark))).ToList();
        if (other) series.Add((L.T("OtherProgramsShort"), t.Dim));
        Stacked(g, y, series, (from, to) =>
        {
            var v = new long[series.Count];
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                long all = r.ProgramDaily.Where(kv => kv.Key.day == d).Sum(kv => kv.Value), named = 0;
                for (int i = 0; i < top.Count; i++) { long x = r.ProgramOn(d, top[i]); v[i] += x; named += x; }
                if (other) v[^1] += all - named;
            }
            return v;
        });
    }

    /// <summary>A stacked bar chart per day (or month) with a legend, one colour per series.</summary>
    void Stacked(Graphics g, int y, IReadOnlyList<(string label, Color color)> series, Func<DateOnly, DateOnly, long[]> valuesFor)
    {
        var box = new RectangleF(Margin, y, Content, P(288));
        Art.FillRound(g, box, P(12), t.Surface);
        using (var p = Art.RoundRect(box, P(12))) using (var pen = new Pen(t.Border, Math.Max(1, s))) g.DrawPath(pen, p);

        var buckets = Buckets();
        var totals = buckets.Select(b => valuesFor(b.from, b.to)).ToList();
        long max = Math.Max(1, totals.Count == 0 ? 1 : totals.Max(a => a.Sum()));
        long step = NiceStep(max / 4.0);
        long top = Math.Max(step, (long)Math.Ceiling(max / (double)step) * step);

        using var fAxis = F(10.5f);
        int axisW = P(64), legendH = P(30);
        var plot = new RectangleF(box.Left + (rtl ? P(16) : axisW), box.Top + P(18), box.Width - axisW - P(16), box.Height - P(18) - P(26) - legendH);
        using var grid = new Pen(Color.FromArgb(t.Dark ? 34 : 26, t.Text), Math.Max(1, s)) { DashStyle = DashStyle.Dot };
        for (int k = 0; k <= 4; k++)
        {
            float gy = plot.Bottom - plot.Height * k / 4f;
            g.DrawLine(grid, plot.Left, gy, plot.Right, gy);
            var lab = new RectangleF(rtl ? plot.Right + P(4) : box.Left + P(6), gy - P(9), axisW - P(10), P(18));
            using var sf = new StringFormat { Alignment = rtl ? StringAlignment.Near : StringAlignment.Far, LineAlignment = StringAlignment.Center };
            using var b = new SolidBrush(t.Dim);
            g.DrawString(ByteFormat.Size(top * k / 4), fAxis, b, lab, sf);
        }

        int n = Math.Max(1, buckets.Count);
        float slot = plot.Width / n, barW = Math.Max(P(2), Math.Min(slot * 0.68f, P(46)));
        int labelEvery = Math.Max(1, (int)Math.Ceiling(n / (plot.Width / P(52))));
        for (int i = 0; i < buckets.Count; i++)
        {
            int idx = rtl ? buckets.Count - 1 - i : i;
            float cx = plot.Left + slot * i + slot / 2;
            float yb = plot.Bottom;
            for (int k = 0; k < series.Count; k++)
            {
                long v = totals[idx][k];
                if (v <= 0) continue;
                float h = (float)(plot.Height * v / (double)top);
                var seg = new RectangleF(cx - barW / 2, yb - h, barW, h);
                using var b = new SolidBrush(series[k].color);
                g.FillRectangle(b, seg);
                yb -= h;
            }
            if (i % labelEvery == 0)
                Text(g, buckets[idx].label, fAxis, new RectangleF(cx - slot * labelEvery / 2, plot.Bottom + P(4), slot * labelEvery, P(18)), t.Dim, StringAlignment.Center, false, ltr: !L.Ar || r.Days.Count > 7);
        }

        // Legend: one swatch per series.
        float lx = 0;
        using var fLeg = F(11.5f);
        float ly = box.Bottom - legendH - P(2);
        foreach (var (label, color) in series)
        {
            float w = g.MeasureString(label, fLeg).Width + P(30);
            if (lx + w > Content - P(32)) break;
            var sw = new RectangleF(X(P(16) + lx, P(12)), ly + P(9), P(12), P(12));
            Art.FillRound(g, sw, P(3), color);
            Text(g, label, fLeg, new RectangleF(X(P(16) + lx + P(18), w - P(18)), ly, w - P(18), P(30)), t.Text, StringAlignment.Near, false);
            lx += w + P(10);
        }
        if (series.Count == 0)
        {
            using var f = F(14);
            Text(g, L.T("NoData"), f, plot, t.Dim, StringAlignment.Center);
        }
    }

    static long NiceStep(double raw)
    {
        if (raw <= 1) return 1;
        double mag = Math.Pow(1024, Math.Floor(Math.Log(raw, 1024)));
        double n = raw / mag;
        double nice = n <= 1 ? 1 : n <= 2 ? 2 : n <= 2.5 ? 2.5 : n <= 5 ? 5 : n <= 10 ? 10 : n <= 25 ? 25 : n <= 50 ? 50 : n <= 100 ? 100 : n <= 250 ? 250 : n <= 500 ? 500 : 1024;
        return (long)(nice * mag);
    }

    // ---------- tables ----------

    /// <summary>Column starts and widths as fractions of the content width, from the start edge.</summary>
    void Row(Graphics g, int y, int h, float[] cols, string[] cells, Font f, Color c, Color? fill, Color? swatch = null, Color[]? cellColors = null, int nearColumns = 1, Image? icon = null)
    {
        if (fill is { } fc) Art.FillRound(g, new RectangleF(Margin, y + P(2), Content, h - P(4)), P(6), fc);
        float x = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            float w = Content * cols[i];
            float pad = P(12);
            float sx = 0;
            if (i == 0 && swatch is { } sc)
            {
                Art.FillRound(g, new RectangleF(X(x + pad, P(10)), y + (h - P(10)) / 2f, P(10), P(10)), P(3), sc);
                sx = P(18);
            }
            else if (i == 0 && icon != null)
            {
                int iz = P(18);
                var old = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(icon, new RectangleF(X(x + pad, iz), y + (h - iz) / 2f, iz, iz));
                g.InterpolationMode = old;
                sx = iz + P(10);
            }
            var align = i < nearColumns ? StringAlignment.Near : StringAlignment.Far;
            Text(g, cells[i], f, new RectangleF(X(x + pad + sx, w - pad * 2 - sx), y, w - pad * 2 - sx, h), cellColors?[i] ?? c, align, ltr: i >= nearColumns);
            x += w;
        }
    }

    void BuildInterfaceTable()
    {
        Blocks.Add(new ReportBlock(P(52), (g, y) => SectionTitle(g, y + P(8), L.T("ByInterface"))));
        float[] cols = [0.40f, 0.17f, 0.17f, 0.15f, 0.11f];
        int rh = P(34);
        void Head(Graphics g, int y)
        {
            using var f = F(11.5f, true);
            Row(g, y, rh, cols, [L.T("Interface"), L.T("Download"), L.T("Upload"), L.T("Total"), L.T("Share")], f, t.Dim, null);
            using var pen = new Pen(t.Border, Math.Max(1, s));
            g.DrawLine(pen, Margin, y + rh - 1, Margin + Content, y + rh - 1);
        }
        Blocks.Add(new ReportBlock(rh, Head, RepeatHeader: Head, RepeatHeaderHeight: rh));
        long all = Math.Max(1, r.Total);
        int n = 0;
        foreach (var i in r.Interfaces)
        {
            bool zebra = n++ % 2 == 1;
            var it = i;
            Blocks.Add(new ReportBlock(rh, (g, y) =>
            {
                using var f = F(12.5f);
                Row(g, y, rh, cols, [it.InterfaceName, ByteFormat.Size(it.Received), ByteFormat.Size(it.Sent), ByteFormat.Size(it.Total), $"{100.0 * it.Total / all:0.#}%"],
                    f, t.Text, zebra ? RowFill : null, colors[it.InterfaceId], [t.Text, t.Down, t.Up, t.Text, t.Dim]);
            }, IsTableRow: true));
        }
        if (r.Interfaces.Count > 1)
            Blocks.Add(new ReportBlock(rh, (g, y) =>
            {
                using var f = F(12.5f, true);
                using var pen = new Pen(t.Border, Math.Max(1, s));
                g.DrawLine(pen, Margin, y, Margin + Content, y);
                Row(g, y, rh, cols, [L.T("AllInterfaces"), ByteFormat.Size(r.Received), ByteFormat.Size(r.Sent), ByteFormat.Size(r.Total), "100%"], f, t.Text, null);
            }, IsTableRow: true));
    }

    /// <summary>Programs listed by name before the rest are summed into one "Other programs" row.</summary>
    const int MaxProgramRows = 12;

    void BuildProgramTable()
    {
        Blocks.Add(new ReportBlock(P(52), (g, y) => SectionTitle(g, y + P(8), L.T("ByProgram"))));
        // Windows gives a program's bytes without the adapter, so this table never follows the interface filter.
        Blocks.Add(new ReportBlock(P(26), (g, y) =>
        {
            using var f = F(11.5f);
            Text(g, L.T("ByProgramNote"), f, new RectangleF(Margin, y, Content, P(20)), t.Dim);
        }));
        // The busiest programs day by day, above the table (only when there is something to chart).
        if (r.Programs.Count > 0 && r.Days.Count > 1)
        {
            Blocks.Add(new ReportBlock(P(300), (g, y) => ProgramChart(g, y + P(6))));
            Blocks.Add(new ReportBlock(P(10), (_, _) => { }));
        }
        float[] cols = [0.40f, 0.17f, 0.17f, 0.15f, 0.11f];
        int rh = P(34);
        void Head(Graphics g, int y)
        {
            using var f = F(11.5f, true);
            Row(g, y, rh, cols, [L.T("Program"), L.T("Download"), L.T("Upload"), L.T("Total"), L.T("Share")], f, t.Dim, null);
            using var pen = new Pen(t.Border, Math.Max(1, s));
            g.DrawLine(pen, Margin, y + rh - 1, Margin + Content, y + rh - 1);
        }
        Blocks.Add(new ReportBlock(rh, Head, RepeatHeader: Head, RepeatHeaderHeight: rh));
        if (r.Programs.Count == 0)
        {
            Blocks.Add(new ReportBlock(rh, (g, y) => { using var f = F(12.5f); Text(g, L.T("NoProgramData"), f, new RectangleF(Margin + P(12), y, Content - P(24), rh), t.Dim); }, IsTableRow: true));
            return;
        }
        long all = Math.Max(1, r.Programs.Sum(p => p.Total));
        var shown = r.Programs.Take(MaxProgramRows).ToList();
        var rest = r.Programs.Skip(MaxProgramRows).ToList();
        int n = 0;
        foreach (var p in shown)
        {
            bool zebra = n++ % 2 == 1;
            var it = p;
            Blocks.Add(new ReportBlock(rh, (g, y) =>
            {
                using var f = F(12.5f);
                Row(g, y, rh, cols, [it.Name, ByteFormat.Size(it.Received), ByteFormat.Size(it.Sent), ByteFormat.Size(it.Total), $"{100.0 * it.Total / all:0.#}%"],
                    f, t.Text, zebra ? RowFill : null, null, [t.Text, t.Down, t.Up, t.Text, t.Dim], icon: ProgramIcons.For(it.Path));
            }, IsTableRow: true));
        }
        if (rest.Count > 0)
        {
            bool zebra = n % 2 == 1;
            long rx = rest.Sum(p => p.Received), tx = rest.Sum(p => p.Sent);
            Blocks.Add(new ReportBlock(rh, (g, y) =>
            {
                using var f = F(12.5f);
                Row(g, y, rh, cols, [L.F("OtherPrograms", rest.Count), ByteFormat.Size(rx), ByteFormat.Size(tx), ByteFormat.Size(rx + tx), $"{100.0 * (rx + tx) / all:0.#}%"],
                    f, t.Dim, zebra ? RowFill : null, null, [t.Dim, t.Down, t.Up, t.Text, t.Dim]);
            }, IsTableRow: true));
        }
    }

    Color RowFill => Color.FromArgb(t.Dark ? 18 : 10, t.Text);

    void BuildDailyTable()
    {
        Blocks.Add(new ReportBlock(P(60), (g, y) => SectionTitle(g, y + P(16), L.T("DailyUsage"))));
        float[] cols = [0.24f, 0.28f, 0.16f, 0.16f, 0.16f];
        int rh = P(32);
        void Head(Graphics g, int y)
        {
            using var f = F(11.5f, true);
            Row(g, y, rh, cols, [L.T("Date"), L.T("Interface"), L.T("Download"), L.T("Upload"), L.T("Total")], f, t.Dim, null, nearColumns: 2);
            using var pen = new Pen(t.Border, Math.Max(1, s));
            g.DrawLine(pen, Margin, y + rh - 1, Margin + Content, y + rh - 1);
        }
        Blocks.Add(new ReportBlock(rh, Head, RepeatHeader: Head, RepeatHeaderHeight: rh));
        var byDay = r.Rows.GroupBy(x => x.Date).OrderByDescending(gp => gp.Key).ToList();
        if (byDay.Count == 0)
            Blocks.Add(new ReportBlock(rh, (g, y) => { using var f = F(12.5f); Text(g, L.T("NoData"), f, new RectangleF(Margin, y, Content, rh), t.Dim); }, IsTableRow: true));
        int n = 0;
        foreach (var day in byDay)
        {
            bool zebra = n++ % 2 == 1;
            bool first = true;
            var rows = day.OrderByDescending(x => x.Total).ToList();
            foreach (var x in rows)
            {
                var row = x;
                string date = first ? L.MediumDate(day.Key) : "";
                first = false;
                Blocks.Add(new ReportBlock(rh, (g, y) =>
                {
                    using var f = F(12.5f);
                    Row(g, y, rh, cols, [date, row.InterfaceName, ByteFormat.Size(row.Received), ByteFormat.Size(row.Sent), ByteFormat.Size(row.Total)],
                        f, t.Text, zebra ? RowFill : null, null, [t.Text, colors.TryGetValue(row.InterfaceId, out var c) ? c : t.Text, t.Down, t.Up, t.Text], nearColumns: 2);
                }, IsTableRow: true));
            }
            if (rows.Count > 1)
            {
                long rx = rows.Sum(q => q.Received), tx = rows.Sum(q => q.Sent);
                Blocks.Add(new ReportBlock(rh, (g, y) =>
                {
                    using var f = F(12f, true);
                    Row(g, y, rh, cols, ["", L.T("DayTotal"), ByteFormat.Size(rx), ByteFormat.Size(tx), ByteFormat.Size(rx + tx)], f, t.Dim, zebra ? RowFill : null, nearColumns: 2);
                }, IsTableRow: true));
            }
        }
    }

    void Footer(Graphics g, int y)
    {
        using var f = F(11);
        Text(g, L.F("GeneratedBy", Updater.CurrentText, L.LongDate(DateOnly.FromDateTime(DateTime.Now)), DateTime.Now.ToString("HH:mm")), f,
            new RectangleF(Margin, y + P(16), Content, P(24)), t.Dim, StringAlignment.Center);
    }
}
