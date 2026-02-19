using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace AiClip
{
    public static class IconGenerator
    {
        public static Icon CreateTrayIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            // Orange rounded rectangle background
            using var bgBrush = new SolidBrush(Color.FromArgb(255, 107, 53));
            using var path = CreateRoundedRect(new Rectangle(1, 1, 29, 29), 6);
            g.FillPath(bgBrush, path);

            // "AI" text in white
            using var font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString("AI", font, textBrush, new RectangleF(1, 0, 29, 31), sf);

            return Icon.FromHandle(bmp.GetHicon());
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
