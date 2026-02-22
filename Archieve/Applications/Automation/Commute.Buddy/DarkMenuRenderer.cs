using System.Drawing;
using System.Windows.Forms;

namespace CommuteBuddy;

internal class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var rc = new Rectangle(Point.Empty, e.Item.Size);
        var g = e.Graphics;

        if (e.Item.Selected && e.Item.Enabled)
        {
            using var brush = new SolidBrush(Color.FromArgb(55, 95, 170));
            using var pen   = new Pen(Color.FromArgb(75, 125, 200));
            g.FillRectangle(brush, rc);
            g.DrawRectangle(pen, Rectangle.Inflate(rc, -1, -1));
        }
        else
        {
            using var brush = new SolidBrush(Color.FromArgb(28, 28, 38));
            g.FillRectangle(brush, rc);
        }
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        if (e.AffectedBounds.Width <= 0 || e.AffectedBounds.Height <= 0) return;
        using var brush = new SolidBrush(Color.FromArgb(28, 28, 38));
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var rc = e.Item.Bounds;
        using var pen = new Pen(Color.FromArgb(55, 55, 75));
        e.Graphics.DrawLine(pen, rc.Left + 4, rc.Height / 2, rc.Right - 4, rc.Height / 2);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled
            ? (e.Item.Selected ? Color.White : Color.FromArgb(220, 220, 230))
            : Color.FromArgb(100, 100, 110);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Color.FromArgb(160, 160, 180);
        base.OnRenderArrow(e);
    }
}

internal class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(55, 95, 170);
    public override Color MenuItemSelectedGradientEnd   => Color.FromArgb(55, 95, 170);
    public override Color MenuBorder                    => Color.FromArgb(55, 55, 75);
    public override Color ToolStripDropDownBackground   => Color.FromArgb(28, 28, 38);
    public override Color ImageMarginGradientBegin      => Color.FromArgb(28, 28, 38);
    public override Color ImageMarginGradientMiddle     => Color.FromArgb(28, 28, 38);
    public override Color ImageMarginGradientEnd        => Color.FromArgb(28, 28, 38);
    public override Color MenuItemBorder                => Color.FromArgb(75, 125, 200);
    public override Color SeparatorDark                 => Color.FromArgb(55, 55, 75);
    public override Color SeparatorLight                => Color.FromArgb(55, 55, 75);
    public override Color MenuStripGradientBegin        => Color.FromArgb(28, 28, 38);
    public override Color MenuStripGradientEnd          => Color.FromArgb(28, 28, 38);
}
