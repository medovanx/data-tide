namespace DataTide.DevTools;

/// <summary>Made-up usage for screenshots and visual checks, so no real interface, program or traffic of the user ever appears.</summary>
static class DemoData
{
    public static readonly string[] Interfaces = ["Ethernet", "Wi-Fi", "Phone hotspot"];

    public static UsageLedger Ledger(DateOnly today, int days = 30)
    {
        var l = new UsageLedger();
        var rnd = new Random(7);
        for (int d = days - 1; d >= 0; d--)
        {
            var day = today.AddDays(-d);
            bool weekend = day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
            for (int h = 0; h < 24; h++)
            {
                var at = day.ToDateTime(new TimeOnly(h, 0));
                double evening = h is >= 18 and <= 23 ? 2.4 : h is >= 1 and <= 7 ? 0.15 : 1;
                long eth = (long)(rnd.Next(40, 260) * 1024L * 1024 * evening * (weekend ? 1.6 : 1));
                l.Add(at, "demo-eth", Interfaces[0], eth, eth / rnd.Next(8, 16));
                if (h is >= 8 and <= 17 && !weekend)
                {
                    long wifi = rnd.Next(5, 60) * 1024L * 1024;
                    l.Add(at, "demo-wifi", Interfaces[1], wifi, wifi / 6);
                }
                if (d % 9 == 3 && h is >= 12 and <= 15)
                {
                    long mob = rnd.Next(20, 90) * 1024L * 1024;
                    l.Add(at, "demo-mobile", Interfaces[2], mob, mob / 5);
                }
            }
        }
        return l;
    }

    public static AppSettings Settings(string lang) => new()
    {
        Language = lang,
        InterfaceId = AppSettings.AutoInterface,
        LimitsEnabled = true,
        Limits =
        [
            new LimitRule { ExePath = @"C:\Program Files\Example Launcher\launcher.exe", DownBytesPerSecond = 2048 * 1024, UpBytesPerSecond = 512 * 1024 },
            new LimitRule { ExePath = @"C:\Program Files\Cloud Sync\sync.exe", DownBytesPerSecond = 0, UpBytesPerSecond = 256 * 1024 },
            new LimitRule { ExePath = @"C:\Tools\Downloader\downloader.exe", DownBytesPerSecond = 4096 * 1024, UpBytesPerSecond = 0, Enabled = false },
        ],
    };

    /// <summary>A minute of plausible speeds (a download ramping up while a video call uploads).</summary>
    public static IEnumerable<(double down, double up)> Minute()
    {
        var rnd = new Random(3);
        for (int i = 0; i < 60; i++)
        {
            double down = (i < 20 ? 0.4 + i * 0.25 : 5.5 + Math.Sin(i / 4.0) * 1.2) * 1024 * 1024 + rnd.Next(0, 400_000);
            double up = (0.35 + Math.Sin(i / 7.0) * 0.08) * 1024 * 1024 + rnd.Next(0, 60_000);
            yield return (down, up);
        }
    }
}
