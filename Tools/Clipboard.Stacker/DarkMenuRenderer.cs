using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClipboardStacker;

public sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color BgColor       = Color.FromArgb(28, 28, 40);
    private static readonly Color HoverColor    = Color.FromArgb(55, 55, 80);
    private static readonly Color TextColor     = Color.FromArgb(224, 224, 224);
    private static readonly Color SeparatorColor = Color.FromArgb(55, 55, 70);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(BgColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        var g    = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
        using var brush = new SolidBrush(HoverColor);
        using var path  = RoundedRect(rect, 4);
        g.FillPath(brush, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? TextColor : Color.FromArgb(80, 80, 80);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(SeparatorColor);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, radius * 2, radius * 2, 180, 90);
        path.AddArc(r.Right - radius * 2, r.Top, radius * 2, radius * 2, 270, 90);
        path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(r.Left, r.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        return path;
    }
}
