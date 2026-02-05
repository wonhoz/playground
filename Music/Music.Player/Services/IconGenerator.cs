using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace Music.Player.Services
{
    public static class IconGenerator
    {
        public static void GenerateAppIcon(string outputPath)
        {
            Directory.CreateDirectory(outputPath);
            var icoPath = Path.Combine(outputPath, "app.ico");

            // Generate multiple sizes for ICO
            var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
            var images = new List<Bitmap>();

            foreach (var size in sizes)
            {
                images.Add(CreateMusicIcon(size));
            }

            SaveAsIco(images, icoPath);

            foreach (var img in images)
                img.Dispose();
        }

        private static Bitmap CreateMusicIcon(int size)
        {
            var bitmap = new Bitmap(size, size);
            using var g = Graphics.FromImage(bitmap);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Background - Dark gradient circle
            var bgRect = new Rectangle(0, 0, size, size);
            using (var bgPath = new GraphicsPath())
            {
                bgPath.AddEllipse(bgRect);
                using var bgBrush = new LinearGradientBrush(
                    bgRect,
                    Color.FromArgb(255, 45, 45, 45),
                    Color.FromArgb(255, 30, 30, 30),
                    LinearGradientMode.ForwardDiagonal);
                g.FillPath(bgBrush, bgPath);
            }

            // Orange accent ring
            var ringWidth = size * 0.08f;
            var ringRect = new RectangleF(ringWidth / 2, ringWidth / 2, size - ringWidth, size - ringWidth);
            using (var ringPen = new Pen(Color.FromArgb(255, 255, 107, 53), ringWidth))
            {
                g.DrawEllipse(ringPen, ringRect);
            }

            // Music note
            var noteColor = Color.FromArgb(255, 255, 107, 53); // Orange accent
            var scale = size / 24f;
            var offsetX = size * 0.25f;
            var offsetY = size * 0.2f;

            using (var noteBrush = new SolidBrush(noteColor))
            {
                // Note head (ellipse)
                var headWidth = 5f * scale;
                var headHeight = 4f * scale;
                var headX = offsetX + 2f * scale;
                var headY = offsetY + 12f * scale;
                g.FillEllipse(noteBrush, headX, headY, headWidth, headHeight);

                // Note stem
                var stemX = headX + headWidth - 1.5f * scale;
                var stemY = offsetY + 2f * scale;
                var stemWidth = 2f * scale;
                var stemHeight = 12f * scale;
                g.FillRectangle(noteBrush, stemX, stemY, stemWidth, stemHeight);

                // Note flag
                using var flagPath = new GraphicsPath();
                var flagPoints = new PointF[]
                {
                    new(stemX + stemWidth, stemY),
                    new(stemX + stemWidth + 6f * scale, stemY + 4f * scale),
                    new(stemX + stemWidth + 4f * scale, stemY + 8f * scale),
                    new(stemX + stemWidth, stemY + 4f * scale)
                };
                flagPath.AddPolygon(flagPoints);
                g.FillPath(noteBrush, flagPath);
            }

            // Add subtle highlight
            using (var highlightBrush = new LinearGradientBrush(
                new Rectangle(0, 0, size, size / 2),
                Color.FromArgb(30, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                LinearGradientMode.Vertical))
            {
                using var highlightPath = new GraphicsPath();
                highlightPath.AddEllipse(size * 0.1f, size * 0.05f, size * 0.8f, size * 0.4f);
                g.FillPath(highlightBrush, highlightPath);
            }

            return bitmap;
        }

        private static void SaveAsIco(List<Bitmap> images, string path)
        {
            using var fs = new FileStream(path, FileMode.Create);
            using var bw = new BinaryWriter(fs);

            // ICO Header
            bw.Write((short)0);           // Reserved
            bw.Write((short)1);           // Type: 1 = ICO
            bw.Write((short)images.Count); // Number of images

            var imageDataList = new List<byte[]>();
            var offset = 6 + (16 * images.Count); // Header + directory entries

            // Directory entries
            foreach (var img in images)
            {
                using var ms = new MemoryStream();
                img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                var data = ms.ToArray();
                imageDataList.Add(data);

                bw.Write((byte)(img.Width >= 256 ? 0 : img.Width));
                bw.Write((byte)(img.Height >= 256 ? 0 : img.Height));
                bw.Write((byte)0);        // Color palette
                bw.Write((byte)0);        // Reserved
                bw.Write((short)1);       // Color planes
                bw.Write((short)32);      // Bits per pixel
                bw.Write(data.Length);    // Image data size
                bw.Write(offset);         // Image data offset

                offset += data.Length;
            }

            // Image data
            foreach (var data in imageDataList)
            {
                bw.Write(data);
            }
        }
    }
}
