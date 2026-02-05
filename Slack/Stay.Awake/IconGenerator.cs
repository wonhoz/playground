using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace StayAwake
{
    /// <summary>
    /// Slack Awake 테마 아이콘 생성기
    /// </summary>
    public static class IconGenerator
    {
        // Slack 브랜드 컬러
        private static readonly Color SlackPurple = Color.FromArgb(74, 21, 75);      // #4A154B
        private static readonly Color SlackPink = Color.FromArgb(224, 30, 90);       // #E01E5A
        private static readonly Color SlackBlue = Color.FromArgb(54, 197, 240);      // #36C5F0
        private static readonly Color SlackGreen = Color.FromArgb(46, 182, 125);     // #2EB67D
        private static readonly Color SlackYellow = Color.FromArgb(236, 178, 46);    // #ECB22E

        /// <summary>
        /// ICO 파일 생성 (여러 크기 포함)
        /// </summary>
        public static void CreateIconFile(string filePath)
        {
            var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
            var images = sizes.Select(size => CreateAwakeIcon(size)).ToList();

            SaveAsIco(images, filePath);

            foreach (var img in images)
                img.Dispose();
        }

        /// <summary>
        /// Awake 테마 아이콘 비트맵 생성
        /// - Slack 색상의 눈 뜬 모양 (커피컵 + 번개)
        /// </summary>
        public static Bitmap CreateAwakeIcon(int size)
        {
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float scale = size / 64f;
            g.Clear(Color.Transparent);

            // 배경 원 (Slack 보라색 그라데이션)
            using (var bgBrush = new LinearGradientBrush(
                new Rectangle(0, 0, size, size),
                SlackPurple,
                Color.FromArgb(106, 27, 154), // 더 밝은 보라색
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillEllipse(bgBrush, 2 * scale, 2 * scale, 60 * scale, 60 * scale);
            }

            // 커피컵 모양 (흰색)
            using (var cupBrush = new SolidBrush(Color.White))
            using (var cupPen = new Pen(Color.White, 2 * scale))
            {
                // 컵 몸체
                var cupBody = new RectangleF(16 * scale, 22 * scale, 24 * scale, 28 * scale);
                using (var path = CreateRoundedRect(cupBody, 4 * scale))
                {
                    g.FillPath(cupBrush, path);
                }

                // 컵 손잡이
                g.DrawArc(cupPen, 38 * scale, 28 * scale, 12 * scale, 16 * scale, -60, 240);
            }

            // 번개 모양 (Slack 노란색) - 활성화/에너지 표시
            using (var boltBrush = new SolidBrush(SlackYellow))
            {
                var boltPoints = new PointF[]
                {
                    new(30 * scale, 10 * scale),   // 상단
                    new(24 * scale, 24 * scale),   // 중앙 왼쪽
                    new(30 * scale, 24 * scale),   // 중앙
                    new(26 * scale, 38 * scale),   // 하단
                    new(36 * scale, 20 * scale),   // 중앙 오른쪽
                    new(30 * scale, 20 * scale),   // 중앙
                };
                g.FillPolygon(boltBrush, boltPoints);
            }

            // 김 나는 효과 (Slack 파란색)
            using (var steamPen = new Pen(SlackBlue, 2 * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                // 왼쪽 김
                DrawSteam(g, steamPen, 20 * scale, 18 * scale, 4 * scale);
                // 오른쪽 김
                DrawSteam(g, steamPen, 34 * scale, 16 * scale, 4 * scale);
            }

            // 테두리 (미묘한 그림자 효과)
            using (var borderPen = new Pen(Color.FromArgb(50, 0, 0, 0), 1 * scale))
            {
                g.DrawEllipse(borderPen, 2 * scale, 2 * scale, 60 * scale, 60 * scale);
            }

            return bitmap;
        }

        /// <summary>
        /// 실행 중 아이콘 (녹색 활성 표시)
        /// </summary>
        public static Bitmap CreateRunningIcon(int size)
        {
            var bitmap = CreateAwakeIcon(size);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float scale = size / 64f;

            // 우하단에 녹색 활성 점
            using var activeBrush = new SolidBrush(SlackGreen);
            using var borderPen = new Pen(Color.White, 2 * scale);
            g.FillEllipse(activeBrush, 44 * scale, 44 * scale, 16 * scale, 16 * scale);
            g.DrawEllipse(borderPen, 44 * scale, 44 * scale, 16 * scale, 16 * scale);

            return bitmap;
        }

        /// <summary>
        /// 정지 아이콘 (회색 비활성 표시)
        /// </summary>
        public static Bitmap CreateStoppedIcon(int size)
        {
            var bitmap = CreateAwakeIcon(size);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float scale = size / 64f;

            // 우하단에 회색 비활성 점
            using var inactiveBrush = new SolidBrush(Color.FromArgb(158, 158, 158));
            using var borderPen = new Pen(Color.White, 2 * scale);
            g.FillEllipse(inactiveBrush, 44 * scale, 44 * scale, 16 * scale, 16 * scale);
            g.DrawEllipse(borderPen, 44 * scale, 44 * scale, 16 * scale, 16 * scale);

            return bitmap;
        }

        private static void DrawSteam(Graphics g, Pen pen, float x, float y, float amplitude)
        {
            var points = new List<PointF>();
            for (float i = 0; i <= 10; i += 0.5f)
            {
                float px = x + (float)Math.Sin(i * 1.5) * amplitude;
                float py = y - i * 1.2f;
                points.Add(new PointF(px, py));
            }
            if (points.Count > 1)
            {
                g.DrawCurve(pen, points.ToArray(), 0.5f);
            }
        }

        private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }

        /// <summary>
        /// 여러 크기의 비트맵을 ICO 파일로 저장
        /// </summary>
        private static void SaveAsIco(List<Bitmap> images, string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Create);
            using var bw = new BinaryWriter(fs);

            // ICO 헤더
            bw.Write((short)0);           // Reserved
            bw.Write((short)1);           // Type (1 = ICO)
            bw.Write((short)images.Count); // Image count

            var imageData = new List<byte[]>();
            int offset = 6 + (16 * images.Count); // 헤더 + 디렉토리 엔트리들

            // 디렉토리 엔트리 작성
            foreach (var img in images)
            {
                using var ms = new MemoryStream();
                img.Save(ms, ImageFormat.Png);
                var data = ms.ToArray();
                imageData.Add(data);

                bw.Write((byte)(img.Width >= 256 ? 0 : img.Width));   // Width
                bw.Write((byte)(img.Height >= 256 ? 0 : img.Height)); // Height
                bw.Write((byte)0);          // Color palette
                bw.Write((byte)0);          // Reserved
                bw.Write((short)1);         // Color planes
                bw.Write((short)32);        // Bits per pixel
                bw.Write(data.Length);      // Image size
                bw.Write(offset);           // Offset

                offset += data.Length;
            }

            // 이미지 데이터 작성
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

            // 메인 아이콘
            CreateIconFile(Path.Combine(directory, "app.ico"));

            // 개별 PNG (트레이용)
            using (var running = CreateRunningIcon(64))
                running.Save(Path.Combine(directory, "running.png"), ImageFormat.Png);

            using (var stopped = CreateStoppedIcon(64))
                stopped.Save(Path.Combine(directory, "stopped.png"), ImageFormat.Png);

            Console.WriteLine($"Icons generated in: {directory}");
        }
    }
}
