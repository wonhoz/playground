using System.Drawing;

namespace PortWatch;

internal sealed class DarkMenuRenderer : ToolStripRenderer
{
    private static readonly Color _bg     = Color.FromArgb(28, 28, 42);
    private static readonly Color _border = Color.FromArgb(55, 55, 75);
    private static readonly Color _hover  = Color.FromArgb(48, 48, 70);
    private static readonly Color _text   = Color.FromArgb(220, 220, 235);
    private static readonly Color _sep    = Color.FromArgb(50, 50, 68);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(_bg);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
        using var pen = new Pen(_border);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var rc = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
        using var brush = new SolidBrush(e.Item.Selected ? _hover : _bg);
        e.Graphics.FillRectangle(brush, rc);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? _text : Color.FromArgb(90, 90, 110);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var y = e.Item.Height / 2;
        using var pen = new Pen(_sep);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }
}
