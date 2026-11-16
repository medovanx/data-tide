using System.Drawing.Drawing2D;

namespace DataTide.UI.SpeedTest;

/// <summary>Three tiles (ping, download, upload) plus a progress line under them, drawn in the app theme.</summary>
sealed class SpeedTiles : Control
{
    public Theme Theme { get; set; } = Theme.Current;

    public double PingMs { get; set; } = double.NaN;
    public double JitterMs { get; set; } = double.NaN;
    public double DownBps { get; set; } = double.NaN;
    public double UpBps { get; set; } = double.NaN;
    /// <summary>Which tile is being measured now (it gets the accent border), or null.</summary>
    public SpeedTestPhase? Active { get; set; }
    public double Fraction { get; set; }
    public string Status { get; set; } = "";

    public SpeedTiles()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Size = new Size(560, 150);
    }

    float S => DeviceDpi / 96f;
    int Px(float v) => (int)Math.Round(v * S);

    /// <summary>"25.3 Mbps" for display; speed tests are read in megabits.</summary>
    public static string Mbps(double bitsPerSecond) =>
        double.IsNaN(bitsPerSecond) ? "-" : bitsPerSecond >= 100e6
            ? $"{bitsPerSecond / 1e6:0} Mbps".Replace(',', '.')
            : $"{bitsPerSecond / 1e6:0.0} Mbps".Replace(',', '.');

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Styler.Backdrop(this, Theme.Surface));
        var t = Theme;
        bool rtl = RightToLeft == RightToLeft.Yes;
        var near = rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left;
        using var fLabel = new Font("Segoe UI", 9f);
        using var fValue = new Font("Segoe UI Semibold", 17f);
        using var fSub = new Font("Segoe UI", 8.5f);

        int gap = Px(10), tw = (Width - gap * 2) / 3, th = Px(96);
        var tiles = new (SpeedTestPhase phase, string label, string value, string sub, Color color, bool? up)[]
        {
            (SpeedTestPhase.Ping, L.T("Ping"), double.IsNaN(PingMs) ? "-" : $"{PingMs:0} ms",
                double.IsNaN(JitterMs) ? "" : L.F("Jitter", $"{JitterMs:0}"), t.Accent, null),
            (SpeedTestPhase.Download, L.T("Download"), Mbps(DownBps), double.IsNaN(DownBps) ? "" : ByteFormat.Rate(DownBps / 8), t.Down, false),
            (SpeedTestPhase.Upload, L.T("Upload"), Mbps(UpBps), double.IsNaN(UpBps) ? "" : ByteFormat.Rate(UpBps / 8), t.Up, true),
        };
        for (int i = 0; i < 3; i++)
        {
            int col = rtl ? 2 - i : i;
            var box = new Rectangle(col * (tw + gap), 0, tw, th);
            bool active = Active == tiles[i].phase;
            Art.FillRound(g, box, Px(10), t.Input);
            using (var p = Art.RoundRect(new RectangleF(box.X + 0.5f, box.Y + 0.5f, box.Width - 1, box.Height - 1), Px(10)))
            using (var pen = new Pen(active ? tiles[i].color : t.Border, active ? 1.6f * S : 1f))
                g.DrawPath(pen, p);
            var inner = Rectangle.Inflate(box, -Px(14), 0);
            int labelX = inner.X;
            if (tiles[i].up is bool up)
            {
                float a = Px(11);
                Art.Arrow(g, new RectangleF(rtl ? inner.Right - a : inner.X, box.Top + Px(14) + (Px(18) - a) / 2, a, a), up, tiles[i].color);
            }
            int shift = tiles[i].up == null ? 0 : Px(17);
            var lr = rtl ? new Rectangle(inner.X, box.Top + Px(12), inner.Width - shift, Px(22)) : new Rectangle(inner.X + shift, box.Top + Px(12), inner.Width - shift, Px(22));
            TextRenderer.DrawText(g, tiles[i].label, fLabel, lr, t.Dim, near | TextFormatFlags.VerticalCenter);
            // Numbers with units stay left to right in Arabic too.
            var vf = (rtl ? TextFormatFlags.Right : TextFormatFlags.Left) | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, tiles[i].value, fValue, new Rectangle(inner.X, box.Top + Px(36), inner.Width, Px(34)), t.Text, vf);
            TextRenderer.DrawText(g, tiles[i].sub, fSub, new Rectangle(inner.X, box.Top + Px(68), inner.Width, Px(18)), t.Dim, vf);
        }

        // Progress line and status under the tiles.
        int y = th + Px(14);
        var track = new Rectangle(0, y, Width, Px(4));
        Art.FillRound(g, track, Px(2), t.Border);
        if (Active is not null and not SpeedTestPhase.Done && Fraction > 0)
        {
            var c = Active == SpeedTestPhase.Download ? t.Down : Active == SpeedTestPhase.Upload ? t.Up : t.Accent;
            int w = (int)(track.Width * Math.Clamp(Fraction, 0, 1));
            Art.FillRound(g, new Rectangle(rtl ? track.Right - w : track.Left, y, Math.Max(w, Px(4)), track.Height), Px(2), c);
        }
        TextRenderer.DrawText(g, Status, fLabel, new Rectangle(0, y + Px(10), Width, Px(22)), t.Dim, near | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
