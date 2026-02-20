using System.Drawing;
using System.Drawing.Drawing2D;

namespace AmbientMixer;

public class DarkMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
{
    private static readonly Color BackgroundColor       = Color.FromArgb(32, 32, 32);
    private static readonly Color MenuItemSelectedColor = Color.FromArgb(55, 55, 55);
    private static readonly Color MenuItemPressedColor  = Color.FromArgb(70, 70, 70);
    private static readonly Color BorderColor           = Color.FromArgb(60, 60, 60);
    private static readonly Color SeparatorColor        = Color.FromArgb(60, 60, 60);
    private static readonly Color TextColor             = Color.FromArgb(240, 240, 240);
    private static readonly Color DisabledTextColor     = Color.FromArgb(128, 128, 128);

    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderToolStripBackground(System.Windows.Forms.ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(BackgroundColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(System.Windows.Forms.ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(BorderColor);
        e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1));
    }

    protected override void OnRenderMenuItemBackground(System.Windows.Forms.ToolStripItemRenderEventArgs e)
    {
        var g    = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);

        if (e.Item.Selected && e.Item.Enabled)
        {
            using var brush = new SolidBrush(MenuItemSelectedColor);
            using var path  = RoundedRect(rect, 4);
            g.FillPath(brush, path);
        }
        else if (e.Item.Pressed)
        {
            using var brush = new SolidBrush(MenuItemPressedColor);
            using var path  = RoundedRect(rect, 4);
            g.FillPath(brush, path);
        }
    }

    protected override void OnRenderItemText(System.Windows.Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? TextColor : DisabledTextColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(System.Windows.Forms.ToolStripSeparatorRenderEventArgs e)
    {
        using var brush = new SolidBrush(SeparatorColor);
        e.Graphics.FillRectangle(brush, new Rectangle(12, e.Item.Height / 2, e.Item.Width - 24, 1));
    }

    protected override void OnRenderArrow(System.Windows.Forms.ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == true ? TextColor : DisabledTextColor;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderImageMargin(System.Windows.Forms.ToolStripRenderEventArgs e) { }

    private static GraphicsPath RoundedRect(Rectangle rect, int r)
    {
        int d = r * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X,          rect.Y,          d, d, 180, 90);
        path.AddArc(rect.Right - d,  rect.Y,          d, d, 270, 90);
        path.AddArc(rect.Right - d,  rect.Bottom - d, d, d,   0, 90);
        path.AddArc(rect.X,          rect.Bottom - d, d, d,  90, 90);
        path.CloseFigure();
        return path;
    }
}

public class DarkColorTable : System.Windows.Forms.ProfessionalColorTable
{
    private static readonly Color Bg       = Color.FromArgb(32, 32, 32);
    private static readonly Color Border   = Color.FromArgb(60, 60, 60);
    private static readonly Color Selected = Color.FromArgb(55, 55, 55);

    public override Color ToolStripDropDownBackground          => Bg;
    public override Color ImageMarginGradientBegin             => Bg;
    public override Color ImageMarginGradientMiddle            => Bg;
    public override Color ImageMarginGradientEnd               => Bg;
    public override Color MenuBorder                           => Border;
    public override Color MenuItemBorder                       => Color.Transparent;
    public override Color MenuItemSelected                     => Selected;
    public override Color MenuStripGradientBegin               => Bg;
    public override Color MenuStripGradientEnd                 => Bg;
    public override Color MenuItemSelectedGradientBegin        => Selected;
    public override Color MenuItemSelectedGradientEnd          => Selected;
    public override Color MenuItemPressedGradientBegin         => Selected;
    public override Color MenuItemPressedGradientMiddle        => Selected;
    public override Color MenuItemPressedGradientEnd           => Selected;
    public override Color SeparatorDark                        => Border;
    public override Color SeparatorLight                       => Border;
    public override Color CheckBackground                      => Color.Transparent;
    public override Color CheckPressedBackground               => Color.Transparent;
    public override Color CheckSelectedBackground              => Color.Transparent;
}
