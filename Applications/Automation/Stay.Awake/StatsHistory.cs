using System.Text.Json;

namespace StayAwake
{
    /// <summary>
    /// 과거 N일 일일 통계 히스토리 (파일 영속화)
    /// </summary>
    public static class StatsHistory
    {
        private static readonly string _historyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StayAwake",
            "stats_history.json");

        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        /// <summary>
        /// 히스토리 로드 (최신순, 최대 maxDays일)
        /// </summary>
        public static List<DailyStats> Load(int maxDays = 30)
        {
            try
            {
                if (File.Exists(_historyPath))
                {
                    var json = File.ReadAllText(_historyPath);
                    var list = JsonSerializer.Deserialize<List<DailyStats>>(json);
                    if (list != null)
                        return [.. list.OrderByDescending(x => x.Date).Take(maxDays)];
                }
            }
            catch { }
            return [];
        }

        /// <summary>
        /// 통계를 히스토리에 추가 (같은 날짜면 덮어씀, 최대 30일 보관)
        /// </summary>
        public static void Append(DailyStats stats)
        {
            var history = Load(30);
            history.RemoveAll(x => x.Date.Date == stats.Date.Date);
            history.Add(stats);
            var sorted = history.OrderByDescending(x => x.Date).Take(30).ToList();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_historyPath)!);
                File.WriteAllText(_historyPath, JsonSerializer.Serialize(sorted, _jsonOptions));
            }
            catch { }
        }
    }
}
