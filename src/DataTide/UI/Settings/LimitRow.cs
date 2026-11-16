namespace DataTide.UI.Settings;

/// <summary>One program's limits in the Limits page: name, download and upload caps in KB/s (0 = no cap), on/off, remove.</summary>
sealed class LimitRow : TableLayoutPanel
{
    public LimitRule Rule { get; }
    readonly ThemedNumber numDown = new() { Minimum = 0, Maximum = 1_000_000, Width = 118, Increment = 64 };
    readonly ThemedNumber numUp = new() { Minimum = 0, Maximum = 1_000_000, Width = 118, Increment = 64 };
    readonly ToggleSwitch tgl = new();
    readonly IconButton btnRemove = new() { Width = 32, Height = 30, Glyph = Art.IconDelete };
    public event Action<LimitRow>? Removed;

    public LimitRow(LimitRule rule, Theme t, ToolTip tip)
    {
        Rule = rule;
        ColumnCount = 5;
        RowCount = 2;
        Width = 494;
        Height = 70;
        Margin = new Padding(0, 0, 0, 8);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++) ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Program icon and name side by side.
        var name = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) };
        var icon = new PictureBox
        {
            Size = new Size(16, 16), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 2, 6, 0),
            Image = ProgramIcons.For(rule.ExePath), BackColor = Color.Transparent,
        };
        name.Controls.Add(icon);
        name.Controls.Add(new Label { Text = rule.Name, AutoSize = true, Font = new Font("Segoe UI Semibold", 9.75f), Margin = Padding.Empty });
        var path = new Label { Text = rule.ExePath, AutoEllipsis = true, Tag = "dim", Width = 470, Height = 18, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 8f) };
        tip.SetToolTip(path, rule.ExePath);
        numDown.Value = rule.DownBytesPerSecond / 1024;
        numUp.Value = rule.UpBytesPerSecond / 1024;
        tgl.Checked = rule.Enabled;
        var down = Labeled("\u2193", numDown, t.Down);
        var up = Labeled("\u2191", numUp, t.Up);

        Controls.Add(name, 0, 0);
        Controls.Add(down, 1, 0);
        Controls.Add(up, 2, 0);
        Controls.Add(tgl, 3, 0);
        Controls.Add(btnRemove, 4, 0);
        Controls.Add(path, 0, 1);
        SetColumnSpan(path, 5);
        tgl.Anchor = btnRemove.Anchor = AnchorStyles.None;
        tgl.Margin = new Padding(8, 0, 4, 0);

        numDown.ValueChanged += (_, _) => Rule.DownBytesPerSecond = (long)numDown.Value * 1024;
        numUp.ValueChanged += (_, _) => Rule.UpBytesPerSecond = (long)numUp.Value * 1024;
        tgl.CheckedChanged += (_, _) => Rule.Enabled = tgl.Checked;
        btnRemove.Click += (_, _) => Removed?.Invoke(this);
        tip.SetToolTip(numDown, L.T("DownLimitTip"));
        tip.SetToolTip(numUp, L.T("UpLimitTip"));
        tip.SetToolTip(btnRemove, L.T("Remove"));
    }

    static FlowLayoutPanel Labeled(string arrow, Control c, Color color)
    {
        var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(6, 0, 0, 0), Anchor = AnchorStyles.None };
        var l = new Label { Text = arrow, AutoSize = true, ForeColor = color, Font = new Font("Segoe UI Black", 12f), Margin = new Padding(0, 3, 2, 0) };
        p.Controls.Add(l);
        p.Controls.Add(c);
        l.Tag = "keep"; // Styler leaves its colour alone
        return p;
    }
}
