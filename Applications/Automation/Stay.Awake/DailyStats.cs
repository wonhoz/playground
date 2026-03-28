using System.Text.Json;

namespace StayAwake
{
    /// <summary>
    /// 일일 통계 (파일로 영구 저장, 날짜 기준 자동 초기화)
    /// </summary>
    public class DailyStats
    {
        public DateTime Date { get; set; } = DateTime.Today;
        public int SimCount { get; set; } = 0;
        public int SkipCount { get; set; } = 0;

        /// <summary>오늘 누적 활성 시간 (초)</summary>
        public long ActiveSeconds { get; set; } = 0;

        private static readonly string _statsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StayAwake",
            "daily_stats.json");

        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public TimeSpan ActiveTime
        {
            get => TimeSpan.FromSeconds(ActiveSeconds);
            set => ActiveSeconds = (long)value.TotalSeconds;
        }

        public static DailyStats Load()
        {
            try
            {
                if (File.Exists(_statsPath))
                {
                    var json = File.ReadAllText(_statsPath);
                    var stats = JsonSerializer.Deserialize<DailyStats>(json);
                    if (stats != null && stats.Date.Date == DateTime.Today)
                        return stats;
                }
            }
            catch { /* 읽기 실패 시 새 통계 시작 */ }
            return new DailyStats();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_statsPath)!);
                File.WriteAllText(_statsPath, JsonSerializer.Serialize(this, _jsonOptions));
            }
            catch { /* 저장 실패 시 무시 */ }
        }
    }
}
