using System.Globalization;

namespace DataTide.Core.SpeedTest;

/// <summary>How long a download of a given size takes at a given speed.</summary>
public static class DownloadTime
{
    public static double Bytes(double size, SizeUnit unit) => size * unit switch
    {
        SizeUnit.MB => 1024d * 1024,
        SizeUnit.GB => 1024d * 1024 * 1024,
        _ => 1024d * 1024 * 1024 * 1024,
    };

    public static double BytesPerSecond(double speed, SpeedUnit unit) => unit == SpeedUnit.Mbps ? speed * 1_000_000 / 8 : speed * 1024 * 1024;

    /// <summary>The time, or null when the speed is zero or anything is negative.</summary>
    public static TimeSpan? Estimate(double size, SizeUnit sizeUnit, double speed, SpeedUnit speedUnit)
    {
        double bps = BytesPerSecond(speed, speedUnit), bytes = Bytes(size, sizeUnit);
        if (bps <= 0 || bytes < 0 || double.IsNaN(bps) || double.IsNaN(bytes)) return null;
        double seconds = bytes / bps;
        return seconds > TimeSpan.MaxValue.TotalSeconds / 2 ? TimeSpan.MaxValue : TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Compact duration in the two largest units: "45 s", "12 min 30 s", "3 h 05 min", "2 d 4 h".
    /// <paramref name="units"/> gives the day, hour, minute and second labels (localised by the caller).
    /// </summary>
    public static string Format(TimeSpan t, (string d, string h, string m, string s) units)
    {
        var inv = CultureInfo.InvariantCulture;
        if (t.TotalSeconds < 1) return $"< 1 {units.s}";
        // Round up to whole seconds: an estimate that says 5 min 43 s for 343.6 s finishes late.
        t = TimeSpan.FromSeconds(Math.Ceiling(t.TotalSeconds));
        if (t.TotalMinutes < 1) return $"{Math.Ceiling(t.TotalSeconds).ToString(inv)} {units.s}";
        if (t.TotalHours < 1) return $"{t.Minutes.ToString(inv)} {units.m} {t.Seconds.ToString("00", inv)} {units.s}";
        if (t.TotalDays < 1) return $"{t.Hours.ToString(inv)} {units.h} {t.Minutes.ToString("00", inv)} {units.m}";
        return $"{((long)t.TotalDays).ToString(inv)} {units.d} {t.Hours.ToString(inv)} {units.h}";
    }
}
