using System.Drawing.Drawing2D;

namespace WiFiCast.Views;

public class DarkMenuRenderer : ToolStripRenderer
{
    private static readonly Color BgColor  = Color.FromArgb(28, 28, 28);
    private static readonly Color SelColor = Color.FromArgb(50, 50, 50);
    private static readonly Color SepColor = Color.FromArgb(60, 60, 60);
    private static readonly Color TxtColor = Color.FromArgb(224, 224, 224);
    private static readonly Color TxtDim   = Color.FromArgb(120, 120, 120);
    private static readonly Color ChkColor = Color.FromArgb(76, 175, 80);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.Clear(BgColor);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(SepColor);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        var r = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
        using var b = new SolidBrush(SelColor);
        using var path = RoundRect(r, 4);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(b, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? TxtColor : TxtDim;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var b = new SolidBrush(SepColor);
        e.Graphics.FillRectangle(b, 10, y, e.Item.Width - 20, 1);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled == true ? TxtColor : TxtDim;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var r = e.ImageRectangle;
        r.Inflate(-2, -2);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(ChkColor, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        e.Graphics.DrawLine(pen, cx - 4, cy, cx - 1, cy + 3);
        e.Graphics.DrawLine(pen, cx - 1, cy + 3, cx + 4, cy - 3);
    }

    private static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
