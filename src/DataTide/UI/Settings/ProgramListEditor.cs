namespace DataTide.UI.Settings;

/// <summary>
/// A list of programs (exe paths) to edit in place: one line per program with its icon, name and a remove button,
/// then a picker of running programs with Add and Browse. Edits go straight into the list it was given.
/// Used by game mode (the games) and the metered block (the programs to cut off).
/// </summary>
sealed class ProgramListEditor : FlowLayoutPanel
{
    public sealed record Choice(string Path, string Label, Image Icon);

    readonly List<string> items;
    readonly Theme t;
    readonly ToolTip tip;
    readonly IReadOnlyList<Choice> running;
    readonly FlowLayoutPanel rows = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = Padding.Empty };
    readonly Label empty = new() { AutoSize = true, Tag = "dim", Margin = new Padding(0, 4, 0, 6) };
    readonly ThemedCombo combo = new() { Width = 300 };
    readonly IconButton add = new() { Height = 30, Font = new Font("Segoe UI", 9.5f) };
    readonly IconButton browse = new() { Height = 30, Font = new Font("Segoe UI", 9.5f) };

    public event Action? Changed;

    public ProgramListEditor(List<string> items, Theme theme, ToolTip tip, IReadOnlyList<Choice> running, string emptyText)
    {
        this.items = items;
        t = theme;
        this.tip = tip;
        this.running = running;
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        AutoSize = true;
        Margin = Padding.Empty;
        empty.Text = emptyText;
        empty.ForeColor = t.Dim;

        foreach (var c in running) { combo.Items.Add(c.Label); combo.Icons.Add(c.Icon); }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        add.Text = L.T("Add");
        browse.Text = L.T("Browse");
        add.Width = Math.Max(80, TextRenderer.MeasureText(add.Text, add.Font).Width + 32);
        browse.Width = Math.Max(90, TextRenderer.MeasureText(browse.Text, browse.Font).Width + 32);
        add.Click += (_, _) => { if (combo.SelectedIndex >= 0 && combo.SelectedIndex < running.Count) Add(running[combo.SelectedIndex].Path); };
        browse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Filter = $"{L.T("Programs")}|*.exe" };
            if (dlg.ShowDialog(FindForm()) == DialogResult.OK) Add(dlg.FileName);
        };

        var addRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 2) };
        combo.Margin = new Padding(0, 0, 8, 0);
        add.Margin = new Padding(0, 0, 8, 0);
        browse.Margin = Padding.Empty;
        addRow.Controls.AddRange([combo, add, browse]);

        Controls.Add(rows);
        Controls.Add(empty);
        Controls.Add(addRow);
        foreach (var p in items) AddRow(p);
        SyncEmpty();
    }

    void Add(string path)
    {
        if (items.Contains(path, StringComparer.OrdinalIgnoreCase)) return;
        items.Add(path);
        AddRow(path);
        SyncEmpty();
        Changed?.Invoke();
    }

    void AddRow(string path)
    {
        var row = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Width = 494, Height = 34, Margin = new Padding(0, 0, 0, 2), RightToLeft = RightToLeft };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var left = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        left.Controls.Add(new PictureBox
        {
            Size = new Size(16, 16), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 1, 8, 0),
            Image = ProgramIcons.For(path), BackColor = Color.Transparent,
        });
        var name = new Label
        {
            Text = ProgramNames.For(path), AutoSize = true, Tag = "keep", ForeColor = t.Text, Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 9.75f), AutoEllipsis = true, MaximumSize = new Size(400, 0),
        };
        left.Controls.Add(name);
        tip.SetToolTip(name, path);
        var remove = new IconButton { Width = 32, Height = 28, Glyph = Art.IconDelete, Theme = t, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
        tip.SetToolTip(remove, L.T("Remove"));
        remove.Click += (_, _) =>
        {
            items.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
            rows.Controls.Remove(row);
            row.Dispose();
            SyncEmpty();
            Changed?.Invoke();
        };
        row.Controls.Add(left, 0, 0);
        row.Controls.Add(remove, 1, 0);
        rows.Controls.Add(row);
    }

    void SyncEmpty() => empty.Visible = items.Count == 0;
}
