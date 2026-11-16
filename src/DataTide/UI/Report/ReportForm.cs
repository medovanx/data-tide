namespace DataTide.UI.Report;

/// <summary>The usage report window: pick a range and an interface, read the chart and tables, export to PNG or PDF.</summary>
sealed class ReportForm : Form
{
    readonly DataTideApp app;
    readonly Theme t = Theme.Current;
    readonly ThemedCombo cmbRange = new() { Width = 190 }, cmbIface = new() { Width = 240 };
    readonly IconButton btnPng = new() { Width = 130, Height = 32 }, btnPdf = new() { Width = 130, Height = 32 };
    readonly Panel scroller = new() { Dock = DockStyle.Fill, AutoScroll = true };
    readonly ReportView view = new();
    readonly List<string?> ifaceIds = [];
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 30_000 };
    UsageReport? current;

    static readonly ReportRange[] Ranges = [ReportRange.Today, ReportRange.Last7Days, ReportRange.Last30Days, ReportRange.ThisMonth, ReportRange.LastMonth, ReportRange.AllTime];

    public ReportForm(DataTideApp app)
    {
        this.app = app;
        Text = $"{L.T("UsageReport")} - {L.T("AppName")}";
        Icon = app.AppIcon;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1000, 760);
        MinimumSize = new Size(760, 480);
        Font = new Font("Segoe UI", 9.75f);
        BackColor = t.Window;
        ForeColor = t.Text;
        if (L.Ar) { RightToLeft = RightToLeft.Yes; }

        foreach (var r in Ranges) cmbRange.Items.Add(L.T("Range" + r));
        cmbRange.SelectedIndex = 2;
        ifaceIds.Add(null);
        cmbIface.Items.Add(L.T("AllInterfaces"));
        foreach (var (id, name) in app.Ledger.Interfaces()) { ifaceIds.Add(id); cmbIface.Items.Add(name); }
        cmbIface.SelectedIndex = 0;
        btnPng.Text = L.T("ExportPng");
        btnPng.Glyph = Art.IconPicture;
        btnPdf.Text = L.T("ExportPdf");
        btnPdf.Glyph = Art.IconDocument;
        btnPdf.Primary = true;

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 56, Padding = new Padding(22, 12, 22, 0), WrapContents = false,
            FlowDirection = L.Ar ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
        };
        var lblRange = new Label { Text = L.T("Range"), AutoSize = true, Margin = new Padding(0, 7, 8, 0) };
        var lblIface = new Label { Text = L.T("Interface"), AutoSize = true, Margin = new Padding(18, 7, 8, 0) };
        var spacer = new Panel { Width = 10, Height = 1 };
        cmbRange.Margin = cmbIface.Margin = new Padding(0, 1, 0, 0);
        btnPng.Margin = new Padding(18, 0, 0, 0);
        btnPdf.Margin = new Padding(8, 0, 0, 0);
        bar.Controls.AddRange([lblRange, cmbRange, lblIface, cmbIface, spacer, btnPng, btnPdf]);

        view.Theme = t;
        view.RightToLeft = RightToLeft;
        scroller.Controls.Add(view);
        Native.DarkScrollbars(scroller, t.Dark);
        Controls.Add(scroller);
        Controls.Add(bar);
        Styler.Apply(this, t);

        cmbRange.SelectedIndexChanged += (_, _) => Rebuild();
        cmbIface.SelectedIndexChanged += (_, _) => Rebuild();
        btnPng.Click += (_, _) => Export("png");
        btnPdf.Click += (_, _) => Export("pdf");
        scroller.Resize += (_, _) => { view.Width = scroller.ClientSize.Width; view.Relayout(); };
        refresh.Tick += (_, _) => Rebuild(keepScroll: true);
        refresh.Start();
        FormClosed += (_, _) => refresh.Dispose();
        Rebuild();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RoundCorners(Handle, t.Dark);
        int caption = ColorTranslator.ToWin32(t.Window);
        Native.DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int)); // DWMWA_CAPTION_COLOR
    }

    string FilterName => cmbIface.SelectedIndex <= 0 ? L.T("AllInterfaces") : cmbIface.Items[cmbIface.SelectedIndex];

    void Rebuild(bool keepScroll = false)
    {
        var range = Ranges[Math.Max(0, cmbRange.SelectedIndex)];
        var (from, to) = UsageReport.Resolve(range, DateOnly.FromDateTime(DateTime.Now), app.Ledger.FirstDay());
        current = UsageReport.Build(app.Ledger, from, to, ifaceIds[Math.Max(0, cmbIface.SelectedIndex)]);
        var pos = scroller.AutoScrollPosition;
        view.Width = scroller.ClientSize.Width;
        view.Show(current, FilterName);
        if (keepScroll) scroller.AutoScrollPosition = new Point(-pos.X, -pos.Y);
    }

    void Export(string ext)
    {
        if (current == null) return;
        using var dlg = new SaveFileDialog
        {
            FileName = ReportExporter.SuggestedName(current, ext),
            Filter = ext == "png" ? "PNG|*.png" : "PDF|*.pdf",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            Cursor = Cursors.WaitCursor;
            if (ext == "png") ReportExporter.SavePng(current, FilterName, dlg.FileName);
            else ReportExporter.SavePdf(current, FilterName, dlg.FileName);
            Cursor = Cursors.Default;
            if (MessageBox.Show(this, L.F("Exported", Path.GetFileName(dlg.FileName)), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Updater.OpenPage(dlg.FileName);
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            Log.Error(ex);
            MessageBox.Show(this, L.T("ExportFailed") + "\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
