using System.Drawing;
using System.Drawing.Drawing2D;

namespace StayAwake
{
    /// <summary>
    /// 다크 모던 테마 컨텍스트 메뉴 렌더러
    /// </summary>
    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        // 다크 테마 컬러
        private static readonly Color BackgroundColor = Color.FromArgb(32, 32, 32);
        private static readonly Color MenuItemSelectedColor = Color.FromArgb(55, 55, 55);
        private static readonly Color MenuItemPressedColor = Color.FromArgb(70, 70, 70);
        private static readonly Color BorderColor = Color.FromArgb(60, 60, 60);
        private static readonly Color SeparatorColor = Color.FromArgb(60, 60, 60);
        private static readonly Color TextColor = Color.FromArgb(240, 240, 240);
        private static readonly Color DisabledTextColor = Color.FromArgb(128, 128, 128);
        private static readonly Color CheckMarkColor = Color.FromArgb(76, 175, 80);  // Green
        private static readonly Color AccentColor = Color.FromArgb(66, 133, 244);    // Blue

        public DarkMenuRenderer() : base(new DarkColorTable())
        {
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(BackgroundColor);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(BorderColor);
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            e.Graphics.DrawRectangle(pen, rect);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);

            if (e.Item.Selected && e.Item.Enabled)
            {
                using var brush = new SolidBrush(MenuItemSelectedColor);
                using var path = CreateRoundedRect(rect, 4);
                g.FillPath(brush, path);
            }
            else if (e.Item.Pressed)
            {
                using var brush = new SolidBrush(MenuItemPressedColor);
                using var path = CreateRoundedRect(rect, 4);
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
            var g = e.Graphics;
            var rect = new Rectangle(12, e.Item.Height / 2, e.Item.Width - 24, 1);
            using var brush = new SolidBrush(SeparatorColor);
            g.FillRectangle(brush, rect);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(e.ImageRectangle.X + 2, e.ImageRectangle.Y + 2,
                                     e.ImageRectangle.Width - 4, e.ImageRectangle.Height - 4);

            // 체크 배경
            using (var bgBrush = new SolidBrush(Color.FromArgb(40, CheckMarkColor)))
            using (var path = CreateRoundedRect(rect, 3))
            {
                g.FillPath(bgBrush, path);
            }

            // 체크 마크
            using var pen = new Pen(CheckMarkColor, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var cx = rect.X + rect.Width / 2;
            var cy = rect.Y + rect.Height / 2;
            g.DrawLine(pen, cx - 4, cy, cx - 1, cy + 3);
            g.DrawLine(pen, cx - 1, cy + 3, cx + 4, cy - 3);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item?.Enabled == true ? TextColor : DisabledTextColor;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // 이미지 마진 배경 제거 (깔끔한 다크 배경 유지)
        }

        private static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }
    }

    /// <summary>
    /// 다크 테마 컬러 테이블
    /// </summary>
    public class DarkColorTable : ProfessionalColorTable
    {
        private static readonly Color DarkBg = Color.FromArgb(32, 32, 32);
        private static readonly Color DarkBorder = Color.FromArgb(60, 60, 60);
        private static readonly Color DarkSelected = Color.FromArgb(55, 55, 55);

        public override Color ToolStripDropDownBackground => DarkBg;
        public override Color ImageMarginGradientBegin => DarkBg;
        public override Color ImageMarginGradientMiddle => DarkBg;
        public override Color ImageMarginGradientEnd => DarkBg;
        public override Color MenuBorder => DarkBorder;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => DarkSelected;
        public override Color MenuStripGradientBegin => DarkBg;
        public override Color MenuStripGradientEnd => DarkBg;
        public override Color MenuItemSelectedGradientBegin => DarkSelected;
        public override Color MenuItemSelectedGradientEnd => DarkSelected;
        public override Color MenuItemPressedGradientBegin => DarkSelected;
        public override Color MenuItemPressedGradientMiddle => DarkSelected;
        public override Color MenuItemPressedGradientEnd => DarkSelected;
        public override Color SeparatorDark => DarkBorder;
        public override Color SeparatorLight => DarkBorder;
        public override Color CheckBackground => Color.Transparent;
        public override Color CheckPressedBackground => Color.Transparent;
        public override Color CheckSelectedBackground => Color.Transparent;
    }
}
