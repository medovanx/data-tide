using System.Diagnostics;

namespace DataTide.UI.Settings;

/// <summary>Settings in the Windows 11 Settings style: a sidebar of pages, each a rounded card.</summary>
sealed class SettingsForm : Form
{
    public const int GeneralPage = 0, NetworkPage = 1, LimitsPage = 2, UpdatesPage = 3;

    readonly DataTideApp app;
    readonly AppSettings s;
    readonly Theme t = Theme.Current;
    readonly ToolTip tip = new();

    // General
    readonly ThemedCombo cmbLang = Combo(), cmbSide = Combo(), cmbUnits = Combo();
    readonly ThemedNumber numOffset = Num(-2000, 2000);
    readonly ToggleSwitch tglStartup = new();
    // Network
    readonly ThemedCombo cmbIface = Combo(300), cmbKeep = Combo();
    readonly ToggleSwitch tglLog = new(), tglVirtual = new();
    readonly Button btnFolder = new() { AutoSize = true, Height = 30 }, btnClear = new() { AutoSize = true, Height = 30 };
    readonly List<string> ifaceIds = [];
    readonly ThemedNumber numKeep = new() { Minimum = 1, Maximum = 36500, Width = 120, Increment = 30 };
    Control keepCustomRow = null!;
    /// <summary>Presets in days; 0 is forever. The last item of the dropdown is "Custom", backed by <see cref="numKeep"/>.</summary>
    static readonly int[] KeepOptions = [7, 30, 90, 180, 365, 730, 1825, 0];
    // Limits
    readonly ToggleSwitch tglLimits = new();
    readonly FlowLayoutPanel rulesPanel = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = Padding.Empty };
    readonly ThemedCombo cmbProcs = Combo(300);
    readonly Button btnAddProc = new() { AutoSize = true, Height = 30 }, btnBrowse = new() { AutoSize = true, Height = 30 }, btnDriver = new() { AutoSize = true, Height = 30 }, btnDriverPage = new() { AutoSize = true, Height = 30 };
    readonly Label lblDriverProgress = new() { AutoSize = true, Tag = "dim", MaximumSize = new Size(490, 0), Margin = new Padding(0, 6, 0, 0) };
    readonly Label lblDriver = new() { AutoSize = true, MaximumSize = new Size(490, 0), Margin = new Padding(0, 0, 0, 8) };
    readonly Label lblLimits = new() { AutoSize = true, Tag = "dim", MaximumSize = new Size(490, 0), Margin = new Padding(0, 6, 0, 0) };
    readonly Label lblLive = new() { AutoSize = true, Tag = "dim", MaximumSize = new Size(490, 0) };
    readonly FlowLayoutPanel livePanel = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = Padding.Empty };
    readonly List<string> procPaths = [];
    readonly System.Windows.Forms.Timer liveTimer = new() { Interval = 1500 };
    // Updates
    readonly ToggleSwitch tglAutoCheck = new(), tglAutoInstall = new();
    readonly Button btnCheck = new() { AutoSize = true, Height = 30 };
    readonly Label lblUpdate = new() { AutoSize = true, Tag = "dim", MaximumSize = new Size(440, 0) };

    readonly Button btnSave = new() { Size = new Size(110, 34) }, btnCancel = new() { Size = new Size(110, 34) };
    readonly List<(string glyph, string title, Control page)> pages = [];
    readonly NavList nav;
    readonly Panel content = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 20, 0, 0) };

    public SettingsForm(DataTideApp app)
    {
        this.app = app;
        s = app.S.Clone();
        Text = $"{L.T("Settings")} - {L.T("AppName")}";
        Icon = app.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(800, 620);
        Font = new Font("Segoe UI", 9.75f);
        BackColor = t.Window;
        ForeColor = t.Text;
        if (L.Ar) RightToLeft = RightToLeft.Yes;

        BuildGeneral();
        BuildNetwork();
        BuildLimits();
        BuildUpdates();

        nav = new NavList(pages.Select(p => (p.glyph, p.title)).ToArray(), t) { Dock = L.Ar ? DockStyle.Right : DockStyle.Left, Width = 210 };
        nav.SelectedChanged += ShowPage;

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(20, 12, 20, 16) };
        btnSave.Text = L.T("Save");
        btnCancel.Text = L.T("Cancel");
        btnSave.Click += (_, _) => Save();
        btnCancel.Click += (_, _) => Close();
        AcceptButton = btnSave;
        CancelButton = btnCancel;
        var buttons = new FlowLayoutPanel { Dock = L.Ar ? DockStyle.Left : DockStyle.Right, AutoSize = true, FlowDirection = L.Ar ? FlowDirection.LeftToRight : FlowDirection.RightToLeft, WrapContents = false };
        buttons.Controls.AddRange([btnSave, btnCancel]);
        footer.Controls.Add(buttons);

        foreach (var p in pages) { p.page.Visible = false; content.Controls.Add(p.page); }
        Controls.Add(content);
        Controls.Add(footer);
        Controls.Add(nav);

        Styler.Apply(this, t);
        Styler.Primary(btnSave, t);
        ShowPage(0);
        SyncEnabled();
        liveTimer.Tick += async (_, _) => await RefreshLiveAsync();
        liveTimer.Start();
        FormClosed += (_, _) => { liveTimer.Dispose(); tip.Dispose(); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RoundCorners(Handle, t.Dark);
        int caption = ColorTranslator.ToWin32(t.Window);
        Native.DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int)); // DWMWA_CAPTION_COLOR
    }

    public int PageCount => pages.Count;

    public void ShowPage(int i)
    {
        i = Math.Clamp(i, 0, pages.Count - 1);
        for (int k = 0; k < pages.Count; k++) pages[k].page.Visible = k == i;
        nav.Selected = i;
    }

    // ---------- pages ----------

    void AddPage(string glyph, string title, params Control[] sections)
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        var heading = new Label { Text = title, AutoSize = true, Font = new Font("Segoe UI Semibold", 17f), Margin = new Padding(2, 0, 0, 12) };
        flow.Controls.Add(heading);
        foreach (var sec in sections) { sec.Margin = new Padding(0, 0, 0, 12); flow.Controls.Add(sec); }
        Native.DarkScrollbars(flow, t.Dark);
        pages.Add((glyph, title, flow));
    }

    void BuildGeneral()
    {
        cmbLang.Items.AddRange(["English", "العربية"]);
        cmbLang.SelectedIndex = s.Language == "ar" ? 1 : 0;
        cmbSide.Items.AddRange([L.T("SideRight"), L.T("SideLeft")]);
        cmbSide.SelectedIndex = s.WidgetSide == "Left" ? 1 : 0;
        numOffset.Value = Math.Clamp(s.WidgetOffset, -2000, 2000);
        numOffset.Increment = 10;
        cmbUnits.Items.AddRange([L.T("UnitsBytes"), L.T("UnitsBits")]);
        cmbUnits.SelectedIndex = s.ShowBits ? 1 : 0;
        tglStartup.Checked = AppSettings.StartWithWindows;
        AddPage(Art.IconGeneral, L.T("General"), Card(L.T("General"), Art.IconGeneral,
            Row(L.T("Language"), cmbLang),
            Row(L.T("Units"), cmbUnits),
            Row(L.T("WidgetPosition"), cmbSide),
            Row(L.T("WidgetOffset"), numOffset),
            Row(L.T("StartWithWindows"), tglStartup)));
    }

    void BuildNetwork()
    {
        ifaceIds.Add(AppSettings.AutoInterface);
        cmbIface.Items.Add(L.T("AutoInterface"));
        foreach (var (id, name, desc, up) in app.Monitor.All())
        {
            ifaceIds.Add(id);
            cmbIface.Items.Add(up ? name : $"{name} ({L.T("Disconnected")})");
        }
        if (!ifaceIds.Contains(s.InterfaceId)) { ifaceIds.Add(s.InterfaceId); cmbIface.Items.Add(app.Ledger.NameOf(s.InterfaceId)); }
        cmbIface.SelectedIndex = Math.Max(0, ifaceIds.IndexOf(s.InterfaceId));

        tglLog.Checked = s.LogUsage;
        tglVirtual.Checked = s.LogVirtual;
        cmbKeep.Items.AddRange([L.T("Keep7"), L.T("Keep30"), L.T("Keep90"), L.T("Keep180"), L.T("Keep365"), L.T("Keep730"), L.T("Keep1825"), L.T("KeepForever"), L.T("KeepCustom")]);
        int preset = Array.IndexOf(KeepOptions, s.KeepDays);
        cmbKeep.SelectedIndex = preset >= 0 ? preset : KeepOptions.Length;
        numKeep.Value = s.KeepDays > 0 ? s.KeepDays : 365;
        cmbKeep.SelectedIndexChanged += (_, _) => SyncEnabled();
        tglLog.CheckedChanged += (_, _) => SyncEnabled();
        btnFolder.Text = L.T("OpenDataFolder");
        btnClear.Text = L.T("ClearHistory");
        btnFolder.Click += (_, _) => { Directory.CreateDirectory(AppPaths.Data); Updater.OpenPage(AppPaths.Data); };
        btnClear.Click += (_, _) =>
        {
            if (MessageBox.Show(this, L.T("ClearConfirm"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            app.Ledger.Clear();
            UsageStore.Save(app.Ledger);
        };
        var autoNote = new Label { Text = L.T("AutoInterfaceNote"), AutoSize = true, MaximumSize = new Size(490, 0), Tag = "dim", Margin = new Padding(0, 0, 0, 6) };
        var virtualNote = new Label { Text = L.T("LogVirtualNote"), AutoSize = true, MaximumSize = new Size(490, 0), Tag = "dim", Margin = new Padding(0, 0, 0, 6) };
        var logNote = new Label { Text = L.T("LogNote"), AutoSize = true, MaximumSize = new Size(490, 0), Tag = "dim", Margin = new Padding(0, 6, 0, 6) };
        AddPage(Art.IconNetwork, L.T("Network"),
            Card(L.T("WidgetInterface"), Art.IconNetwork, Stacked(L.T("Interface"), cmbIface), autoNote),
            Card(L.T("UsageLog"), Art.IconChart, Row(L.T("LogUsage"), tglLog), Row(L.T("LogVirtual"), tglVirtual), virtualNote, Row(L.T("KeepHistory"), cmbKeep), keepCustomRow = Row(L.T("KeepDays"), numKeep), logNote, Flow(btnFolder, btnClear)));
    }

    void BuildLimits()
    {
        tglLimits.Checked = s.LimitsEnabled;
        btnDriver.Text = L.T("InstallDriver");
        btnDriverPage.Text = L.T("DriverPage");
        btnDriverPage.Click += (_, _) => Updater.OpenPage(LimiterClient.DriverDownloadPage);
        btnDriver.Click += async (_, _) =>
        {
            btnDriver.Enabled = false;
            var result = await DriverInstaller.InstallAsync(new Progress<string>(m => lblDriverProgress.Text = m));
            btnDriver.Enabled = true;
            UpdateDriverText();
            SyncEnabled();
        };
        btnAddProc.Text = L.T("Add");
        btnBrowse.Text = L.T("Browse");
        FillProcesses();
        foreach (var r in s.Limits) AddRuleRow(r);

        btnAddProc.Click += (_, _) =>
        {
            if (cmbProcs.SelectedIndex >= 0 && cmbProcs.SelectedIndex < procPaths.Count) AddRule(procPaths[cmbProcs.SelectedIndex]);
        };
        btnBrowse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = $"{L.T("Programs")}|*.exe" };
            if (dlg.ShowDialog(this) == DialogResult.OK) AddRule(dlg.FileName);
        };
        tglLimits.CheckedChanged += (_, _) => SyncEnabled();

        var how = new Label { Text = L.T("LimitsNote"), AutoSize = true, MaximumSize = new Size(490, 0), Tag = "dim", Margin = new Padding(0, 0, 0, 8) };
        var driverCard = Card(L.T("Driver"), Art.IconShield, lblDriver, how, Flow(btnDriver, btnDriverPage), lblDriverProgress);
        var rulesCard = Card(L.T("SpeedLimits"), Art.IconSpeed,
            Row(L.T("EnableLimits"), tglLimits),
            rulesPanel,
            Stacked(L.T("AddProgram"), Flow(cmbProcs, btnAddProc, btnBrowse)),
            lblLimits);
        var liveCard = Card(L.T("LiveByProgram"), Art.IconChart, lblLive, livePanel);
        if (LimiterClient.DriverInstalled) AddPage(Art.IconSpeed, L.T("Limits"), rulesCard, liveCard, driverCard);
        else AddPage(Art.IconSpeed, L.T("Limits"), driverCard, rulesCard, liveCard); // what's missing comes first
        UpdateDriverText();
    }

    void UpdateDriverText()
    {
        bool ok = LimiterClient.DriverInstalled;
        lblDriver.Text = ok ? L.T("DriverInstalled") : L.T("DriverMissingLong");
        lblDriver.ForeColor = ok ? t.Accent : t.Up;
        lblDriver.Tag = "keep";
        btnDriver.Visible = btnDriverPage.Visible = !ok;
    }

    void FillProcesses()
    {
        cmbProcs.Items.Clear();
        cmbProcs.Icons.Clear();
        procPaths.Clear();
        if (app.Headless)
        {
            // Screenshots never list the user's real programs.
            procPaths.Add(@"C:\Program Files\Example Browser\browser.exe");
            cmbProcs.Items.Add("Example Browser  (browser.exe)");
            cmbProcs.Icons.Add(SystemIcons.Application.ToBitmap());
            cmbProcs.SelectedIndex = 0;
            return;
        }
        // Apps with a window come first (what people usually want to limit), then background programs.
        var found = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id == Environment.ProcessId) continue;
                string? path = p.MainModule?.FileName;
                if (path == null || path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase)) continue;
                bool windowed = p.MainWindowHandle != IntPtr.Zero;
                found[path] = found.TryGetValue(path, out var w) ? w || windowed : windowed;
            }
            catch { /* protected or elevated process */ }
            finally { p.Dispose(); }
        }
        var items = found.Select(kv => (path: kv.Key, windowed: kv.Value, name: FriendlyName(kv.Key)))
            .OrderByDescending(x => x.windowed).ThenBy(x => x.name, StringComparer.CurrentCultureIgnoreCase).ToList();
        foreach (var (path, _, name) in items)
        {
            procPaths.Add(path);
            cmbProcs.Items.Add($"{name}  ({Path.GetFileName(path)})");
            cmbProcs.Icons.Add(ProgramIcons.For(path));
        }
        if (cmbProcs.Items.Count > 0) cmbProcs.SelectedIndex = 0;
    }

    /// <summary>The program's own description ("Google Chrome") when it has one, else its file name.</summary>
    static string FriendlyName(string path)
    {
        try
        {
            var d = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
            if (!string.IsNullOrEmpty(d) && d.Length <= 60) return d;
        }
        catch { }
        return Path.GetFileNameWithoutExtension(path);
    }

    void AddRule(string exe)
    {
        if (s.Limits.Any(r => string.Equals(r.ExePath, exe, StringComparison.OrdinalIgnoreCase))) return;
        var rule = new LimitRule { ExePath = exe, DownBytesPerSecond = 1024 * 1024, UpBytesPerSecond = 256 * 1024 };
        s.Limits.Add(rule);
        AddRuleRow(rule);
        Styler.Apply(rulesPanel, t);
        SyncEnabled();
    }

    void AddRuleRow(LimitRule rule)
    {
        var row = new LimitRow(rule, t, tip) { RightToLeft = RightToLeft };
        row.Removed += r =>
        {
            s.Limits.Remove(r.Rule);
            rulesPanel.Controls.Remove(r);
            r.Dispose();
            SyncEnabled();
        };
        rulesPanel.Controls.Add(row);
    }

    async Task RefreshLiveAsync()
    {
        if (!pages[LimitsPage].page.Visible) return;
        if (!LimiterClient.HelperRunning)
        {
            lblLive.Text = LimiterClient.DriverInstalled ? L.T("LiveOff") : L.T("DriverMissing");
            if (livePanel.Controls.Count > 0) livePanel.Controls.Clear();
            return;
        }
        var st = await LimiterClient.StatusAsync();
        if (st == null) { lblLive.Text = L.T("LimiterUnreachable"); return; }
        lblLive.Text = st.Error ?? L.T("LiveNote");
        var top = st.Processes.OrderByDescending(p => p.DownBytesPerSecond + p.UpBytesPerSecond).Take(8).ToList();
        livePanel.SuspendLayout();
        livePanel.Controls.Clear();
        foreach (var p in top)
        {
            var text = $"{Path.GetFileNameWithoutExtension(p.ExePath)}    \u2193 {app.Rate(p.DownBytesPerSecond)}    \u2191 {app.Rate(p.UpBytesPerSecond)}";
            var lbl = new Label { Text = text, AutoSize = true, Margin = new Padding(0, 4, 0, 4), ForeColor = t.Text, BackColor = Color.Transparent };
            tip.SetToolTip(lbl, p.ExePath);
            livePanel.Controls.Add(lbl);
        }
        livePanel.ResumeLayout();
    }

    void BuildUpdates()
    {
        tglAutoCheck.Checked = s.AutoCheckUpdates;
        tglAutoInstall.Checked = s.AutoInstallUpdates;
        btnCheck.Text = L.T("CheckNow");
        var version = new Label { Text = L.F("Version", Updater.CurrentText), AutoSize = true, Font = new Font("Segoe UI Semibold", 11f), Margin = new Padding(0, 0, 0, 8) };
        AddPage(Art.IconUpdate, L.T("Updates"), Card(L.T("Updates"), Art.IconUpdate,
            version,
            Row(L.T("AutoCheck"), tglAutoCheck),
            Row(L.T("AutoInstall"), tglAutoInstall),
            Flow(btnCheck, lblUpdate)));
        tglAutoCheck.CheckedChanged += (_, _) => SyncEnabled();
        btnCheck.Click += async (_, _) =>
        {
            btnCheck.Enabled = false;
            lblUpdate.Text = "...";
            lblUpdate.Text = await app.CheckForUpdatesAsync(userInitiated: true);
            btnCheck.Enabled = true;
        };
    }

    void SyncEnabled()
    {
        tglAutoInstall.Enabled = tglAutoCheck.Checked;
        cmbKeep.Enabled = numKeep.Enabled = tglVirtual.Enabled = tglLog.Checked;
        keepCustomRow.Visible = cmbKeep.SelectedIndex == KeepOptions.Length;
        int active = s.Limits.Count(r => r.Enabled);
        lblLimits.Text = !LimiterClient.DriverInstalled ? L.T("DriverMissing")
                       : s.Limits.Count == 0 ? L.T("NoRules")
                       : tglLimits.Checked ? L.T("LimitsSaveNote") : L.T("LimitsOffNote");
    }

    void Save()
    {
        s.Language = cmbLang.SelectedIndex == 1 ? "ar" : "en";
        s.WidgetSide = cmbSide.SelectedIndex == 1 ? "Left" : "Right";
        s.WidgetOffset = (int)numOffset.Value;
        s.ShowBits = cmbUnits.SelectedIndex == 1;
        s.InterfaceId = ifaceIds[Math.Max(0, cmbIface.SelectedIndex)];
        s.LogUsage = tglLog.Checked;
        s.LogVirtual = tglVirtual.Checked;
        s.KeepDays = cmbKeep.SelectedIndex >= 0 && cmbKeep.SelectedIndex < KeepOptions.Length
            ? KeepOptions[cmbKeep.SelectedIndex] : (int)numKeep.Value;
        s.LimitsEnabled = tglLimits.Checked;
        s.AutoCheckUpdates = tglAutoCheck.Checked;
        s.AutoInstallUpdates = tglAutoInstall.Checked;
        try { AppSettings.StartWithWindows = tglStartup.Checked; } catch { /* registry blocked */ }
        app.ApplySettings(s);
        Close();
    }

    // ---------- layout helpers ----------

    static ThemedCombo Combo(int width = 220) => new() { Width = width };
    static ThemedNumber Num(int min, int max) => new() { Minimum = min, Maximum = max, Width = 120 };

    Section Card(string title, string glyph, params Control[] rows)
    {
        var sec = new Section(title) { Glyph = glyph, Theme = t, RightToLeft = RightToLeft };
        sec.MinimumSize = new Size(526, 0);
        sec.MaximumSize = new Size(526, 0);
        var stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Location = new Point(16, 44), MinimumSize = new Size(494, 0), MaximumSize = new Size(494, 0), RightToLeft = RightToLeft };
        foreach (var r in rows) stack.Controls.Add(r);
        sec.Controls.Add(stack);
        return sec;
    }

    /// <summary>Label on the near side, control on the far side.</summary>
    static TableLayoutPanel Row(string label, Control c)
    {
        var tl = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Width = 494, Height = Math.Max(36, (c.AutoSize ? c.PreferredSize.Height : c.Height) + 8), Margin = new Padding(0) };
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tl.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, MaximumSize = new Size(300, 0) }, 0, 0);
        c.Anchor = AnchorStyles.Right;
        c.Margin = new Padding(0, c.Margin.Top, 0, c.Margin.Bottom);
        tl.Controls.Add(c, 1, 0);
        return tl;
    }

    /// <summary>Label above a wide control.</summary>
    static FlowLayoutPanel Stacked(string label, Control c)
    {
        var f = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 8) };
        f.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 4, 0, 4) });
        if (c is ThemedCombo) c.Width = 490;
        f.Controls.Add(c);
        return f;
    }

    static FlowLayoutPanel Flow(params Control[] cs)
    {
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 4) };
        int tallest = cs.Max(c => c.Height);
        for (int i = 0; i < cs.Length; i++)
        {
            int top = (tallest - cs[i].Height) / 2;
            cs[i].Margin = new Padding(0, top, i < cs.Length - 1 ? 8 : 0, 0);
            p.Controls.Add(cs[i]);
        }
        return p;
    }
}
