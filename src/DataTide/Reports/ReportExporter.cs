namespace DataTide.Reports;

/// <summary>Saves a report as a PNG (one tall image) or a PDF (A4 pages). Exports always use the light theme, for printing.</summary>
static class ReportExporter
{
    /// <summary>PNG width in pixels: 1100 px of layout at 1.5x for a crisp image.</summary>
    const float PngScale = 1.5f;
    const int PngLayoutWidth = 1000;

    public static void SavePng(UsageReport report, string filterName, string path)
    {
        var t = Theme.Light;
        var rr = new ReportRenderer(report, t, L.Ar, PngScale, (int)(PngLayoutWidth * PngScale), filterName);
        using var bmp = new Bitmap((int)(PngLayoutWidth * PngScale), rr.TotalHeight);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(t.Window);
            rr.DrawAll(g);
        }
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    public static void SavePdf(UsageReport report, string filterName, string path)
    {
        var t = Theme.Light;
        // A4 at about 150 dpi: 1240 x 1754 pixels per page.
        const int pageW = 1240;
        int pageH = (int)(pageW * PdfWriter.A4HeightPt / PdfWriter.A4WidthPt);
        float scale = pageW / (float)PngLayoutWidth;
        var rr = new ReportRenderer(report, t, L.Ar, scale, pageW, filterName);
        var pages = rr.Paginate(pageH, (int)(10 * scale), (int)(30 * scale));
        var bitmaps = new List<Bitmap>();
        try
        {
            for (int i = 0; i < pages.Count; i++)
            {
                var bmp = new Bitmap(pageW, pageH);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    ReportRenderer.DrawPage(g, pages[i]);
                    using var f = new Font("Segoe UI", 10 * scale, GraphicsUnit.Pixel);
                    using var b = new SolidBrush(t.Dim);
                    using var sf = new StringFormat { Alignment = StringAlignment.Center };
                    g.DrawString(L.F("PageOf", i + 1, pages.Count), f, b, new RectangleF(0, pageH - 24 * scale, pageW, 18 * scale), sf);
                }
                bitmaps.Add(bmp);
            }
            using var fs = File.Create(path);
            PdfWriter.Write(fs, bitmaps, L.T("ReportTitle"));
        }
        finally { foreach (var b in bitmaps) b.Dispose(); }
    }

    public static string SuggestedName(UsageReport r, string ext) =>
        $"DataTide-usage-{r.From:yyyy-MM-dd}" + (r.From == r.To ? "" : $"-to-{r.To:yyyy-MM-dd}") + "." + ext;
}
