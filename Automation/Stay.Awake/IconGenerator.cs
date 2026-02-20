using System.Drawing;

namespace StayAwake
{
    /// <summary>
    /// Pre-rendered 리소스 아이콘 로더
    /// </summary>
    public static class IconGenerator
    {
        private static readonly string ResourceDir = Path.Combine(AppContext.BaseDirectory, "Resources");

        /// <summary>
        /// 실행 중 트레이 아이콘 (녹색 활성 점)
        /// </summary>
        public static Icon LoadRunningIcon()
        {
            var path = Path.Combine(ResourceDir, "running.png");
            using var bitmap = new Bitmap(path);
            return Icon.FromHandle(bitmap.GetHicon());
        }

        /// <summary>
        /// 정지 트레이 아이콘 (회색 비활성 점)
        /// </summary>
        public static Icon LoadStoppedIcon()
        {
            var path = Path.Combine(ResourceDir, "stopped.png");
            using var bitmap = new Bitmap(path);
            return Icon.FromHandle(bitmap.GetHicon());
        }
    }
}
