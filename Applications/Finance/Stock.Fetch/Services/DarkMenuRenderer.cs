using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Stock.Fetch.Services;

/// <summary>트레이 우클릭 메뉴 다크 테마 렌더러(Stay.Awake에서 포팅).</summary>
public sealed class DarkMenuRenderer : ToolStripRenderer
{
    private static readonly Color BackgroundColor = Color.FromArgb(32, 32, 32);
    private static readonly Color SelectedColor = Color.FromArgb(55, 55, 55);
    private static readonly Color PressedColor = Color.FromArgb(70, 70, 70);
    private static readonly Color BorderColor = Color.FromArgb(60, 60, 60);
    private static readonly Color SeparatorColor = Color.FromArgb(60, 60, 60);
    private static readonly Color TextColor = Color.FromArgb(240, 240, 240);
    private static readonly Color DisabledTextColor = Color.FromArgb(128, 128, 128);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(BackgroundColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(BorderColor);
        e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1));
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
        if (e.Item.Selected && e.Item.Enabled)
        {
            using var brush = new SolidBrush(SelectedColor);
            using var path = Rounded(rect, 4);
            g.FillPath(brush, path);
        }
        else if (e.Item.Pressed)
        {
            using var brush = new SolidBrush(PressedColor);
            using var path = Rounded(rect, 4);
            g.FillPath(brush, path);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? TextColor : DisabledTextColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var brush = new SolidBrush(SeparatorColor);
        e.Graphics.FillRectangle(brush, new Rectangle(12, e.Item.Height / 2, e.Item.Width - 24, 1));
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { /* 깔끔한 다크 배경 유지 */ }

    private static GraphicsPath Rounded(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
