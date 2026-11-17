using Microsoft.Win32;

namespace DataTide;

sealed class DataTideApp : ApplicationContext
{
    public AppSettings S { get; private set; }
    public Icon AppIcon { get; }
    public UsageLedger Ledger { get; }
    /// <summary>Data per program per day, filled while the Programs list is on.</summary>
    public ProgramLedger ProgramLog { get; }
    public NetworkMonitor Monitor { get; } = new();
    public SpeedHistory History { get; } = new(60);

    /// <summary>Current speeds of the interface shown in the widget.</summary>
    public double Down { get; private set; }
    public double Up { get; private set; }
    /// <summary>The interface the widget shows right now (resolved from "Auto").</summary>
    public string? ShownInterfaceId { get; private set; }
    public string ShownInterfaceName { get; private set; } = "";
    /// <summary>Latest speeds of every interface, for the flyout.</summary>
    public IReadOnlyList<InterfaceSample> LastSamples { get; private set; } = [];

    readonly InterfaceSampler sampler = new();
    readonly HostWindow host;
    readonly TaskbarWidget widget = new();
    readonly ContextMenuStrip menu = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 45_000 };
    TrafficFlyout? flyout;
    readonly HoverTip hoverTip = new();
    SettingsForm? settingsForm;
    ReportForm? reportForm;
    DateTime lastSave = DateTime.Now, lastPrune = DateTime.MinValue;
    int ticks;

    /// <summary>Headless instance for --shots: no tray, widget, timers or saving.</summary>
    public bool Headless { get; }

    public event Action? Ticked;

    /// <summary>Bytes received and sent today and this month on the logged interfaces (refreshed every 10 s).</summary>
    public (long rx, long tx) TodayTotal { get; private set; }
    public (long rx, long tx) MonthTotal { get; private set; }

    public void RefreshTotals()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var month = Ledger.Days(new DateOnly(today.Year, today.Month, 1), today)
            .Where(d => S.LogVirtual || !Monitor.IsVirtual(d.InterfaceId)).ToList();
        MonthTotal = (month.Sum(d => d.Received), month.Sum(d => d.Sent));
        var day = month.Where(d => d.Date == today).ToList();
        TodayTotal = (day.Sum(d => d.Received), day.Sum(d => d.Sent));
    }

    public DataTideApp(AppSettings? settings = null, UsageLedger? ledger = null, ProgramLedger? programLog = null)
    {
        Headless = settings != null;
        S = settings ?? AppSettings.Load();
        L.Ar = S.Language == "ar";
        AppIcon = LoadIcon();
        Ledger = ledger ?? UsageStore.Load();
        ProgramLog = programLog ?? (settings != null ? new ProgramLedger() : UsageStore.LoadPrograms());
        // Filter bindings logged by 1.0.0 builds before they were recognised: they only repeat a real adapter.
        if (ledger == null && Ledger.Remove(Monitor.FilterIds) > 0) UsageStore.Save(Ledger);

        host = new HostWindow(this);
        menu.Opening += (_, e) => { BuildMenu(); e.Cancel = false; };
        // No tray icon: the widget is the app's surface (click, hover, right-click), messages go out as Windows notifications.
        if (Headless) return;
        Toasts.Register();

        widget.Clicked += () => { hoverTip.Cancel(); ToggleFlyout(); };
        widget.HoverChanged += over =>
        {
            if (!over) { hoverTip.Cancel(); return; }
            if (flyout?.Visible == true) return;
            RefreshTotals();
            hoverTip.ShowSoon(this, widget.ScreenBounds, () => flyout?.Visible == true);
        };
        widget.RightClicked += () =>
        {
            Native.SetForegroundWindow(host.Handle);
            menu.Show(Cursor.Position);
        };

        SystemEvents.PowerModeChanged += (_, e) => { if (e.Mode == PowerModes.Resume) host.BeginInvoke(sampler.Reset); };
        SystemEvents.DisplaySettingsChanged += (_, _) => host.BeginInvoke(() => { widget.Invalidate(); widget.Ensure(); });
        SystemEvents.UserPreferenceChanged += (_, _) => host.BeginInvoke(widget.Invalidate);
        SystemEvents.SessionEnding += (_, _) => SaveLogs();

        widget.SetPlacement(S.WidgetSide == "Left", S.WidgetOffset);
        widget.Ensure();
        Sample();
        UpdateWidget();

        timer.Tick += (_, _) => Tick();
        timer.Start();

        Updater.CleanupOld();
        if (Environment.GetCommandLineArgs().Contains("--updated"))
            Toasts.Show(L.T("AppName"), L.F("Updated", Updater.CurrentText));
        updateTimer.Tick += async (_, _) => { updateTimer.Interval = 6 * 3600 * 1000; await CheckForUpdatesAsync(false); };
        updateTimer.Start(); // first check 45 s after start, then every 6 hours

        if (S.LimitsEnabled && S.Limits.Any(r => r.Enabled) && LimiterClient.DriverInstalled)
            _ = StartLimitsAsync(userInitiated: false);
        if (S.ShowPrograms) _ = StartProgramsAsync();
    }

    /// <summary>Writes the usage and per-program logs when they changed.</summary>
    public void SaveLogs()
    {
        if (Headless) return;
        if (Ledger.Dirty) UsageStore.Save(Ledger);
        if (ProgramLog.Dirty) UsageStore.Save(ProgramLog);
    }

    static Icon LoadIcon()
    {
        using var stream = typeof(DataTideApp).Assembly.GetManifestResourceStream("app.ico");
        return stream != null ? new Icon(stream) : SystemIcons.Application;
    }

    void Tick()
    {
        Sample();
        if (++ticks % 2 == 0) widget.Ensure(); // handles Explorer restarts and tray width changes
        UpdateWidget();
        var now = DateTime.Now;
        if ((now - lastSave).TotalSeconds >= 60 && (Ledger.Dirty || ProgramLog.Dirty)) { lastSave = now; SaveLogs(); }
        if (S.KeepDays > 0 && (now - lastPrune).TotalHours >= 12)
        {
            lastPrune = now;
            Ledger.Prune(now.Date.AddDays(-S.KeepDays));
            ProgramLog.Prune(DateOnly.FromDateTime(now.Date.AddDays(-S.KeepDays)));
        }
        if (ticks % 10 == 1) RefreshTotals();
        if (S.ShowPrograms && ticks % 2 == 0) _ = PollProgramsAsync();
        Ticked?.Invoke();
    }

    void Sample()
    {
        var now = DateTime.Now;
        var samples = sampler.Sample(Monitor.Read(), now);
        LastSamples = samples;
        if (S.LogUsage)
            foreach (var x in samples)
                if (S.LogVirtual || !Monitor.IsVirtual(x.InterfaceId))
                    Ledger.Add(now, x.InterfaceId, x.Name, x.ReceivedDelta, x.SentDelta);

        string? id = S.InterfaceId == AppSettings.AutoInterface ? Monitor.DefaultInterfaceId() : S.InterfaceId;
        var shown = samples.FirstOrDefault(x => x.InterfaceId == id);
        if (id != ShownInterfaceId) History.Clear();
        ShownInterfaceId = id;
        ShownInterfaceName = shown?.Name ?? (id != null ? Ledger.NameOf(id) : L.T("NoInterface"));
        Down = shown?.DownBytesPerSecond ?? 0;
        Up = shown?.UpBytesPerSecond ?? 0;
        if (shown != null) History.Add(Down, Up);
    }

    /// <summary>Fills the live numbers with made-up values for screenshots.</summary>
    public void Demo(string interfaceName, IEnumerable<(double down, double up)> minute)
    {
        Programs = DemoData.Programs();
        History.Clear();
        foreach (var (d, u) in minute) { History.Add(d, u); Down = d; Up = u; }
        ShownInterfaceId = "demo-eth";
        ShownInterfaceName = interfaceName;
    }

    public string Rate(double bytesPerSecond) => ByteFormat.Rate(bytesPerSecond, S.ShowBits);

    void UpdateWidget()
    {
        bool limited = S.LimitsEnabled && LimiterClient.HelperRunning;
        widget.SetContent(Rate(Up), Rate(Down), L.Ar, limited);
    }

    // ---------- windows ----------

    void ToggleFlyout()
    {
        if (flyout?.Visible == true) { flyout.Hide(); return; }
        // A click on the widget first deactivates (hides) the flyout; don't reopen it on that same click.
        if (flyout != null && (DateTime.Now - flyout.LastHidden).TotalMilliseconds < 300) return;
        ShowFlyout();
    }

    void ShowFlyout()
    {
        flyout ??= new TrafficFlyout(this);
        var anchor = widget.ScreenBounds ?? new Rectangle(Cursor.Position, new Size(1, 1));
        flyout.ShowNear(anchor);
    }

    public void ShowSettings(int page = 0)
    {
        flyout?.Hide();
        if (settingsForm is { IsDisposed: false }) { settingsForm.ShowPage(page); settingsForm.Activate(); return; }
        settingsForm = new SettingsForm(this);
        settingsForm.ShowPage(page);
        settingsForm.Show();
        settingsForm.Activate();
    }

    SpeedTestForm? speedForm;

    public void ShowSpeedTest()
    {
        flyout?.Hide();
        if (speedForm is { IsDisposed: false }) { speedForm.Activate(); return; }
        speedForm = new SpeedTestForm(this);
        speedForm.Show();
        speedForm.Activate();
    }

    public void ShowReport()
    {
        flyout?.Hide();
        if (reportForm is { IsDisposed: false }) { reportForm.Activate(); return; }
        reportForm = new ReportForm(this);
        reportForm.Show();
        reportForm.Activate();
    }

    /// <summary>The enabled-or-paused rule for a program, if any.</summary>
    public LimitRule? RuleFor(string exePath) =>
        S.Limits.FirstOrDefault(r => string.Equals(r.ExePath, exePath, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A quick change from the flyout: "limit:BYTES", "block", "unblock", "pause", "remove". Saves, turns limits on
    /// when a rule needs them, and applies at once. Returns a status line, empty on success.
    /// </summary>
    public async Task<string> ChangeRuleAsync(string exePath, string action)
    {
        var s = S.Clone();
        var rule = s.Limits.FirstOrDefault(r => string.Equals(r.ExePath, exePath, StringComparison.OrdinalIgnoreCase));
        if (rule == null && action is not ("remove" or "pause" or "unblock"))
            s.Limits.Add(rule = new LimitRule { ExePath = exePath });
        if (rule != null)
            switch (action)
            {
                case var a when a.StartsWith("limit:", StringComparison.Ordinal) && long.TryParse(a[6..], out long b):
                    rule.Block = false; rule.Enabled = true; rule.DownBytesPerSecond = rule.UpBytesPerSecond = b; break;
                case "block": rule.Block = true; rule.Enabled = true; break;
                case "unblock": rule.Block = false; break;
                case "pause": rule.Enabled = !rule.Enabled; break;
                case "remove": s.Limits.Remove(rule); break;
            }
        // Unblocking a rule that never had speeds leaves nothing to limit: drop it.
        if (rule is { Block: false } && rule.DownBytesPerSecond == 0 && rule.UpBytesPerSecond == 0) s.Limits.Remove(rule);
        if (s.Limits.Any(r => r.Enabled)) s.LimitsEnabled = true;
        S = s;
        S.Save();
        UpdateWidget();
        return await SyncLimitsAsync();
    }

    public void ApplySettings(AppSettings s)
    {
        bool langChanged = s.Language != S.Language;
        bool limitsChanged = s.LimitsEnabled != S.LimitsEnabled
            || System.Text.Json.JsonSerializer.Serialize(s.Limits) != System.Text.Json.JsonSerializer.Serialize(S.Limits);
        S = s;
        L.Ar = S.Language == "ar";
        S.Save();
        widget.SetPlacement(S.WidgetSide == "Left", S.WidgetOffset);
        if (langChanged)
        {
            flyout?.Dispose();
            flyout = null;
            if (reportForm is { IsDisposed: false }) reportForm.Close();
        }
        History.Clear();
        UpdateWidget();
        widget.Invalidate();
        if (limitsChanged) _ = SyncLimitsAsync();
    }

    // ---------- limits ----------

    /// <summary>Starts the elevated helper if needed and sends the rules. Returns a status line, empty on success.</summary>
    public async Task<string> StartLimitsAsync(bool userInitiated)
    {
        if (!LimiterClient.DriverInstalled) return L.T("DriverMissing");
        if (!LimiterClient.HelperRunning && !await LimiterClient.StartAsync())
        {
            if (userInitiated) return L.T("LimiterDeclined");
            Toasts.Show(L.T("AppName"), L.T("LimiterDeclined"));
            return L.T("LimiterDeclined");
        }
        bool ok = await LimiterClient.ApplyAsync(S.Limits.Where(r => r.Enabled).ToList());
        UpdateWidget();
        return ok ? "" : L.T("LimiterUnreachable");
    }

    /// <summary>
    /// The quick switch in the flyout: saves the choice and starts or stops limiting. If the admin prompt is
    /// declined or the helper fails, limits stay off. Returns a status line, empty on success.
    /// </summary>
    public async Task<string> SetLimitsEnabledAsync(bool on)
    {
        S.LimitsEnabled = on;
        S.Save();
        string msg = await SyncLimitsAsync();
        if (on && msg.Length > 0)
        {
            S.LimitsEnabled = false;
            S.Save();
            UpdateWidget();
        }
        return msg;
    }

    public async Task<string> SyncLimitsAsync()
    {
        if (S.LimitsEnabled && S.Limits.Any(r => r.Enabled)) return await StartLimitsAsync(userInitiated: true);
        if (LimiterClient.HelperRunning)
        {
            // The helper also feeds the Programs list: keep it, just without rules, while that is shown.
            if (S.ShowPrograms) await LimiterClient.ApplyAsync([]);
            else await LimiterClient.StopAsync();
        }
        UpdateWidget();
        return "";
    }

    // ---------- programs ----------

    /// <summary>Busiest programs right now, merged by name, with today's data since monitoring began.</summary>
    public IReadOnlyList<ProgramUsage> Programs { get; private set; } = [];
    /// <summary>Why the Programs list can't show, or null.</summary>
    public string? ProgramsError { get; private set; }
    public bool ProgramsStarting { get; private set; }

    DateTime lastPoll = DateTime.MinValue;
    bool polling;

    /// <summary>Starts the elevated helper if needed (one admin prompt) and its kernel network monitor.</summary>
    public async Task StartProgramsAsync()
    {
        if (ProgramsStarting) return;
        ProgramsStarting = true;
        ProgramsError = null;
        try
        {
            if (!LimiterClient.HelperRunning && !await LimiterClient.StartAsync())
            {
                ProgramsError = L.T("ProgramsDeclined");
                return;
            }
            ProgramsError = await LimiterClient.MonitorAsync();
            if (ProgramsError == null && !S.LimitsEnabled) await LimiterClient.ApplyAsync([]);
            if (ProgramsError == null) await PollProgramsAsync();
        }
        finally { ProgramsStarting = false; }
    }

    public async Task SetShowProgramsAsync(bool on)
    {
        S.ShowPrograms = on;
        S.Save();
        if (on) { await StartProgramsAsync(); return; }
        Programs = [];
        if (LimiterClient.HelperRunning && !(S.LimitsEnabled && S.Limits.Any(r => r.Enabled))) await LimiterClient.StopAsync();
    }

    async Task PollProgramsAsync()
    {
        if (polling || !LimiterClient.HelperRunning) return;
        polling = true;
        try
        {
            var st = await LimiterClient.StatusAsync();
            if (st == null) return;
            var now = DateTime.Now;
            var day = DateOnly.FromDateTime(now);
            double dt = lastPoll == DateTime.MinValue ? 0 : Math.Min(10, (now - lastPoll).TotalSeconds);
            lastPoll = now;
            // Several exes can share a name (two copies of one app): show them as one row.
            var merged = st.Processes
                .GroupBy(p => ProgramNames.For(p.ExePath), StringComparer.CurrentCultureIgnoreCase)
                .Select(g => (name: g.Key, path: g.First().ExePath, down: g.Sum(p => p.DownBytesPerSecond), up: g.Sum(p => p.UpBytesPerSecond)))
                .ToList();
            // Logged per day for the report; today's total in the flyout comes from the same log, so it survives restarts.
            foreach (var m in merged) ProgramLog.Add(day, m.name, m.path, m.down * dt, m.up * dt);
            Programs = merged
                .Select(m => new ProgramUsage(m.path, m.name, m.down, m.up, ProgramLog.DayTotal(day, m.name)))
                .Where(p => p.Down + p.Up > 0 || p.Today > 0)
                .OrderByDescending(p => p.Down + p.Up).ThenByDescending(p => p.Today)
                .ToList();
        }
        catch (Exception ex) { Log.Error(ex); }
        finally { polling = false; }
    }

    // ---------- updates ----------

    Updater.Release? pendingUpdate;
    bool installing;

    /// <summary>True while checking for or installing an update, so the Updates page can show progress.</summary>
    public bool UpdateBusy { get; private set; }
    /// <summary>0 to 1 while downloading, null when the end isn't known (checking, verifying).</summary>
    public double? UpdateFraction { get; private set; }
    /// <summary>What the update is doing now, or its last result.</summary>
    public string UpdateStatus { get; private set; } = "";
    public event Action? UpdateChanged;

    /// <summary>For --shots: shows an update in progress without downloading anything.</summary>
    public void DemoUpdate(bool busy, double? fraction, string status) => SetUpdate(busy, fraction, status);

    void SetUpdate(bool busy, double? fraction, string status)
    {
        UpdateBusy = busy; UpdateFraction = fraction; UpdateStatus = status;
        UpdateChanged?.Invoke();
    }

    /// <summary>Checks GitHub; installs automatically when allowed. Returns a status line.</summary>
    public async Task<string> CheckForUpdatesAsync(bool userInitiated)
    {
        if (!userInitiated && !S.AutoCheckUpdates) return "";
        if (installing) return UpdateStatus;
        Updater.Release? rel;
        if (userInitiated) SetUpdate(true, null, L.T("CheckingUpdates"));
        try { rel = await Updater.CheckAsync(); }
        catch (Exception ex)
        {
            Log.Error(ex);
            if (userInitiated) SetUpdate(false, null, L.T("UpdateCheckFailed"));
            return L.T("UpdateCheckFailed");
        }
        S.LastUpdateCheckUtc = DateTime.UtcNow;
        S.Save();
        if (rel == null)
        {
            if (userInitiated) SetUpdate(false, null, L.F("UpToDate", Updater.CurrentText));
            return L.F("UpToDate", Updater.CurrentText);
        }

        pendingUpdate = rel;
        string ver = rel.Version.ToString(3);
        if (userInitiated || S.AutoInstallUpdates)
        {
            _ = InstallUpdateAsync(rel);
            return L.F("Updating", ver);
        }
        Toasts.Show(L.T("AppName"), L.F("UpdateAvailable", ver));
        SetUpdate(false, null, L.F("UpdateAvailable", ver));
        return L.F("UpdateAvailable", ver);
    }

    async Task InstallUpdateAsync(Updater.Release rel)
    {
        if (installing) return;
        installing = true;
        string ver = rel.Version.ToString(3);
        Toasts.Show(L.T("AppName"), L.F("Updating", ver));
        SetUpdate(true, 0, L.F("Updating", ver));
        SaveLogs();
        // Progress<T> made here posts its reports back to the UI thread.
        var progress = new Progress<Updater.Progress>(p =>
        {
            if (!installing) return;
            if (p.Verifying) SetUpdate(true, null, L.T("UpdateVerifying"));
            else if (p.Total > 0)
                SetUpdate(true, (double)p.Done / p.Total,
                    L.F("UpdateDownloading", ver, L.Ltr($"{100.0 * p.Done / p.Total:0}%  {ByteFormat.Size(p.Done)} / {ByteFormat.Size(p.Total)}")));
            else SetUpdate(true, null, L.F("UpdateDownloading", ver, L.Ltr(ByteFormat.Size(p.Done))));
        });
        if (await Updater.InstallAsync(rel, progress))
        {
            SetUpdate(true, 1, L.F("UpdateRestarting", ver));
            Exit();
            return;
        }
        installing = false;
        SetUpdate(false, null, L.T("UpdateFailed"));
        Toasts.Show(L.T("AppName"), L.T("UpdateFailed"));
        Updater.OpenPage(rel.PageUrl);
    }

    void BuildMenu()
    {
        menu.Items.Clear();
        menu.RightToLeft = L.Ar ? RightToLeft.Yes : RightToLeft.No;
        menu.Items.Add(L.T("ShowTraffic"), null, (_, _) => ShowFlyout()).Font = new Font(menu.Font, FontStyle.Bold);
        menu.Items.Add(L.T("UsageReport"), null, (_, _) => ShowReport());
        menu.Items.Add(L.T("SpeedTest"), null, (_, _) => ShowSpeedTest());
        menu.Items.Add(L.T("SpeedLimits"), null, (_, _) => ShowSettings(SettingsForm.LimitsPage));

        var ifMenu = new ToolStripMenuItem(L.T("Interface"));
        void AddIf(string id, string text)
        {
            var item = new ToolStripMenuItem(text) { Checked = S.InterfaceId == id };
            item.Click += (_, _) => { var s = S.Clone(); s.InterfaceId = id; ApplySettings(s); };
            ifMenu.DropDownItems.Add(item);
        }
        AddIf(AppSettings.AutoInterface, L.T("AutoInterface"));
        ifMenu.DropDownItems.Add(new ToolStripSeparator());
        foreach (var (id, name, _, up) in Monitor.All().Where(i => i.up)) AddIf(id, name);
        menu.Items.Add(ifMenu);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("Settings"), null, (_, _) => ShowSettings());
        menu.Items.Add(L.T("CheckForUpdates"), null, async (_, _) =>
        {
            var msg = await CheckForUpdatesAsync(userInitiated: true);
            if (!installing) Toasts.Show(L.T("AppName"), msg);
        });
        var startup = new ToolStripMenuItem(L.T("StartWithWindows")) { Checked = AppSettings.StartWithWindows };
        startup.Click += (_, _) => { try { AppSettings.StartWithWindows = !startup.Checked; } catch { } };
        menu.Items.Add(startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.T("Exit"), null, (_, _) => Exit());
    }

    public const string ShowFlyoutMessage = "DataTide.ShowFlyout";

    public void OnTaskbarCreated() => widget.Ensure();

    public void ShowFlyoutFromOutside()
    {
        if (flyout?.Visible == true) return;
        ShowFlyout();
    }

    public void Exit()
    {
        timer.Stop();
        SaveLogs();
        if (LimiterClient.HelperRunning)
            try { LimiterClient.StopAsync().Wait(2000); } catch { }
        if (widget.Handle != IntPtr.Zero) widget.DestroyHandle();
        ExitThread();
    }
}
