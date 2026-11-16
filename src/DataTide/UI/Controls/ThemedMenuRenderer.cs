namespace DataTide.UI.Controls;

/// <summary>Menus in the app theme (the stock ones stay light in dark mode).</summary>
sealed class ThemedMenuRenderer(Theme t) : ToolStripProfessionalRenderer(new Colors(t))
{
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item.ForeColor == SystemColors.ControlText) e.TextColor = t.Text;
        base.OnRenderItemText(e);
    }

    sealed class Colors(Theme t) : ProfessionalColorTable
    {
        Color Hover => Color.FromArgb(t.Dark ? 60 : 30, t.Accent);
        public override Color ToolStripDropDownBackground => t.Surface;
        public override Color MenuBorder => t.Border;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color ImageMarginGradientBegin => t.Surface;
        public override Color ImageMarginGradientMiddle => t.Surface;
        public override Color ImageMarginGradientEnd => t.Surface;
        public override Color SeparatorDark => t.Border;
        public override Color SeparatorLight => t.Surface;
    }
}
