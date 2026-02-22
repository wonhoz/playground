using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Photo.Video.Organizer
{
    /// <summary>
    /// Photo Video Organizer 앱 아이콘 생성기
    /// 사진/동영상 정리 컨셉: 카메라 + 폴더 + 캘린더
    /// </summary>
    public static class IconGenerator
    {
        // 컬러 팔레트
        private static readonly Color PrimaryBlue = Color.FromArgb(66, 133, 244);       // Google Blue
        private static readonly Color SecondaryGreen = Color.FromArgb(52, 168, 83);     // Green
        private static readonly Color AccentYellow = Color.FromArgb(251, 188, 4);       // Yellow
        private static readonly Color AccentRed = Color.FromArgb(234, 67, 53);          // Red
        private static readonly Color DarkGray = Color.FromArgb(66, 66, 66);
        private static readonly Color LightGray = Color.FromArgb(240, 240, 240);

        /// <summary>
        /// ICO 파일 생성 (여러 크기 포함)
        /// </summary>
        public static void CreateIconFile(string filePath)
        {
            var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
            var images = sizes.Select(size => CreateOrganizerIcon(size)).ToList();

            SaveAsIco(images, filePath);

            foreach (var img in images)
                img.Dispose();
        }

        /// <summary>
        /// 앱 아이콘 비트맵 생성
        /// 디자인: 사진들이 날짜별 폴더로 정리되는 이미지
        /// </summary>
        public static Bitmap CreateOrganizerIcon(int size)
        {
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float scale = size / 64f;
            g.Clear(Color.Transparent);

            // 배경 원 (그라데이션)
            using (var bgBrush = new LinearGradientBrush(
                new Rectangle(0, 0, size, size),
                Color.FromArgb(79, 195, 247),  // Light Blue
                PrimaryBlue,
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillEllipse(bgBrush, 2 * scale, 2 * scale, 60 * scale, 60 * scale);
            }

            // 폴더 아이콘 (왼쪽 하단)
            DrawFolder(g, scale, 8, 32, SecondaryGreen);

            // 사진 아이콘들 (겹쳐서)
            DrawPhoto(g, scale, 28, 14, Color.White, AccentYellow);
            DrawPhoto(g, scale, 24, 18, Color.White, AccentRed);

            // 화살표 (정리 방향)
            DrawArrow(g, scale, 36, 36);

            // 캘린더/날짜 표시 (우하단)
            DrawCalendar(g, scale, 38, 40);

            // 테두리 (미묘한 그림자 효과)
            using (var borderPen = new Pen(Color.FromArgb(40, 0, 0, 0), 1 * scale))
            {
                g.DrawEllipse(borderPen, 2 * scale, 2 * scale, 60 * scale, 60 * scale);
            }

            return bitmap;
        }

        /// <summary>
        /// 폴더 아이콘 그리기
        /// </summary>
        private static void DrawFolder(Graphics g, float scale, float x, float y, Color color)
        {
            using var brush = new SolidBrush(color);
            using var darkBrush = new SolidBrush(ControlPaint.Dark(color, 0.2f));

            // 폴더 탭
            var tabPoints = new PointF[]
            {
                new(x * scale, (y + 2) * scale),
                new((x + 8) * scale, (y + 2) * scale),
                new((x + 10) * scale, y * scale),
                new((x + 4) * scale, y * scale),
            };
            g.FillPolygon(darkBrush, tabPoints);

            // 폴더 몸체
            var bodyRect = new RectangleF(x * scale, (y + 2) * scale, 20 * scale, 16 * scale);
            using (var path = CreateRoundedRect(bodyRect, 2 * scale))
            {
                g.FillPath(brush, path);
            }
        }

        /// <summary>
        /// 사진 아이콘 그리기
        /// </summary>
        private static void DrawPhoto(Graphics g, float scale, float x, float y, Color bgColor, Color accentColor)
        {
            // 사진 프레임
            var frameRect = new RectangleF(x * scale, y * scale, 20 * scale, 16 * scale);
            using (var frameBrush = new SolidBrush(bgColor))
            using (var path = CreateRoundedRect(frameRect, 2 * scale))
            {
                g.FillPath(frameBrush, path);
            }

            // 산 (풍경 이미지 표현)
            using var mountainBrush = new SolidBrush(accentColor);
            var mountainPoints = new PointF[]
            {
                new((x + 3) * scale, (y + 13) * scale),
                new((x + 8) * scale, (y + 6) * scale),
                new((x + 12) * scale, (y + 10) * scale),
                new((x + 17) * scale, (y + 5) * scale),
                new((x + 17) * scale, (y + 13) * scale),
            };
            g.FillPolygon(mountainBrush, mountainPoints);

            // 태양
            using var sunBrush = new SolidBrush(AccentYellow);
            g.FillEllipse(sunBrush, (x + 14) * scale, (y + 3) * scale, 4 * scale, 4 * scale);

            // 테두리
            using var borderPen = new Pen(Color.FromArgb(80, 0, 0, 0), 0.5f * scale);
            using (var path = CreateRoundedRect(frameRect, 2 * scale))
            {
                g.DrawPath(borderPen, path);
            }
        }

        /// <summary>
        /// 화살표 그리기
        /// </summary>
        private static void DrawArrow(Graphics g, float scale, float x, float y)
        {
            using var pen = new Pen(Color.White, 2.5f * scale)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.ArrowAnchor
            };

            g.DrawLine(pen, (x - 6) * scale, y * scale, (x + 2) * scale, (y + 6) * scale);
        }

        /// <summary>
        /// 캘린더 아이콘 그리기
        /// </summary>
        private static void DrawCalendar(Graphics g, float scale, float x, float y)
        {
            // 캘린더 배경
            var calRect = new RectangleF(x * scale, y * scale, 14 * scale, 12 * scale);
            using (var calBrush = new SolidBrush(Color.White))
            using (var path = CreateRoundedRect(calRect, 2 * scale))
            {
                g.FillPath(calBrush, path);
            }

            // 캘린더 헤더 (빨간색)
            using (var headerBrush = new SolidBrush(AccentRed))
            {
                var headerRect = new RectangleF(x * scale, y * scale, 14 * scale, 4 * scale);
                using var path = CreateRoundedRect(headerRect, 2 * scale, true, true, false, false);
                g.FillPath(headerBrush, path);
            }

            // 날짜 격자
            using var gridPen = new Pen(Color.FromArgb(150, 100, 100, 100), 0.5f * scale);
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    g.DrawRectangle(gridPen,
                        (x + 2 + i * 4) * scale,
                        (y + 5 + j * 2) * scale,
                        3 * scale, 1.5f * scale);
                }
            }

            // 테두리
            using var borderPen = new Pen(Color.FromArgb(80, 0, 0, 0), 0.5f * scale);
            using (var path = CreateRoundedRect(calRect, 2 * scale))
            {
                g.DrawPath(borderPen, path);
            }
        }

        private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius,
            bool topLeft = true, bool topRight = true, bool bottomRight = true, bool bottomLeft = true)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2;

            if (topLeft)
                path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            else
                path.AddLine(rect.X, rect.Y, rect.X, rect.Y);

            if (topRight)
                path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            else
                path.AddLine(rect.Right, rect.Y, rect.Right, rect.Y);

            if (bottomRight)
                path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            else
                path.AddLine(rect.Right, rect.Bottom, rect.Right, rect.Bottom);

            if (bottomLeft)
                path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            else
                path.AddLine(rect.X, rect.Bottom, rect.X, rect.Bottom);

            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// 여러 크기의 비트맵을 ICO 파일로 저장
        /// </summary>
        private static void SaveAsIco(List<Bitmap> images, string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using var fs = new FileStream(filePath, FileMode.Create);
            using var bw = new BinaryWriter(fs);

            // ICO 헤더
            bw.Write((short)0);           // Reserved
            bw.Write((short)1);           // Type (1 = ICO)
            bw.Write((short)images.Count); // Image count

            var imageData = new List<byte[]>();
            int offset = 6 + (16 * images.Count);

            foreach (var img in images)
            {
                using var ms = new MemoryStream();
                img.Save(ms, ImageFormat.Png);
                var data = ms.ToArray();
                imageData.Add(data);

                bw.Write((byte)(img.Width >= 256 ? 0 : img.Width));
                bw.Write((byte)(img.Height >= 256 ? 0 : img.Height));
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((short)1);
                bw.Write((short)32);
                bw.Write(data.Length);
                bw.Write(offset);

                offset += data.Length;
            }

            foreach (var data in imageData)
            {
                bw.Write(data);
            }
        }

        /// <summary>
        /// 아이콘 파일 생성 실행
        /// </summary>
        public static void GenerateAllIcons(string directory)
        {
            Directory.CreateDirectory(directory);
            CreateIconFile(Path.Combine(directory, "app.ico"));
            Console.WriteLine($"Icons generated in: {directory}");
        }
    }
}
