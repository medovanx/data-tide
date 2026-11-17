using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using static DataTide.Interop.Native;

namespace DataTide.UI.Taskbar;

/// <summary>
/// A per-pixel-alpha child window inside the Windows taskbar (Shell_TrayWnd) showing upload and download speed.
/// It sits left of the notification area and takes the nearest free spot that overlaps no other app's window
/// embedded there (TrafficMonitor, Salaty Reminder...). Being a child of the taskbar, it hides with it in fullscreen.
/// </summary>
sealed class TaskbarWidget : NativeWindow
{
    const int WS_CHILD = 0x40000000, WS_CLIPSIBLINGS = 0x04000000;
    const int WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
    const int WM_MOUSEMOVE = 0x200, WM_LBUTTONUP = 0x202, WM_RBUTTONUP = 0x205, WM_MOUSELEAVE = 0x2A3, WM_MOUSEACTIVATE = 0x21;

    public event Action? Clicked;
    /// <summary>True when the mouse enters the widget, false when it leaves.</summary>
    public event Action<bool>? HoverChanged;
    public event Action? RightClicked;

    IntPtr tray;
    WidgetContent content = WidgetContent.Empty;
    WidgetLayout layout = WidgetLayout.Speeds;
    WidgetBackground background = WidgetBackground.Sea;
    bool hover, rtl, limited;
    Size size;
    Point lastPos = new(int.MinValue, 0);

    /// <summary>Left end of the taskbar instead of beside the tray.</summary>
    public bool LeftSide { get; private set; }
    /// <summary>Shift towards the taskbar centre, in pixels at 100% scale.</summary>
    public int Offset { get; private set; }

    public void SetPlacement(bool left, int offset)
    {
        if (left == LeftSide && offset == Offset) return;
        LeftSide = left;
        Offset = offset;
        lastPos = new(int.MinValue, 0);
        if (Alive) Place();
    }

    public bool Alive => Handle != IntPtr.Zero && IsWindow(Handle);

