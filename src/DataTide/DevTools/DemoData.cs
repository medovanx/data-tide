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

    /// <summary>Made-up daily data per program for the report's By program table.</summary>
    public static ProgramLedger ProgramLog(DateOnly today, int days = 30)
    {
        var l = new ProgramLedger();
        var rnd = new Random(11);
        (string name, string path, int down, int up)[] apps =
        [
            ("Example Launcher", @"C:\Program Files\Example Launcher\launcher.exe", 2600, 60),
            ("Example Browser", @"C:\Program Files\Example Browser\browser.exe", 900, 70),
            ("Video Player", @"C:\Program Files\Video Player\player.exe", 700, 10),
            ("Cloud Sync", @"C:\Program Files\Cloud Sync\sync.exe", 120, 260),
            ("Chat", @"C:\Program Files\Chat\chat.exe", 60, 40),
            ("Mail", @"C:\Program Files\Mail\mail.exe", 25, 8),
            ("Music", @"C:\Program Files\Music\music.exe", 40, 2),
            ("Windows Update", @"C:\Windows\System32\svchost.exe", 300, 4),
        ];
        for (int d = days - 1; d >= 0; d--)
        {
            var day = today.AddDays(-d);
            foreach (var (name, path, down, up) in apps)
            {
                if (rnd.Next(4) == 0) continue;
                double k = rnd.Next(30, 170) / 100.0 * 1024 * 1024;
                l.Add(day, name, path, down * k, up * k);
            }
        }
        return l;
    }

    /// <summary>Made-up programs for the flyout's Programs section.</summary>
    public static List<ProgramUsage> Programs() =>
    [
        new(@"C:\Program Files\Example Launcher\launcher.exe", "Example Launcher", 5.2 * 1024 * 1024, 64 * 1024, 2_400_000_000),
        new(@"C:\Program Files\Example Browser\browser.exe", "Example Browser", 1.1 * 1024 * 1024, 120 * 1024, 860_000_000),
        new(@"C:\Program Files\Cloud Sync\sync.exe", "Cloud Sync", 12 * 1024, 410 * 1024, 310_000_000),
        new(@"C:\Program Files\Chat\chat.exe", "Chat", 34 * 1024, 18 * 1024, 95_000_000),
    ];

    public static AppSettings Settings(string lang) => new()
    {
        ShowPrograms = true,
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
