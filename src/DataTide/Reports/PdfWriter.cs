using System.Drawing.Imaging;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace DataTide.Reports;

/// <summary>
/// Writes a PDF whose pages are images (one RGB bitmap per page, Flate-compressed), with no outside library.
/// Rendering the pages with GDI+ keeps Arabic shaping and right-to-left layout exactly as on screen.
/// </summary>
static class PdfWriter
{
    public const float A4WidthPt = 595.28f, A4HeightPt = 841.89f;

    public static void Write(Stream output, IReadOnlyList<Bitmap> pages, string title)
    {
        var inv = CultureInfo.InvariantCulture;
        var offsets = new List<long>();
        var pdf = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        void Raw(string s) => pdf.Write(Encoding.ASCII.GetBytes(s));
        void Obj(int n) { pdf.Flush(); offsets.Add(output.Position); Raw($"{n} 0 obj\n"); }

        Raw("%PDF-1.4\n");
        pdf.Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' }); // marks the file as binary
        int n = pages.Count;
        // Objects: 1 catalog, 2 pages, 3 info, then per page: page, content, image.
        int First(int i) => 4 + i * 3;

        Obj(1); Raw("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        Obj(2);
        Raw("<< /Type /Pages /Count " + n + " /Kids [" + string.Join(" ", Enumerable.Range(0, n).Select(i => $"{First(i)} 0 R")) + "] >>\nendobj\n");
        Obj(3);
        Raw($"<< /Title {PdfText(title)} /Producer (Data Tide {Updater.CurrentText}) /Creator (Data Tide) /CreationDate (D:{DateTime.Now:yyyyMMddHHmmss}) >>\nendobj\n");

        for (int i = 0; i < n; i++)
        {
            var bmp = pages[i];
            float w = A4WidthPt, h = A4WidthPt * bmp.Height / bmp.Width;
            string wS = w.ToString("0.##", inv), hS = h.ToString("0.##", inv);
            Obj(First(i));
            Raw($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {wS} {hS}] /Resources << /XObject << /Im0 {First(i) + 2} 0 R >> >> /Contents {First(i) + 1} 0 R >>\nendobj\n");
            var content = Encoding.ASCII.GetBytes($"q {wS} 0 0 {hS} 0 0 cm /Im0 Do Q\n");
            Obj(First(i) + 1);
            Raw($"<< /Length {content.Length} >>\nstream\n"); pdf.Write(content); Raw("\nendstream\nendobj\n");
            var data = Deflate(Rgb(bmp));
            Obj(First(i) + 2);
            Raw($"<< /Type /XObject /Subtype /Image /Width {bmp.Width} /Height {bmp.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {data.Length} >>\nstream\n");
            pdf.Write(data); Raw("\nendstream\nendobj\n");
        }

        pdf.Flush();
        long xref = output.Position;
        Raw($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) Raw($"{o.ToString("D10", inv)} 00000 n \n");
        Raw($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R /Info 3 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        pdf.Flush();
    }

    /// <summary>A PDF text string in UTF-16BE hex, so Arabic titles survive.</summary>
    static string PdfText(string s)
    {
        var sb = new StringBuilder("<FEFF");
        foreach (char c in s) sb.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
        return sb.Append('>').ToString();
    }

    /// <summary>Top-to-bottom RGB bytes (the bitmap is BGR(A) in memory).</summary>
    static unsafe byte[] Rgb(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var outBytes = new byte[bmp.Width * bmp.Height * 3];
            int o = 0;
            for (int y = 0; y < bmp.Height; y++)
            {
                byte* row = (byte*)data.Scan0 + y * data.Stride;
                for (int x = 0; x < bmp.Width; x++, row += 3)
                {
                    outBytes[o++] = row[2];
                    outBytes[o++] = row[1];
                    outBytes[o++] = row[0];
                }
            }
            return outBytes;
        }
        finally { bmp.UnlockBits(data); }
    }

    static byte[] Deflate(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true)) z.Write(raw);
        return ms.ToArray();
    }
}
