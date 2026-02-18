using System.Text.Json;

namespace StayAwake
{
    /// <summary>
    /// 앱 설정 (파일로 영구 저장)
    /// </summary>
    public class AppSettings
    {
        public int IntervalMinutes { get; set; } = 3;
        public int MoveDistance { get; set; } = 50;
        public string ActivityType { get; set; } = nameof(StayAwake.ActivityType.MouseMove);
        public bool PreventDisplaySleep { get; set; } = true;
        public bool SkipIfUserActive { get; set; } = true;

        // Slack 자동 상태 변경
        public string? SlackToken { get; set; }
        public bool SlackAutoStatusEnabled { get; set; } = false;
        public int WorkStartHour { get; set; } = 9;
        public int WorkEndHour { get; set; } = 19;
        public DateTime? SlackTokenExpiresAt { get; set; }

        private static readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StayAwake",
            "settings.json");

        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { /* 읽기 실패 시 기본값 사용 */ }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(this, _jsonOptions));
            }
            catch { /* 저장 실패 시 무시 */ }
        }
    }
}
