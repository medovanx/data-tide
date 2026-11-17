using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;

namespace DataTide.Configuration;

public sealed class AppSettings
{
    public const string AutoInterface = "auto";

    public string Language { get; set; } = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? "ar" : "en";

    /// <summary>Interface id shown in the widget, or "auto" to follow the default route.</summary>
    public string InterfaceId { get; set; } = AutoInterface;
    /// <summary>Show speeds in bits (Mb/s) instead of bytes (MB/s).</summary>
    public bool ShowBits { get; set; }

    /// <summary>"Right" = beside the tray, "Left" = left end of the taskbar.</summary>
    public string WidgetSide { get; set; } = "Right";
    /// <summary>Extra shift in pixels (at 100% scale), positive = towards the centre.</summary>
    public int WidgetOffset { get; set; }
    public WidgetLayout WidgetLayout { get; set; } = WidgetLayout.Speeds;
    public WidgetBackground WidgetBackground { get; set; } = WidgetBackground.Sea;
    /// <summary>Bars or lines for the report's charts.</summary>
    public ReportChart ReportChart { get; set; } = ReportChart.Bars;

    /// <summary>Log usage of every interface; logging never sends anything anywhere.</summary>
    public bool LogUsage { get; set; } = true;
    /// <summary>
    /// Also log virtual adapters (VPNs, Hyper-V and WSL switches, VirtualBox, VMware). Off by default: their traffic
    /// usually crosses a real adapter too, so it would be counted twice.
    /// </summary>
    public bool LogVirtual { get; set; }
    /// <summary>Days of usage history to keep (0 = forever).</summary>
    public int KeepDays { get; set; } = 365;

    public bool LimitsEnabled { get; set; }
    /// <summary>Show which programs use the network in the flyout (needs the elevated helper, so an admin prompt per session).</summary>
    public bool ShowPrograms { get; set; }
    public List<LimitRule> Limits { get; set; } = [];
    /// <summary>Unit the Limits page shows speeds in; limits are stored in bytes per second either way.</summary>
    public RateUnit LimitUnit { get; set; } = RateUnit.KBps;

    /// <summary>Game mode: while one of <see cref="Games"/> runs, every other program together is held to these speeds.</summary>
    public bool GameMode { get; set; }
    public List<string> Games { get; set; } = [];
    public long GameOthersDown { get; set; } = 512 * 1024;
    public long GameOthersUp { get; set; } = 128 * 1024;

    /// <summary>Cut these programs off while the connection is metered (a hotspot or mobile plan).</summary>
    public bool MeteredBlock { get; set; }
    public List<string> MeteredBlockList { get; set; } = [];

    /// <summary>Ping this host once a second for the flyout and hover card.</summary>
    public bool Ping { get; set; } = true;
    public string PingHost { get; set; } = "1.1.1.1";

    public bool AutoCheckUpdates { get; set; } = true;
    public bool AutoInstallUpdates { get; set; } = true;
    public DateTime LastUpdateCheckUtc { get; set; }

    /// <summary>The last finished speed test, reused by Detect in the download-time calculator.</summary>
    public SpeedTestResult? LastSpeedTest { get; set; }

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.Settings))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.Settings)) ?? new();
        }
        catch { /* corrupt file: fall back to defaults */ }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(AppPaths.Data);
        var tmp = AppPaths.Settings + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, AppPaths.Settings, true);
    }

    public AppSettings Clone() => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;

    // Autostart lives in the registry, not the settings file.
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "DataTide";

    public static bool StartWithWindows
    {
        get
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey);
            return k?.GetValue(RunName) is string;
        }
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) k.SetValue(RunName, $"\"{Environment.ProcessPath}\"");
            else k.DeleteValue(RunName, false);
        }
    }
}
