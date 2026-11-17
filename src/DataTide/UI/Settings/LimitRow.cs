namespace DataTide.UI.Settings;

/// <summary>
/// One program in the Limits page, on a single line: icon and name, download and upload limits in the list's
/// unit (0 = no limit), and a menu for the rarer actions (block completely, pause, remove).
/// A blocked or paused program shows a pill instead of the numbers. The exe path is the name's tooltip.
/// </summary>
sealed class LimitRow : TableLayoutPanel
{
    public LimitRule Rule { get; }
    public event Action<LimitRow>? Removed;

    readonly Theme t;
    readonly ThemedNumber numDown = new() { Minimum = 0, Maximum = 1_000_000, Width = 112 };
    readonly ThemedNumber numUp = new() { Minimum = 0, Maximum = 1_000_000, Width = 112 };
    readonly FlowLayoutPanel limits = new() { AutoSize = true, WrapContents = false, Margin = Padding.Empty, Anchor = AnchorStyles.Right };
    readonly Label pill = new() { AutoSize = true, Tag = "keep", Font = new Font("Segoe UI Semibold", 8.5f), Padding = new Padding(10, 3, 10, 3), Anchor = AnchorStyles.Right, Margin = new Padding(0, 0, 4, 0) };
    readonly Label name = new() { AutoSize = true, Tag = "keep", Font = new Font("Segoe UI Semibold", 9.75f), Margin = Padding.Empty, AutoEllipsis = true, MaximumSize = new Size(170, 0) };
    readonly IconButton btnMore = new() { Width = 32, Height = 30, Glyph = "\uE712" };
    RateUnit unit;
    bool loading;

    public LimitRow(LimitRule rule, Theme theme, ToolTip tip, RateUnit listUnit)
    {
        Rule = rule;
        t = theme;
        ColumnCount = 3;
        RowCount = 1;
        Width = 494;
        Height = 40;
        Margin = new Padding(0, 0, 0, 2);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var left = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        left.Controls.Add(new PictureBox
        {
            Size = new Size(16, 16), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 1, 8, 0),
            Image = ProgramIcons.For(rule.ExePath), BackColor = Color.Transparent,
        });
        name.Text = rule.Name;
        left.Controls.Add(name);
        tip.SetToolTip(name, rule.ExePath);

        limits.Controls.Add(new ArrowGlyph { Up = false, Theme = theme, Margin = new Padding(0, 0, 3, 0) });
        limits.Controls.Add(numDown);
        limits.Controls.Add(new ArrowGlyph { Up = true, Theme = theme, Margin = new Padding(12, 0, 3, 0) });
        limits.Controls.Add(numUp);
        numDown.Margin = numUp.Margin = Padding.Empty;
        tip.SetToolTip(numDown, L.T("DownLimitTip"));
        tip.SetToolTip(numUp, L.T("UpLimitTip"));

        var right = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
        right.Controls.Add(limits);
        right.Controls.Add(pill);
        Controls.Add(left, 0, 0);
        Controls.Add(right, 1, 0);
        Controls.Add(btnMore, 2, 0);
        btnMore.Anchor = AnchorStyles.Right;
        btnMore.Margin = new Padding(8, 0, 0, 0);
        tip.SetToolTip(btnMore, L.T("MoreOptions"));

        SetUnit(listUnit);
        numDown.ValueChanged += (_, _) => { if (!loading) Rule.DownBytesPerSecond = RateUnits.ToBytes(numDown.Value, unit); };
        numUp.ValueChanged += (_, _) => { if (!loading) Rule.UpBytesPerSecond = RateUnits.ToBytes(numUp.Value, unit); };
        btnMore.Click += (_, _) => ShowMenu();
        SyncState();
    }

    /// <summary>Shows the limits in another unit; the stored speeds don't change.</summary>
    public void SetUnit(RateUnit u)
    {
        unit = u;
        loading = true;
        foreach (var n in new[] { numDown, numUp })
        {
            n.DecimalPlaces = u == RateUnit.KBps ? 0 : 1;
            n.Increment = u == RateUnit.KBps ? 64 : u == RateUnit.MBps ? 0.5m : 1;
        }
        numDown.Value = RateUnits.FromBytes(Rule.DownBytesPerSecond, u);
        numUp.Value = RateUnits.FromBytes(Rule.UpBytesPerSecond, u);
        loading = false;
    }

    void SyncState()
    {
        bool showPill = Rule.Block || !Rule.Enabled;
        limits.Visible = !showPill;
        pill.Visible = showPill;
        var c = Rule.Block && Rule.Enabled ? Color.FromArgb(232, 72, 85) : t.Dim;
        pill.Text = !Rule.Enabled ? L.T("Paused") : L.T("Blocked");
        pill.ForeColor = c;
        pill.BackColor = Color.FromArgb(t.Dark ? 40 : 28, c);
        name.ForeColor = Rule.Enabled ? t.Text : t.Dim;
    }

    void ShowMenu()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false, RightToLeft = RightToLeft };
        var block = new ToolStripMenuItem(Rule.Block ? L.T("LimitInstead") : L.T("BlockAll"));
        block.Click += (_, _) => { Rule.Block = !Rule.Block; SyncState(); };
        var pause = new ToolStripMenuItem(Rule.Enabled ? L.T("PauseRule") : L.T("ResumeRule"));
        pause.Click += (_, _) => { Rule.Enabled = !Rule.Enabled; SyncState(); };
        var remove = new ToolStripMenuItem(L.T("Remove")) { ForeColor = Color.FromArgb(232, 72, 85) };
        remove.Click += (_, _) => Removed?.Invoke(this);
        block.ForeColor = pause.ForeColor = t.Text;
        menu.Items.AddRange([block, pause, new ToolStripSeparator(), remove]);
        ThemedMenuRenderer.Style(menu, t);
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        bool rtl = RightToLeft == RightToLeft.Yes;
        menu.Show(btnMore, new Point(rtl ? 0 : btnMore.Width, btnMore.Height), rtl ? ToolStripDropDownDirection.BelowRight : ToolStripDropDownDirection.BelowLeft);
    }
}
