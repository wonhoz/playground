using System.Drawing;
using System.Drawing.Drawing2D;

namespace AiClip.Rendering
{
    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        private static readonly Color Background = Color.FromArgb(32, 32, 32);
        private static readonly Color Selected   = Color.FromArgb(55, 55, 55);
        private static readonly Color Border     = Color.FromArgb(60, 60, 60);
        private static readonly Color Separator  = Color.FromArgb(55, 55, 55);
        private static readonly Color TextNormal  = Color.FromArgb(240, 240, 240);
        private static readonly Color TextDisabled = Color.FromArgb(90, 90, 90);

        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var b = new SolidBrush(Background);
            e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var p = new Pen(Border);
            e.Graphics.DrawRectangle(p, new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1));
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            var rect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
            using var b = new SolidBrush(Selected);
            using var path = RoundRect(rect, 4);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(b, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? TextNormal : TextDisabled;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var p = new Pen(Separator);
            e.Graphics.DrawLine(p, 8, y, e.Item.Width - 8, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item?.Enabled == true ? TextNormal : TextDisabled;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            var d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        private static readonly Color Bg  = Color.FromArgb(32, 32, 32);
        private static readonly Color Bdr = Color.FromArgb(60, 60, 60);
        private static readonly Color Sel = Color.FromArgb(55, 55, 55);

        public override Color ToolStripDropDownBackground       => Bg;
        public override Color ImageMarginGradientBegin          => Bg;
        public override Color ImageMarginGradientMiddle         => Bg;
        public override Color ImageMarginGradientEnd            => Bg;
        public override Color MenuBorder                        => Bdr;
        public override Color MenuItemBorder                    => Color.Transparent;
        public override Color MenuItemSelected                  => Sel;
        public override Color MenuStripGradientBegin            => Bg;
        public override Color MenuStripGradientEnd              => Bg;
        public override Color MenuItemSelectedGradientBegin     => Sel;
        public override Color MenuItemSelectedGradientEnd       => Sel;
        public override Color MenuItemPressedGradientBegin      => Sel;
        public override Color MenuItemPressedGradientMiddle     => Sel;
        public override Color MenuItemPressedGradientEnd        => Sel;
        public override Color SeparatorDark                     => Bdr;
        public override Color SeparatorLight                    => Bdr;
        public override Color CheckBackground                   => Color.Transparent;
        public override Color CheckPressedBackground            => Color.Transparent;
        public override Color CheckSelectedBackground           => Color.Transparent;
    }
}
