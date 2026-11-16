namespace DataTide.Core.Formatting;

/// <summary>Units a speed limit can be typed in. Limits are always stored in bytes per second.</summary>
public enum RateUnit { KBps, MBps, Mbps }

public static class RateUnits
{
    public static readonly RateUnit[] All = [RateUnit.KBps, RateUnit.MBps, RateUnit.Mbps];

    public static string Label(RateUnit u) => u switch { RateUnit.KBps => "KB/s", RateUnit.MBps => "MB/s", _ => "Mb/s" };

    /// <summary>Bytes per second in one of the unit (1 KB = 1024 B, 1 MB = 1024 KB, 1 Mb = 1,000,000 bits).</summary>
    public static double Bytes(RateUnit u) => u switch { RateUnit.KBps => 1024, RateUnit.MBps => 1024 * 1024, _ => 125_000 };

    public static long ToBytes(decimal value, RateUnit u) => (long)Math.Round((double)value * Bytes(u));

    public static decimal FromBytes(long bytesPerSecond, RateUnit u) => Math.Round((decimal)(bytesPerSecond / Bytes(u)), 2);

    /// <summary>The unit an existing limit reads best in: MB/s for whole or half megabytes from 1 MB/s up, else KB/s.</summary>
    public static RateUnit Natural(long bytesPerSecond) =>
        bytesPerSecond >= 1024 * 1024 && bytesPerSecond % (512 * 1024) == 0 ? RateUnit.MBps : RateUnit.KBps;
}
