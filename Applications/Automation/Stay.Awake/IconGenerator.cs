using System.Drawing;
using System.Runtime.InteropServices;

namespace StayAwake
{
    /// <summary>
    /// Pre-rendered 리소스 아이콘 로더
    /// </summary>
    public static class IconGenerator
    {
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static readonly string ResourceDir = Path.Combine(AppContext.BaseDirectory, "Resources");

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
    }
}
