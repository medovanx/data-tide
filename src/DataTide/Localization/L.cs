using System.Globalization;

namespace DataTide.Localization;

/// <summary>English / Arabic UI text and date formatting.</summary>
public static class L
{
    public static bool Ar { get; set; }

    static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-GB");
    static readonly CultureInfo Arabic = CreateArabic();

    /// <summary>Arabic month and day names on the Gregorian calendar, with Latin digits.</summary>
    static CultureInfo CreateArabic()
    {
        var c = (CultureInfo)CultureInfo.GetCultureInfo("ar-EG").Clone();
        c.DateTimeFormat.Calendar = new GregorianCalendar();
        return c;
    }

    public static CultureInfo Culture => Ar ? Arabic : En;

    static readonly Dictionary<string, (string en, string ar)> D = new()
    {
        ["AppName"] = ("Data Tide", "داتا تايد"),
        ["Download"] = ("Download", "التنزيل"),
        ["Upload"] = ("Upload", "الرفع"),
        ["Total"] = ("Total", "الإجمالي"),
        ["Peak"] = ("Peak", "الذروة"),
        ["Auto"] = ("Auto", "تلقائي"),
        ["NoInterface"] = ("No connection", "لا يوجد اتصال"),
        ["ThisMonth"] = ("This month", "هذا الشهر"),
        ["Today"] = ("Today", "اليوم"),
        ["TodayUsage"] = ("Used today", "استهلاك اليوم"),
        ["NothingYet"] = ("Nothing logged yet today.", "لم يُسجَّل أي استهلاك اليوم بعد."),
        ["LoggingOff"] = ("Usage logging is off.", "تسجيل الاستهلاك متوقف."),
        ["Report"] = ("Report", "التقرير"),
        ["Limits"] = ("Limits", "الحدود"),
        ["Settings"] = ("Settings", "الإعدادات"),
        ["LimitsOff"] = ("Speed limits are off", "حدود السرعة متوقفة"),
        ["LimitsOn"] = ("Speed limits on for {0} programs", "حدود السرعة مفعّلة لعدد {0} من البرامج"),
        ["LimitsPaused"] = ("Speed limits are paused (helper not running)", "حدود السرعة معلّقة (المساعد لا يعمل)"),

        ["ShowTraffic"] = ("Show traffic", "عرض حركة البيانات"),
        ["UsageReport"] = ("Usage report", "تقرير الاستهلاك"),
        ["SpeedLimits"] = ("Speed limits", "حدود السرعة"),
        ["Interface"] = ("Interface", "واجهة الشبكة"),
        ["AutoInterface"] = ("Auto (follow the active connection)", "تلقائي (يتبع الاتصال النشط)"),
        ["CheckForUpdates"] = ("Check for updates", "التحقق من التحديثات"),
        ["StartWithWindows"] = ("Start with Windows", "التشغيل مع ويندوز"),
        ["Exit"] = ("Exit", "خروج"),

        ["General"] = ("General", "عام"),
        ["Language"] = ("Language", "اللغة"),
        ["Units"] = ("Speed units", "وحدة السرعة"),
        ["UnitsBytes"] = ("Bytes (MB/s)", "بايت (MB/s)"),
        ["UnitsBits"] = ("Bits (Mb/s)", "بت (Mb/s)"),
        ["WidgetPosition"] = ("Taskbar position", "الموضع في شريط المهام"),
        ["WidgetOffset"] = ("Shift (pixels)", "الإزاحة (بكسل)"),
        ["SideRight"] = ("Right, next to the tray", "يمين، بجوار منطقة الإشعارات"),
        ["SideLeft"] = ("Left side", "الجانب الأيسر"),
        ["Save"] = ("Save", "حفظ"),
        ["Cancel"] = ("Cancel", "إلغاء"),

        ["Network"] = ("Network", "الشبكة"),
        ["WidgetInterface"] = ("Shown in the taskbar", "المعروض في شريط المهام"),
        ["AutoInterfaceNote"] = ("Auto shows the interface Windows uses to reach the internet, and switches by itself when that changes (Wi-Fi to Ethernet, a VPN...).",
                                 "الوضع التلقائي يعرض الواجهة التي يستخدمها ويندوز للوصول إلى الإنترنت، ويتبدل وحده عند تغيرها (من Wi-Fi إلى Ethernet أو عند تشغيل VPN...)."),
        ["Disconnected"] = ("disconnected", "غير متصل"),
        ["UsageLog"] = ("Usage log", "سجل الاستهلاك"),
        ["LogUsage"] = ("Log data usage", "تسجيل استهلاك البيانات"),
        ["LogVirtual"] = ("Include virtual adapters", "تضمين المحولات الافتراضية"),
        ["LogVirtualNote"] = ("VPNs (Radmin, WireGuard...), Hyper-V and WSL switches and virtual machine adapters. Their traffic usually also crosses your real adapter, so including them counts it twice.",
                              "مثل شبكات VPN (Radmin وWireGuard...) ومحولات Hyper-V وWSL والأجهزة الافتراضية. حركتها تمر عادةً عبر المحول الحقيقي أيضًا، لذا فإن تضمينها يحسبها مرتين."),
        ["KeepHistory"] = ("Keep history for", "الاحتفاظ بالسجل لمدة"),
        ["Keep7"] = ("1 week", "أسبوع"),
        ["Keep30"] = ("1 month", "شهر"),
        ["Keep90"] = ("3 months", "3 أشهر"),
        ["Keep180"] = ("6 months", "6 أشهر"),
        ["Keep365"] = ("1 year", "سنة"),
        ["Keep730"] = ("2 years", "سنتان"),
        ["Keep1825"] = ("5 years", "5 سنوات"),
        ["KeepForever"] = ("Forever", "دائمًا"),
        ["KeepCustom"] = ("Custom...", "مدة مخصصة..."),
        ["KeepDays"] = ("Days to keep", "عدد الأيام"),
        ["LogNote"] = ("Every interface is logged separately, by the hour. The log stays on this PC in usage.csv.",
                       "تُسجَّل كل واجهة على حدة، ساعة بساعة. يبقى السجل على هذا الجهاز في الملف usage.csv."),
        ["OpenDataFolder"] = ("Open data folder", "فتح مجلد البيانات"),
        ["ClearHistory"] = ("Clear history", "مسح السجل"),
        ["ClearConfirm"] = ("Delete all logged usage? This cannot be undone.", "حذف كل الاستهلاك المسجَّل؟ لا يمكن التراجع عن ذلك."),

        ["Driver"] = ("Packet filter driver", "برنامج تشغيل ترشيح الحزم"),
        ["DriverInstalled"] = ("Windows Packet Filter is installed.", "برنامج Windows Packet Filter مثبَّت."),
        ["DriverMissing"] = ("Install Windows Packet Filter to use speed limits.", "ثبّت Windows Packet Filter لاستخدام حدود السرعة."),
        ["DriverMissingLong"] = ("Speed limits need the free Windows Packet Filter driver by NT Kernel. Windows has no built-in way to limit an app's download speed.",
                                 "تحتاج حدود السرعة إلى برنامج التشغيل المجاني Windows Packet Filter من NT Kernel، إذ لا يوفر ويندوز طريقة مدمجة للحد من سرعة تنزيل برنامج."),
        ["LimitsNote"] = ("Limiting runs in a small helper that asks for administrator rights once per session. Only programs you add here are slowed; everything else passes straight through.",
                          "يعمل تحديد السرعة في برنامج مساعد صغير يطلب صلاحيات المسؤول مرة واحدة في كل جلسة. لا يُبطَّأ إلا البرامج التي تضيفها هنا، وكل ما عداها يمر كما هو."),
        ["GetDriver"] = ("Get the driver", "تنزيل برنامج التشغيل"),
        ["EnableLimits"] = ("Turn on speed limits", "تفعيل حدود السرعة"),
        ["AddProgram"] = ("Add a program", "إضافة برنامج"),
        ["Add"] = ("Add", "إضافة"),
        ["Browse"] = ("Browse...", "استعراض..."),
        ["Programs"] = ("Programs", "البرامج"),
        ["Remove"] = ("Remove", "إزالة"),
        ["DownLimitTip"] = ("Download limit in KB/s (0 = no limit)", "حد التنزيل بالكيلوبايت/ث (0 = بلا حد)"),
        ["UpLimitTip"] = ("Upload limit in KB/s (0 = no limit)", "حد الرفع بالكيلوبايت/ث (0 = بلا حد)"),
        ["NoRules"] = ("No programs yet. Add one below.", "لا توجد برامج بعد. أضف برنامجًا بالأسفل."),
        ["LimitsSaveNote"] = ("Limits are in KB/s; 0 means no limit. Save to apply them.", "الحدود بالكيلوبايت/ث، و0 يعني بلا حد. احفظ لتطبيقها."),
        ["LimitsOffNote"] = ("Turn on speed limits and save to apply them.", "فعّل حدود السرعة واحفظ لتطبيقها."),
        ["LiveByProgram"] = ("Live by program", "مباشر حسب البرنامج"),
        ["LiveOff"] = ("Shown while speed limits are on.", "يظهر أثناء تفعيل حدود السرعة."),
        ["LiveNote"] = ("Busiest programs right now.", "أكثر البرامج استخدامًا للشبكة الآن."),
        ["LimiterDeclined"] = ("Speed limits need administrator rights, and the request was declined.", "تحتاج حدود السرعة إلى صلاحيات المسؤول، وقد رُفض الطلب."),
        ["LimiterUnreachable"] = ("The speed limit helper is not responding.", "المساعد الخاص بحدود السرعة لا يستجيب."),

        ["Updates"] = ("Updates", "التحديثات"),
        ["AutoCheck"] = ("Check for updates automatically", "التحقق من التحديثات تلقائيًا"),
        ["AutoInstall"] = ("Install updates automatically", "تثبيت التحديثات تلقائيًا"),
        ["CheckNow"] = ("Check now", "تحقق الآن"),
        ["Version"] = ("Version {0}", "الإصدار {0}"),
        ["UpToDate"] = ("You're on the latest version ({0}).", "لديك أحدث إصدار ({0})."),
        ["UpdateAvailable"] = ("Version {0} is available.", "الإصدار {0} متاح."),
        ["Updating"] = ("Updating to {0}...", "جارٍ التحديث إلى {0}..."),
        ["UpdateFailed"] = ("Couldn't install the update. Opening the download page.", "تعذر تثبيت التحديث. سيتم فتح صفحة التنزيل."),
        ["UpdateCheckFailed"] = ("Couldn't check for updates.", "تعذر التحقق من التحديثات."),
        ["Updated"] = ("Updated to version {0}.", "تم التحديث إلى الإصدار {0}."),

        ["Range"] = ("Range", "الفترة"),
        ["RangeToday"] = ("Today", "اليوم"),
        ["RangeLast7Days"] = ("Last 7 days", "آخر 7 أيام"),
        ["RangeLast30Days"] = ("Last 30 days", "آخر 30 يومًا"),
        ["RangeThisMonth"] = ("This month", "هذا الشهر"),
        ["RangeLastMonth"] = ("Last month", "الشهر الماضي"),
        ["RangeAllTime"] = ("All time", "كل الفترات"),
        ["AllInterfaces"] = ("All interfaces", "كل الواجهات"),
        ["ExportPng"] = ("Save PNG", "حفظ PNG"),
        ["ExportPdf"] = ("Save PDF", "حفظ PDF"),
        ["Exported"] = ("Saved {0}. Open it now?", "تم حفظ {0}. هل تريد فتحه الآن؟"),
        ["ExportFailed"] = ("Couldn't save the report.", "تعذر حفظ التقرير."),
        ["ReportTitle"] = ("Data usage report", "تقرير استهلاك البيانات"),
        ["RangeText"] = ("{0} to {1}", "من {0} إلى {1}"),
        ["TotalUsage"] = ("Total used", "إجمالي الاستهلاك"),
        ["DailyAverage"] = ("Daily average", "المتوسط اليومي"),
        ["UsageOverTime"] = ("Usage over time", "الاستهلاك عبر الوقت"),
        ["ByInterface"] = ("By interface", "حسب الواجهة"),
        ["DailyUsage"] = ("Day by day", "يومًا بيوم"),
        ["Date"] = ("Date", "التاريخ"),
        ["Share"] = ("Share", "النسبة"),
        ["DayTotal"] = ("Day total", "إجمالي اليوم"),
        ["NoData"] = ("No usage logged in this range.", "لا يوجد استهلاك مسجَّل في هذه الفترة."),
        ["GeneratedBy"] = ("Made with Data Tide {0} on {1} at {2}", "أُنشئ بواسطة Data Tide {0} في {1} الساعة {2}"),
        ["PageOf"] = ("Page {0} of {1}", "صفحة {0} من {1}"),
    };

    public static string T(string key) => D.TryGetValue(key, out var v) ? (Ar ? v.ar : v.en) : key;

    public static string F(string key, params object[] args) => string.Format(Culture, T(key), args);

    /// <summary>"16 November 2026" / "16 نوفمبر 2026".</summary>
    public static string LongDate(DateOnly d) => d.ToDateTime(TimeOnly.MinValue).ToString("d MMMM yyyy", Culture);

    /// <summary>"Mon 16 Nov 2026" for table rows.</summary>
    public static string MediumDate(DateOnly d) => d.ToDateTime(TimeOnly.MinValue).ToString(Ar ? "dddd d MMMM" : "ddd d MMM yyyy", Culture);

    /// <summary>Axis label under a day's bar: the weekday for a week, the day number for longer ranges.</summary>
    public static string ShortDay(DateOnly d, int daysInRange) =>
        d.ToDateTime(TimeOnly.MinValue).ToString(daysInRange <= 7 ? "ddd d" : "d/M", Culture);

    public static string MonthLabel(DateOnly d) => d.ToDateTime(TimeOnly.MinValue).ToString("MMM yyyy", Culture);
}
