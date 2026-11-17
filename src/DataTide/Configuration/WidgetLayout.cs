namespace DataTide.Configuration;

/// <summary>What the taskbar widget shows.</summary>
public enum WidgetLayout
{
    /// <summary>Upload over download.</summary>
    Speeds,
    /// <summary>Upload over download, with the ping beside them.</summary>
    SpeedsPing,
    /// <summary>Download and upload side by side on one line.</summary>
    OneLine,
    /// <summary>Data used today over this month's total.</summary>
    Totals,
    /// <summary>Ping over jitter and loss.</summary>
    Ping,
}

/// <summary>The pill behind the widget's text.</summary>
public enum WidgetBackground
{
    /// <summary>Data Tide's sea blue.</summary>
    Sea,
    /// <summary>The Windows accent colour.</summary>
    Accent,
    /// <summary>No pill, just the text on the taskbar.</summary>
    None,
}
