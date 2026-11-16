namespace DataTide.UI.Settings;

/// <summary>
/// One program's limits in the Limits page. First line: icon, name, on/off, remove. Second line: download and
/// upload caps, each with its own unit (0 = no cap). Third line: the exe path.
/// </summary>
sealed class LimitRow : TableLayoutPanel
{
    public LimitRule Rule { get; }
    readonly ToggleSwitch tgl = new();
    readonly IconButton btnRemove = new() { Width = 32, Height = 30, Glyph = Art.IconDelete };
    public event Action<LimitRow>? Removed;

    public LimitRow(LimitRule rule, Theme t, ToolTip tip)
    {
        Rule = rule;
        ColumnCount = 3;
        RowCount = 3;
        Width = 494;
        Height = 104;
        Margin = new Padding(0, 0, 0, 10);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Program icon and name side by side.
        var name = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) };
        name.Controls.Add(new PictureBox
        {
            Size = new Size(16, 16), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 2, 6, 0),
            Image = ProgramIcons.For(rule.ExePath), BackColor = Color.Transparent,
        });
        name.Controls.Add(new Label { Text = rule.Name, AutoSize = true, Font = new Font("Segoe UI Semibold", 9.75f), Margin = Padding.Empty });

        var limits = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, Margin = new Padding(0, 2, 0, 0) };
        limits.Controls.Add(Limit(Direction.Download, t, tip));
        limits.Controls.Add(new Panel { Width = 18, Height = 1, Margin = Padding.Empty });
        limits.Controls.Add(Limit(Direction.Upload, t, tip));

        var path = new Label { Text = rule.ExePath, AutoEllipsis = true, Tag = "dim", Width = 470, Height = 18, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 8f) };
        tip.SetToolTip(path, rule.ExePath);

        tgl.Checked = rule.Enabled;
        tgl.Anchor = btnRemove.Anchor = AnchorStyles.None;
        tgl.Margin = new Padding(8, 0, 6, 0);

        Controls.Add(name, 0, 0);
        Controls.Add(tgl, 1, 0);
        Controls.Add(btnRemove, 2, 0);
        Controls.Add(limits, 0, 1);
        SetColumnSpan(limits, 3);
        Controls.Add(path, 0, 2);
        SetColumnSpan(path, 3);

        tgl.CheckedChanged += (_, _) => Rule.Enabled = tgl.Checked;
        btnRemove.Click += (_, _) => Removed?.Invoke(this);
        tip.SetToolTip(tgl, L.T("RuleOnTip"));
        tip.SetToolTip(btnRemove, L.T("Remove"));
    }

    /// <summary>Arrow, number and unit for one direction; the rule always keeps bytes per second.</summary>
    FlowLayoutPanel Limit(Direction dir, Theme t, ToolTip tip)
    {
        long bytes = Rule.RateFor(dir);
        var unit = RateUnits.Natural(bytes);
        var num = new ThemedNumber { Minimum = 0, Maximum = 1_000_000, Width = 118 };
        var cmb = new ThemedCombo { Width = 82 };
        foreach (var u in RateUnits.All) cmb.Items.Add(RateUnits.Label(u));

        void Configure(RateUnit u)
        {
            num.DecimalPlaces = u == RateUnit.KBps ? 0 : 1;
            num.Increment = u == RateUnit.KBps ? 64 : u == RateUnit.MBps ? 0.5m : 1;
        }
        Configure(unit);
        num.Value = RateUnits.FromBytes(bytes, unit);
        cmb.SelectedIndex = Array.IndexOf(RateUnits.All, unit);

        void Store()
        {
            long v = RateUnits.ToBytes(num.Value, RateUnits.All[Math.Max(0, cmb.SelectedIndex)]);
            if (dir == Direction.Download) Rule.DownBytesPerSecond = v; else Rule.UpBytesPerSecond = v;
        }
        num.ValueChanged += (_, _) => Store();
        cmb.SelectedIndexChanged += (_, _) =>
        {
            // Changing the unit keeps the same speed: 2048 KB/s becomes 2 MB/s, not 2048 MB/s.
            var u = RateUnits.All[Math.Max(0, cmb.SelectedIndex)];
            long current = Rule.RateFor(dir);
            Configure(u);
            num.Value = RateUnits.FromBytes(current, u);
            Store();
        };

        var arrow = new ArrowGlyph { Up = dir == Direction.Upload, Theme = t, Margin = new Padding(0, 0, 4, 0) };
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        num.Margin = new Padding(0, 0, 6, 0);
        cmb.Margin = Padding.Empty;
        p.Controls.AddRange([arrow, num, cmb]);
        tip.SetToolTip(num, L.T(dir == Direction.Download ? "DownLimitTip" : "UpLimitTip"));
        return p;
    }
}
