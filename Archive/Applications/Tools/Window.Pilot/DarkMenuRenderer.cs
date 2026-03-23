using System.Drawing;
using System.Drawing.Drawing2D;

namespace WindowPilot;

public class DarkMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
{
    private static readonly Color Bg       = Color.FromArgb(32, 32, 32);
    private static readonly Color Selected = Color.FromArgb(55, 55, 55);
    private static readonly Color Border   = Color.FromArgb(60, 60, 60);
    private static readonly Color TextCol  = Color.FromArgb(240, 240, 240);
    private static readonly Color Disabled = Color.FromArgb(128, 128, 128);

    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderToolStripBackground(System.Windows.Forms.ToolStripRenderEventArgs e)
    { using var b = new SolidBrush(Bg); e.Graphics.FillRectangle(b, e.AffectedBounds); }

    protected override void OnRenderToolStripBorder(System.Windows.Forms.ToolStripRenderEventArgs e)
    { using var p = new Pen(Border); e.Graphics.DrawRectangle(p, new Rectangle(0, 0, e.ToolStrip.Width-1, e.ToolStrip.Height-1)); }

    protected override void OnRenderMenuItemBackground(System.Windows.Forms.ToolStripItemRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(4, 2, e.Item.Width-8, e.Item.Height-4);
        if (e.Item.Selected && e.Item.Enabled)
        { using var b = new SolidBrush(Selected); using var path = Rnd(r,4); g.FillPath(b, path); }
    }

    protected override void OnRenderItemText(System.Windows.Forms.ToolStripItemTextRenderEventArgs e)
    { e.TextColor = e.Item.Enabled ? TextCol : Disabled; base.OnRenderItemText(e); }

    protected override void OnRenderSeparator(System.Windows.Forms.ToolStripSeparatorRenderEventArgs e)
    { using var b = new SolidBrush(Border); e.Graphics.FillRectangle(b, new Rectangle(12, e.Item.Height/2, e.Item.Width-24, 1)); }

    protected override void OnRenderArrow(System.Windows.Forms.ToolStripArrowRenderEventArgs e)
    { e.ArrowColor = e.Item?.Enabled == true ? TextCol : Disabled; base.OnRenderArrow(e); }

    protected override void OnRenderImageMargin(System.Windows.Forms.ToolStripRenderEventArgs e) { }

    private static GraphicsPath Rnd(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right-d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right-d, r.Bottom-d, d, d, 0, 90); p.AddArc(r.X, r.Bottom-d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
}

public class DarkColorTable : System.Windows.Forms.ProfessionalColorTable
{
    private static readonly Color Bg = Color.FromArgb(32,32,32), Border = Color.FromArgb(60,60,60), Sel = Color.FromArgb(55,55,55);
    public override Color ToolStripDropDownBackground => Bg;
    public override Color ImageMarginGradientBegin => Bg; public override Color ImageMarginGradientMiddle => Bg; public override Color ImageMarginGradientEnd => Bg;
    public override Color MenuBorder => Border; public override Color MenuItemBorder => Color.Transparent; public override Color MenuItemSelected => Sel;
    public override Color MenuStripGradientBegin => Bg; public override Color MenuStripGradientEnd => Bg;
    public override Color MenuItemSelectedGradientBegin => Sel; public override Color MenuItemSelectedGradientEnd => Sel;
    public override Color MenuItemPressedGradientBegin => Sel; public override Color MenuItemPressedGradientMiddle => Sel; public override Color MenuItemPressedGradientEnd => Sel;
    public override Color SeparatorDark => Border; public override Color SeparatorLight => Border;
    public override Color CheckBackground => Color.Transparent; public override Color CheckPressedBackground => Color.Transparent; public override Color CheckSelectedBackground => Color.Transparent;
}
