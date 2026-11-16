using System.Globalization;

namespace DataTide.Core.Formatting;

/// <summary>Human-readable sizes and speeds, in binary units (1 KB = 1024 bytes) like Task Manager.</summary>
public static class ByteFormat
{
    static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>"0 B", "512 B", "1.5 KB", "12.3 MB", "1.25 GB".</summary>
    public static string Size(long bytes)
    {
        if (bytes < 0) bytes = 0;
        double v = bytes;
        int u = 0;
        while (v >= 1024 && u < Units.Length - 1) { v /= 1024; u++; }
        return u == 0 ? $"{bytes.ToString(Inv)} B" : $"{Number(v)} {Units[u]}";
    }

    /// <summary>Speed in bytes per second ("1.2 MB/s"), or in bits when <paramref name="bits"/> is set ("9.6 Mb/s").</summary>
    public static string Rate(double bytesPerSecond, bool bits = false)
    {
        if (double.IsNaN(bytesPerSecond) || bytesPerSecond < 0) bytesPerSecond = 0;
        if (bits)
        {
            double b = bytesPerSecond * 8;
            string[] bu = ["b/s", "Kb/s", "Mb/s", "Gb/s", "Tb/s"];
            int k = 0;
            while (b >= 1000 && k < bu.Length - 1) { b /= 1000; k++; }
            return $"{(k == 0 ? Math.Round(b).ToString(Inv) : Number(b))} {bu[k]}";
        }
        double v = bytesPerSecond;
        int u = 0;
        while (v >= 1024 && u < Units.Length - 1) { v /= 1024; u++; }
        return $"{(u == 0 ? Math.Round(v).ToString(Inv) : Number(v))} {Units[u]}/s";
    }

    /// <summary>Three significant digits at most: 1.25, 12.5, 125.</summary>
    static string Number(double v) => v switch
    {
        < 10 => v.ToString("0.##", Inv),
        < 100 => v.ToString("0.#", Inv),
        _ => Math.Round(v).ToString(Inv),
    };

    /// <summary>Parses "500", "500K", "1.5M", "2 MB/s" into bytes per second; null when unreadable.</summary>
    public static long? ParseRate(string text)
    {
        var t = text.Trim().ToUpperInvariant().Replace("/S", "").Replace("B", "").Trim();
        if (t.Length == 0) return null;
        long mul = 1;
        char last = t[^1];
        if (last is 'K' or 'M' or 'G')
        {
            mul = last switch { 'K' => 1024, 'M' => 1024 * 1024, _ => 1024L * 1024 * 1024 };
            t = t[..^1].Trim();
        }
        return double.TryParse(t, NumberStyles.Float, Inv, out var v) && v >= 0 ? (long)Math.Round(v * mul) : null;
    }
}
