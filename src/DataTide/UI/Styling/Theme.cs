namespace DataTide.UI.Styling;

/// <summary>Colours shared by the flyout, report and settings window.</summary>
sealed record Theme(bool Dark)
{
    public static Theme Current => new(Native.SystemDark());
    public static Theme Light => new(false);
    public Color Window => Dark ? Color.FromArgb(28, 31, 34) : Color.FromArgb(243, 245, 247);
    public Color Surface => Dark ? Color.FromArgb(38, 42, 46) : Color.White;
    public Color Input => Dark ? Color.FromArgb(50, 55, 60) : Color.FromArgb(246, 247, 249);
    public Color Border => Dark ? Color.FromArgb(60, 66, 72) : Color.FromArgb(220, 224, 229);
    public Color Text => Dark ? Color.FromArgb(238, 240, 242) : Color.FromArgb(22, 26, 30);
    public Color Dim => Dark ? Color.FromArgb(156, 164, 172) : Color.FromArgb(100, 108, 116);
    public Color Accent => Dark ? Color.FromArgb(88, 190, 240) : Color.FromArgb(3, 105, 161);
    public Color AccentFill => Color.FromArgb(8, 112, 168);
    public Color AccentHover => Color.FromArgb(14, 132, 190);
    /// <summary>Download is cyan, upload is amber, everywhere in the app.</summary>
    public Color Down => Dark ? Color.FromArgb(56, 189, 248) : Color.FromArgb(2, 132, 199);
    public Color Up => Dark ? Color.FromArgb(251, 191, 36) : Color.FromArgb(217, 119, 6);
}