    public Rectangle? ScreenBounds => Alive && GetWindowRect(Handle, out var r)
        ? Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom) : null;

    /// <summary>Creates the window if needed (first run, Explorer restart) and keeps it positioned.</summary>
    public void Ensure()
    {
        var t = FindWindow("Shell_TrayWnd", null);
        if (t == IntPtr.Zero) return;
        if (Handle != IntPtr.Zero && (t != tray || !IsWindow(Handle)))
        {
            try { DestroyHandle(); } catch { ReleaseHandle(); }
        }
        if (Handle == IntPtr.Zero)
        {
            tray = t;
            lastPos = new(int.MinValue, 0);
            CreateHandle(new CreateParams
            {
                Caption = "DataTideTaskbarWindow",
                Parent = t,
                Style = WS_CHILD | WS_CLIPSIBLINGS,
                ExStyle = WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
                Width = 1, Height = 1,
            });
            Render();
        }
        Place();
    }

    public void SetContent(WidgetContent c, WidgetLayout look, WidgetBackground back, bool isRtl, bool isLimited)
    {
        if (c == content && look == layout && back == background && isRtl == rtl && isLimited == limited) return;
        bool relayout = isRtl != rtl || look != layout;
        content = c; layout = look; background = back; rtl = isRtl; limited = isLimited;
        if (!Alive) return;
        var before = size;
        Render();
        if (relayout || size != before) Place();
    }

    public void Invalidate() { if (Alive) { Render(); Place(); } }

    // ---------- update progress ----------

    bool updating;
    double? updateFraction;
    float updatePhase = 0.4f;
    System.Windows.Forms.Timer? updateAnim;

    /// <summary>
    /// Shows a thin progress line along the bottom of the pill while an update is checked for or installed,
    /// so it is visible without any window open. A null fraction slides (checking, verifying).
    /// </summary>
    public void SetUpdate(bool busy, double? fraction)
    {
        if (busy == updating && fraction == updateFraction) return;
        updating = busy;
        updateFraction = fraction;
        bool animate = busy && fraction == null;
        if (animate && updateAnim == null)
        {
            updateAnim = new System.Windows.Forms.Timer { Interval = 33 };
            updateAnim.Tick += (_, _) => { updatePhase = (updatePhase + 0.025f) % 1f; if (Alive) Render(); };
        }
        if (updateAnim != null) updateAnim.Enabled = animate;
        if (Alive) Render();
    }

    float Scale => tray != IntPtr.Zero && GetDpiForWindow(tray) is var d and > 0 ? d / 96f : 1f;

    void Place()
    {
        if (!GetWindowRect(tray, out var tr)) return;
        int gap = (int)(6 * Scale), shift = (int)(Offset * Scale);
        int y = Math.Max(0, (tr.Height - size.Height) / 2);
        Point pos;
        if (LeftSide)
            pos = new Point(Math.Clamp(gap + shift, 0, Math.Max(0, tr.Width - size.Width)), y);
        else
        {
            int right = FreeRightEdge(tr, TrayLeftEdge(tr) - shift, gap);
            pos = new Point(Math.Max(0, right - size.Width - tr.Left), y);
            if (IsWin10) MakeRoomOnWin10(tr, pos.X + tr.Left - gap);
        }
        // Compare with where the window really is, so anything that moved it gets corrected.
        if (pos == lastPos && IsWindowVisible(Handle) && GetWindowRect(Handle, out var cur)
            && cur.Left - tr.Left == pos.X && cur.Top - tr.Top == pos.Y) return;
        lastPos = pos;
        SetWindowPos(Handle, IntPtr.Zero, pos.X, pos.Y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    /// <summary>
    /// Screen x where our right edge goes: the nearest spot left of <paramref name="anchor"/> that overlaps
    /// no other app's window embedded in the right half of the taskbar. Only windows that would overlap us
    /// push us left, so two widgets that each avoid the other settle instead of chasing each other.
    /// </summary>
    int FreeRightEdge(RECT tr, int anchor, int gap)
    {
        var others = new List<RECT>();
        GetWindowThreadProcessId(tray, out uint explorerPid);
        int mid = tr.Left + tr.Width / 2;
        EnumChildWindows(tray, (h, _) =>
        {
            if (h == Handle || !IsWindowVisible(h)) return true;
            GetWindowThreadProcessId(h, out uint pid);
            if (pid == explorerPid || pid == Environment.ProcessId) return true;
            if (GetWindowRect(h, out var r) && r.Width > 0 && r.Left > mid) others.Add(r);
            return true;
        }, IntPtr.Zero);

        int right = anchor - gap;
        for (int pass = 0; pass < 12; pass++)
        {
            int left = right - size.Width;
            var hit = others.Where(r => r.Left < right + gap && r.Right > left - gap).ToList();
            if (hit.Count == 0) break;
            right = hit.Min(r => r.Left) - gap;
        }
        return right;
    }

    volatile int uiaTrayLeft;   // last tray edge found through UI Automation
    volatile bool uiaBusy;
    DateTime uiaCheckedAt;

    /// <summary>
    /// Screen x where the notification area starts. TrayNotifyWnd is right when it has real bounds,
    /// but on some Windows 11 builds it stays 0 px wide at x=0 after boot. Then ask UI Automation
    /// for the leftmost tray button.
    /// </summary>
    int TrayLeftEdge(RECT tr)
    {
        var notify = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        int mid = tr.Left + tr.Width / 2;
        if (notify != IntPtr.Zero && GetWindowRect(notify, out var nr) && nr.Width > 0 && nr.Left > mid && nr.Right <= tr.Right + 2)
            return nr.Left;

        // UI Automation runs on a background thread: this window is inside the taskbar, so querying
        // the taskbar from our own UI thread would call back into us and could deadlock.
        if (!uiaBusy && (DateTime.Now - uiaCheckedAt).TotalSeconds >= (uiaTrayLeft > mid ? 5 : 1))
        {
            uiaBusy = true;
            uiaCheckedAt = DateTime.Now;
            var taskbar = tray;
            Task.Run(() =>
            {
                int found = UiaTrayLeft(taskbar, mid);
                if (found > mid) uiaTrayLeft = found;
                uiaBusy = false;
            });
        }
        return uiaTrayLeft > mid ? uiaTrayLeft : tr.Right - (int)(300 * Scale); // until UIA answers: typical tray width
    }

    static int UiaTrayLeft(IntPtr taskbar, int mid)
    {
        try
        {
            var root = System.Windows.Automation.AutomationElement.FromHandle(taskbar);
            var cond = new System.Windows.Automation.PropertyCondition(
                System.Windows.Automation.AutomationElement.AutomationIdProperty, "SystemTrayIcon");
            int left = int.MaxValue;
            foreach (System.Windows.Automation.AutomationElement e in root.FindAll(System.Windows.Automation.TreeScope.Descendants, cond))
            {
                var r = e.Current.BoundingRectangle;
                if (r.Width > 0 && r.X > mid) left = Math.Min(left, (int)r.X);
            }
            return left == int.MaxValue ? 0 : left;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return 0;
        }
    }

    static readonly bool IsWin10 = Environment.OSVersion.Version.Build < 22000;

    /// <summary>
    /// Windows 10 task buttons run from the Start button to the tray, so shrink their strip
    /// (ReBarWindow32) to end before the widget. Explorer re-lays it out by itself when we exit.
    /// </summary>
    void MakeRoomOnWin10(RECT taskbar, int screenRight)
    {
        var rebar = FindWindowEx(tray, IntPtr.Zero, "ReBarWindow32", null);
        if (rebar == IntPtr.Zero || !GetWindowRect(rebar, out var rr)) return;
        if (taskbar.Height > taskbar.Width) return; // vertical taskbar: leave it alone
        if (rr.Right <= screenRight) return;
        int width = Math.Max(0, screenRight - rr.Left);
        SetWindowPos(rebar, IntPtr.Zero, rr.Left - taskbar.Left, rr.Top - taskbar.Top, width, rr.Height, SWP_NOACTIVATE | 0x4 /* NOZORDER */);
    }

    void Render()
    {
        if (!GetWindowRect(tray, out var tr)) return;
        using var bmp = Paint(content, layout, background, hover, rtl, limited, SystemDark(), Scale, tr.Height, updating, updateFraction, updatePhase);
        size = bmp.Size;
        var screen = GetDC(IntPtr.Zero);
        var mem = CreateCompatibleDC(screen);
        var hbmp = bmp.GetHbitmap(Color.FromArgb(0));
        var old = SelectObject(mem, hbmp);
        try
        {
            var sz = new SIZE { CX = size.Width, CY = size.Height };
            var src = new POINT();
            var blend = new BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
            UpdateLayeredWindow(Handle, screen, IntPtr.Zero, ref sz, mem, ref src, 0, ref blend, 2 /* ULW_ALPHA */);
        }
        finally
        {
            SelectObject(mem, old);
            DeleteObject(hbmp);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>The Windows accent colour (Start and the taskbar use it), or Windows blue when it can't be read.</summary>
    public static Color AccentColor()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (k?.GetValue("AccentColor") is int abgr) return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
        }
        catch { }
        return Color.FromArgb(0, 120, 212);
    }

    /// <summary>The classic widget (speeds on the sea-blue pill), for screenshots.</summary>
    public static Bitmap Paint(string up, string down, bool hover, bool rtl, bool limited, bool dark, float s, int h,
        bool updating = false, double? updateFraction = null, float updatePhase = 0.4f) =>
        Paint(WidgetContent.Empty with { Up = up, Down = down }, WidgetLayout.Speeds, WidgetBackground.Sea, hover, rtl, limited, dark, s, h, updating, updateFraction, updatePhase);

    /// <summary>
    /// Draws the widget in any layout. Widths come from the widest text each layout can show, never from the
    /// current values, so the widget doesn't jitter as numbers change. Everything is mirrored for Arabic.
    /// </summary>
    public static Bitmap Paint(WidgetContent c, WidgetLayout layout, WidgetBackground back, bool hover, bool rtl, bool limited, bool dark, float s, int h,
        bool updating = false, double? updateFraction = null, float updatePhase = 0.4f)
    {
        using var font = new Font("Segoe UI Semibold", 13.5f * s, GraphicsUnit.Pixel);
        using var small = new Font("Segoe UI", 10.5f * s, GraphicsUnit.Pixel);
        using var probe = new Bitmap(1, 1);
        using var pg = Graphics.FromImage(probe);
        pg.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float Wd(Font f, params string[] t) => t.Max(x => pg.MeasureString(x, f).Width);

        int pad = (int)(9 * s), arrow = (int)(11 * s), arrowGap = (int)(6 * s);
        int speedW = (int)Math.Ceiling(Wd(font, "888.8 KB/s", "888.8 Mb/s", c.Up, c.Down));
        int pingW = (int)Math.Ceiling(Wd(font, "888")) + (int)(4 * s);
        int w = layout switch
        {
            WidgetLayout.SpeedsPing => pad + arrow + arrowGap + speedW + (int)(12 * s) + pingW + pad,
            WidgetLayout.OneLine => pad + (arrow + (int)(5 * s) + speedW) * 2 + (int)(12 * s) + pad,
            WidgetLayout.Totals => pad + (int)Math.Ceiling(Wd(small, L.T("Today"), L.T("ThisMonth"))) + (int)(10 * s)
                                   + (int)Math.Ceiling(Wd(font, "888.8 GB", c.Today, c.Month)) + pad,
            WidgetLayout.Ping => pad + (int)Math.Ceiling(Math.Max(Wd(font, "8888 ms") + 12 * s, Wd(small, c.PingDetail, L.F("PingJitter", "88") + "  " + L.F("PingLoss", "88%")))) + pad,
            _ => pad + arrow + arrowGap + speedW + pad,
        };
        int X(float x, float width) => (int)(rtl ? w - x - width : x);

        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; // grayscale AA: correct on per-pixel alpha
        g.Clear(Color.FromArgb(1, 0, 0, 0)); // alpha 1 keeps the whole box clickable

        var box = new RectangleF(1, 4 * s, w - 2, h - 8 * s);
        if (back != WidgetBackground.None || hover)
        {
            // Soft pill, brighter on hover. With no background it only appears on hover, so the widget still answers the mouse.
            Color c1 = back == WidgetBackground.Accent ? AccentColor() : Art.SeaLight;
            Color c2 = back == WidgetBackground.Accent ? ControlPaint.Dark(c1, 0.3f) : Art.SeaDark;
            int a1 = hover ? (dark ? 95 : 70) : (dark ? 55 : 40);
            int a2 = hover ? (dark ? 70 : 50) : (dark ? 30 : 22);
            if (back == WidgetBackground.None) { a1 = dark ? 40 : 30; a2 = dark ? 26 : 18; }
            using var path = Art.RoundRect(box, 6 * s);
            using var bg = new LinearGradientBrush(box, Color.FromArgb(a1, c1), Color.FromArgb(a2, c2), LinearGradientMode.ForwardDiagonal);
            g.FillPath(bg, path);
            if (back != WidgetBackground.None)
            {
                using var edge = new Pen(Color.FromArgb(dark ? 40 : 30, dark ? Color.White : Art.SeaDark), Math.Max(1f, s));
                g.DrawPath(edge, path);
            }
        }
        if (limited)
        {
            // Speed limits active: a small amber dot in the far top corner, like a notification badge.
            float d = 7 * s;
            float dx = rtl ? box.Left + 3 * s : box.Right - d - 3 * s, dy = box.Top + 3 * s;
            using var ring = new SolidBrush(dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(240, 240, 240));
            g.FillEllipse(ring, dx - s, dy - s, d + 2 * s, d + 2 * s);
            using var dot = new SolidBrush(Art.UpGlow);
            g.FillEllipse(dot, dx, dy, d, d);
        }

        Color upC = dark ? Art.UpGlow : Color.FromArgb(200, 110, 0);
        Color downC = dark ? Art.DownGlow : Color.FromArgb(0, 118, 180);
        Color fg = dark ? Color.White : Color.FromArgb(20, 20, 20);
        Color dim = dark ? Color.FromArgb(175, 255, 255, 255) : Color.FromArgb(150, 20, 20, 20);
        using var b = new SolidBrush(fg);
        using var bDim = new SolidBrush(dim);
        float lineH = font.GetHeight(g);
        float total = lineH * 2 - 2 * s;
        float y1 = (h - total) / 2, y2 = y1 + lineH - 2 * s;
        using var farFmt = new StringFormat { Alignment = rtl ? StringAlignment.Near : StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap };
        using var nearFmt = new StringFormat { Alignment = StringAlignment.Near, FormatFlags = StringFormatFlags.NoWrap };
        using var centerFmt = new StringFormat { Alignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

        // Speeds stacked: arrows on the near side, numbers right-aligned so the digits line up (both languages).
        void Stack(float x0)
        {
            Art.Arrow(g, new RectangleF(X(x0, arrow), y1 + (lineH - arrow) / 2, arrow, arrow), true, upC);
            Art.Arrow(g, new RectangleF(X(x0, arrow), y2 + (lineH - arrow) / 2, arrow, arrow), false, downC);
            g.DrawString(c.Up, font, b, new RectangleF(X(x0 + arrow + arrowGap, speedW), y1, speedW, lineH), farFmt);
            g.DrawString(c.Down, font, b, new RectangleF(X(x0 + arrow + arrowGap, speedW), y2, speedW, lineH), farFmt);
        }
        void Dot(float x, float y, float d)
        {
            using var q = new SolidBrush(c.PingMs == null ? Color.FromArgb(232, 72, 85) : c.PingQuality);
            g.FillEllipse(q, x, y, d, d);
        }

        switch (layout)
        {
            case WidgetLayout.SpeedsPing:
            {
                Stack(pad);
                float x = pad + arrow + arrowGap + speedW + 12 * s;
                using (var sep = new Pen(Color.FromArgb(dark ? 50 : 40, fg), Math.Max(1, s)))
                {
                    float sx = X(x - 6 * s, 0);
                    g.DrawLine(sep, sx, 12 * s, sx, h - 12 * s);
                }
                g.DrawString(c.PingMs ?? "...", font, b, new RectangleF(X(x, pingW), y1, pingW, lineH), centerFmt);
                float d = 6 * s;
                var ms = g.MeasureString("ms", small);
                float mx = x + (pingW - (ms.Width + d + 3 * s)) / 2;
                Dot(X(mx, d), y2 + (lineH - d) / 2 + s, d);
                g.DrawString("ms", small, bDim, X(mx + d + 3 * s, ms.Width), y2 + (lineH - ms.Height) / 2 + s);
                break;
            }
            case WidgetLayout.OneLine:
            {
                float y = (h - lineH) / 2, x = pad, gap = 5 * s;
                Art.Arrow(g, new RectangleF(X(x, arrow), y + (lineH - arrow) / 2, arrow, arrow), false, downC);
                g.DrawString(c.Down, font, b, new RectangleF(X(x + arrow + gap, speedW), y, speedW, lineH), rtl ? farFmt : nearFmt);
                x += arrow + gap + speedW + 12 * s;
                Art.Arrow(g, new RectangleF(X(x, arrow), y + (lineH - arrow) / 2, arrow, arrow), true, upC);
                g.DrawString(c.Up, font, b, new RectangleF(X(x + arrow + gap, speedW), y, speedW, lineH), rtl ? farFmt : nearFmt);
                break;
            }
            case WidgetLayout.Totals:
            {
                float labelW = Wd(small, L.T("Today"), L.T("ThisMonth")), valueW = w - pad * 2 - labelW - 10 * s;
                float ly = (lineH - small.GetHeight(g)) / 2 + s;
                using var labelFmt = new StringFormat { Alignment = StringAlignment.Near, FormatFlags = StringFormatFlags.NoWrap | (rtl ? StringFormatFlags.DirectionRightToLeft : 0) };
                g.DrawString(L.T("Today"), small, bDim, new RectangleF(X(pad, labelW), y1 + ly, labelW, lineH), labelFmt);
                g.DrawString(L.T("ThisMonth"), small, bDim, new RectangleF(X(pad, labelW), y2 + ly, labelW, lineH), labelFmt);
                g.DrawString(c.Today, font, b, new RectangleF(X(pad + labelW + 10 * s, valueW), y1, valueW, lineH), farFmt);
                g.DrawString(c.Month, font, b, new RectangleF(X(pad + labelW + 10 * s, valueW), y2, valueW, lineH), farFmt);
                break;
            }
            case WidgetLayout.Ping:
            {
                float d = 7 * s, inner = w - pad * 2;
                string v = c.PingMs == null ? L.T("PingNoReply") : L.Ltr(c.PingMs + " ms"); // "18 ms" stays in order in Arabic
                float vw = g.MeasureString(v, font).Width;
                Dot(X(pad, d), y1 + (lineH - d) / 2, d);
                g.DrawString(v, font, b, new RectangleF(X(pad + d + 5 * s, inner - d - 5 * s), y1, inner - d - 5 * s, lineH),
                    new StringFormat { Alignment = StringAlignment.Near, FormatFlags = StringFormatFlags.NoWrap | (rtl ? StringFormatFlags.DirectionRightToLeft : 0) });
                g.DrawString(c.PingDetail, small, bDim, new RectangleF(X(pad, inner), y2 + 2 * s, inner, lineH),
                    new StringFormat { Alignment = StringAlignment.Near, FormatFlags = StringFormatFlags.NoWrap | (rtl ? StringFormatFlags.DirectionRightToLeft : 0) });
                break;
            }
            default:
                Stack(pad);
                break;
        }

        if (updating)
        {
            // Updating: a thin accent line just inside the bottom edge, filling as it downloads.
            float lh = 2f * s, inset = 7 * s;
            var track = new RectangleF(box.Left + inset, box.Bottom - lh - 1.5f * s, box.Width - inset * 2, lh);
            using (var tb = new SolidBrush(Color.FromArgb(dark ? 50 : 40, dark ? Color.White : Art.SeaDark)))
            using (var tp = Art.RoundRect(track, lh / 2)) g.FillPath(tb, tp);
            RectangleF fill;
            if (updateFraction is { } f) fill = new RectangleF(track.X, track.Y, Math.Max(lh, track.Width * (float)Math.Clamp(f, 0, 1)), lh);
            else
            {
                float seg = track.Width / 3, t = (1 - MathF.Cos(updatePhase * MathF.PI)) / 2;
                float x = track.X - seg + (track.Width + seg) * t;
                float l = Math.Max(track.X, x), r = Math.Min(track.Right, x + seg);
                fill = new RectangleF(l, track.Y, Math.Max(0, r - l), lh);
            }
            if (rtl) fill.X = track.Right - (fill.X - track.X) - fill.Width;
            if (fill.Width >= 1)
            {
                using var fb = new SolidBrush(downC);
                using var fp = Art.RoundRect(fill, lh / 2);
                g.FillPath(fb, fp);
            }
        }
        return bmp;
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_MOUSEACTIVATE:
                m.Result = 3; // MA_NOACTIVATE
                return;
            case WM_MOUSEMOVE when !hover:
                hover = true;
                HoverChanged?.Invoke(true);
                var tme = new TRACKMOUSEEVENT { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<TRACKMOUSEEVENT>(), dwFlags = 2 /* TME_LEAVE */, hwndTrack = Handle };
                TrackMouseEvent(ref tme);
                Render();
                break;
            case WM_MOUSELEAVE:
                hover = false;
                HoverChanged?.Invoke(false);
                Render();
                break;
            case WM_LBUTTONUP:
                Clicked?.Invoke();
                return;
            case WM_RBUTTONUP:
                RightClicked?.Invoke();
                return;
        }
        base.WndProc(ref m);
    }
}
