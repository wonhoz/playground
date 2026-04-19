using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace StayAwake
{
    /// <summary>
    /// Pre-rendered 리소스 아이콘 로더 + 카운트다운 진행률 동적 렌더
    /// </summary>
    public static class IconGenerator
    {
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static readonly string ResourceDir = Path.Combine(AppContext.BaseDirectory, "Resources");

        // 진행률 아이콘 색상 (ACTIVE 배지 색상과 일치)
        private static readonly Color ProgressColor = Color.FromArgb(67, 217, 123); // #43D97B
        private static readonly Color ProgressTrack = Color.FromArgb(60, 60, 60);
        private static readonly Color ProgressBg = Color.FromArgb(24, 24, 24);

        /// <summary>
        /// 실행 중 트레이 아이콘 (녹색 활성 점)
        /// </summary>
        public static Icon LoadRunningIcon()
        {
            var path = Path.Combine(ResourceDir, "running.png");
            if (File.Exists(path))
            {
                try
                {
                    using var bitmap = new Bitmap(path);
                    return IconFromBitmap(bitmap);
                }
                catch { }
            }
            return CreateFallbackIcon(Color.FromArgb(67, 217, 123)); // 초록
        }

        /// <summary>
        /// 정지 트레이 아이콘 (회색 비활성 점)
        /// </summary>
        public static Icon LoadStoppedIcon()
        {
            var path = Path.Combine(ResourceDir, "stopped.png");
            if (File.Exists(path))
            {
                try
                {
                    using var bitmap = new Bitmap(path);
                    return IconFromBitmap(bitmap);
                }
                catch { }
            }
            return CreateFallbackIcon(Color.FromArgb(128, 128, 128)); // 회색
        }

        /// <summary>
        /// 단색 원형 폴백 아이콘 생성 (리소스 파일 없을 때)
        /// </summary>
        private static Icon CreateFallbackIcon(Color color)
        {
            using var bitmap = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 1, 1, 14, 14);
            return IconFromBitmap(bitmap);
        }

        /// <summary>
        /// Bitmap → Icon 변환 (GetHicon 핸들 즉시 해제하여 메모리 누수 방지)
        /// </summary>
        private static Icon IconFromBitmap(Bitmap bitmap)
        {
            var hIcon = bitmap.GetHicon();
            try
            {
                return (Icon)Icon.FromHandle(hIcon).Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        /// <summary>
        /// 다음 시뮬레이션까지 진행률을 원형 링으로 표시하는 32×32 트레이 아이콘 생성
        /// </summary>
        /// <param name="progress">0.0 (이제 막 시작) ~ 1.0 (다음 실행 임박)</param>
        public static Icon CreateProgressIcon(double progress)
        {
            progress = Math.Clamp(progress, 0.0, 1.0);

            const int size = 32;
            using var bitmap = new Bitmap(size, size);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // 외곽 링 트랙 (회색)
            using (var trackPen = new Pen(ProgressTrack, 4f))
                g.DrawEllipse(trackPen, 3, 3, size - 6, size - 6);

            // 진행률 아크 (초록, 시계방향 12시 시작)
            if (progress > 0.01)
            {
                using var progPen = new Pen(ProgressColor, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                var sweep = (float)(360.0 * progress);
                g.DrawArc(progPen, 3, 3, size - 6, size - 6, -90f, sweep);
            }

            // 중앙 점 (실행 중 녹색 표시)
            using (var centerBrush = new SolidBrush(ProgressColor))
                g.FillEllipse(centerBrush, 13, 13, 6, 6);

            return IconFromBitmap(bitmap);
        }
    }
}
