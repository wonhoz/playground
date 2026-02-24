using System.Drawing;
using System.Drawing.Drawing2D;

namespace ToastCast;

/// <summary>
/// ToolStripRenderer 직접 상속으로 이미지 마진 공간 완전 제거.
/// ProfessionalRenderer 상속 시 ShowImageMargin=false 해도 내부 레이아웃에서 공간을 예약해 짜부러짐 발생.
/// </summary>
public class DarkMenuRenderer : ToolStripRenderer
{
    private static readonly Color BgColor      = Color.FromArgb(26, 26, 36);
    private static readonly Color HoverColor   = Color.FromArgb(50, 50, 70);
    private static readonly Color BorderColor  = Color.FromArgb(55, 55, 75);
    private static readonly Color SepColor     = Color.FromArgb(50, 50, 70);
    private static readonly Color TxtColor     = Color.FromArgb(230, 230, 235);
    private static readonly Color DisabledColor = Color.FromArgb(110, 110, 130);
    private static readonly Color CheckColor   = Color.FromArgb(100, 220, 150);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(BgColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(BorderColor);
        var r = e.ToolStrip.ClientRectangle;
        r.Width--;
        r.Height--;
        e.Graphics.DrawRoundedRectangle(pen, r, 6);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
        using var brush = new SolidBrush(HoverColor);
        using var path = MakeRoundRect(rect, 4);
        g.FillPath(brush, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? TxtColor : DisabledColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(SepColor);
        e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = e.ImageRectangle;
        int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        using var pen = new Pen(CheckColor, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, cx - 4, cy, cx - 1, cy + 3);
        g.DrawLine(pen, cx - 1, cy + 3, cx + 4, cy - 3);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == true ? TxtColor : DisabledColor;
        base.OnRenderArrow(e);
    }

    private static GraphicsPath MakeRoundRect(Rectangle rect, int r)
    {
        var path = new GraphicsPath();
        path.AddRoundedRectangle(rect, r);
        return path;
    }
}
