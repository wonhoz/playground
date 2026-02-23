using System.Drawing;
using System.Drawing.Drawing2D;

namespace ToastCast;

public class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color BgColor = Color.FromArgb(26, 26, 36);
    private static readonly Color HoverColor = Color.FromArgb(50, 50, 70);
    private static readonly Color BorderColor = Color.FromArgb(55, 55, 75);
    private static readonly Color SepColor = Color.FromArgb(50, 50, 70);
    private static readonly Color TxtColor = Color.FromArgb(230, 230, 235);
    private static readonly Color DisabledColor = Color.FromArgb(110, 110, 130);
    private static readonly Color CheckColor = Color.FromArgb(100, 220, 150);

    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(BgColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(BorderColor);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);

        if (e.Item.Selected && e.Item.Enabled)
        {
            using var brush = new SolidBrush(HoverColor);
            using var path = RoundRect(rect, 4);
            g.FillPath(brush, path);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? TxtColor : DisabledColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var brush = new SolidBrush(SepColor);
        e.Graphics.FillRectangle(brush, 12, e.Item.Height / 2, e.Item.Width - 24, 1);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = e.ImageRectangle;
        using var pen = new Pen(CheckColor, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var cx = r.X + r.Width / 2;
        var cy = r.Y + r.Height / 2;
        g.DrawLine(pen, cx - 4, cy, cx - 1, cy + 3);
        g.DrawLine(pen, cx - 1, cy + 3, cx + 4, cy - 3);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == true ? TxtColor : DisabledColor;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

    private static GraphicsPath RoundRect(Rectangle rect, int r)
    {
        var path = new GraphicsPath();
        var d = r * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public class DarkColorTable : ProfessionalColorTable
{
    private static readonly Color Bg = Color.FromArgb(26, 26, 36);
    private static readonly Color Bd = Color.FromArgb(55, 55, 75);
    private static readonly Color Sel = Color.FromArgb(50, 50, 70);

    public override Color ToolStripDropDownBackground => Bg;
    public override Color ImageMarginGradientBegin => Bg;
    public override Color ImageMarginGradientMiddle => Bg;
    public override Color ImageMarginGradientEnd => Bg;
    public override Color MenuBorder => Bd;
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => Sel;
    public override Color MenuStripGradientBegin => Bg;
    public override Color MenuStripGradientEnd => Bg;
    public override Color MenuItemSelectedGradientBegin => Sel;
    public override Color MenuItemSelectedGradientEnd => Sel;
    public override Color MenuItemPressedGradientBegin => Sel;
    public override Color MenuItemPressedGradientMiddle => Sel;
    public override Color MenuItemPressedGradientEnd => Sel;
    public override Color SeparatorDark => Bd;
    public override Color SeparatorLight => Bd;
    public override Color CheckBackground => Color.Transparent;
    public override Color CheckPressedBackground => Color.Transparent;
    public override Color CheckSelectedBackground => Color.Transparent;
}
