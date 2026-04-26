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

        /// <summary>트레이 아이콘에 카운트다운 진행률 원형 링 표시 여부 (false: 고정 녹색/회색 점)</summary>
        public bool ShowProgressIcon { get; set; } = true;

        /// <summary>전역 단축키 활성화 여부 (Ctrl+Alt+S 시작/정지, Ctrl+Alt+R 즉시 실행)</summary>
        public bool GlobalHotkeyEnabled { get; set; } = true;

        // Slack 자동 상태 변경 (UI 자동화 방식 - 토큰 불필요)
        public bool SlackAutoStatusEnabled { get; set; } = false;
        public int WorkStartHour { get; set; } = 8;
        public int WorkStartMinute { get; set; } = 55;
        public int WorkEndHour { get; set; } = 18;
        public int WorkEndMinute { get; set; } = 55;

        // 마지막 Slack 상태 변경 날짜 저장 (재시작 시 중복 전송 방지)
        public DateTime LastSlackActiveSetDate { get; set; } = DateTime.MinValue;
        public DateTime LastSlackAwaySetDate { get; set; } = DateTime.MinValue;

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
            catch (Exception ex) { Logger.LogException("AppSettings.Load", ex); /* 읽기 실패 시 기본값 사용 */ }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                File.WriteAllText(_settingsPath, JsonSerializer.Serialize(this, _jsonOptions));
            }
            catch (Exception ex) { Logger.LogException("AppSettings.Save", ex); /* 저장 실패 시 무시 */ }
        }

        public async Task SaveAsync()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                await File.WriteAllTextAsync(_settingsPath, JsonSerializer.Serialize(this, _jsonOptions));
            }
            catch (Exception ex) { Logger.LogException("AppSettings.SaveAsync", ex); /* 저장 실패 시 무시 */ }
        }
    }
}
