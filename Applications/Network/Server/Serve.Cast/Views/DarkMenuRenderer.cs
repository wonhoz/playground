using System.Drawing;
using System.Windows.Forms;
using SysColor = System.Drawing.Color;

namespace ServeCast.Views;

/// <summary>트레이 컨텍스트 메뉴 다크 렌더러
/// ToolStripRenderer 직접 상속 필수 — Professional 상속 시 이미지 마진 공간 예약으로 짜부러짐 발생
/// </summary>
public sealed class DarkMenuRenderer : ToolStripRenderer
{
    private static readonly SysColor BgColor     = SysColor.FromArgb(0x25, 0x25, 0x35);
    private static readonly SysColor HovColor    = SysColor.FromArgb(0x1E, 0x2A, 0x40);
    private static readonly SysColor TextColor   = SysColor.FromArgb(0xCD, 0xD6, 0xF4);
    private static readonly SysColor BorderColor = SysColor.FromArgb(0x31, 0x32, 0x44);
    private static readonly SysColor SepColor    = SysColor.FromArgb(0x31, 0x32, 0x44);
    private static readonly SysColor AccentColor = SysColor.FromArgb(0x89, 0xB4, 0xFA);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var br = new SolidBrush(BgColor);
        e.Graphics.FillRoundedRectangle(br, e.AffectedBounds, 6);

        using var pen = new Pen(BorderColor);
        var rect = new Rectangle(e.AffectedBounds.X, e.AffectedBounds.Y,
                                 e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
        e.Graphics.DrawRoundedRectangle(pen, rect, 6);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected && !e.Item.Pressed) return;
        var rect = new Rectangle(3, 0, e.Item.Width - 6, e.Item.Height);
        using var br = new SolidBrush(HovColor);
        e.Graphics.FillRoundedRectangle(br, rect, 4);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? TextColor : SysColor.FromArgb(0x7F, 0x84, 0x9C);
        e.TextFont  = new Font("Segoe UI", 9.5f);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(SepColor);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = AccentColor;
        base.OnRenderArrow(e);
    }
}

// ── Graphics 확장 메서드 ────────────────────────────────────────────────────
file static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius)
    {
        using var path = GetRoundedPath(rect, radius);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rect, int radius)
    {
        using var path = GetRoundedPath(rect, radius);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.DrawPath(pen, path);
    }

    private static System.Drawing.Drawing2D.GraphicsPath GetRoundedPath(Rectangle r, int rad)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
        path.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
        path.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
        path.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
        path.CloseFigure();
        return path;
    }
}
