using System.Text.Json;

namespace ToastCast.Models;

public class AppConfig
{
    public List<Routine> Routines { get; set; } = [];

    /// <summary>유휴 기준 시간(분) — 이 이상 유휴면 알림 스킵</summary>
    public int IdleThresholdMinutes { get; set; } = 5;

    /// <summary>시작할 때 루틴 타이머를 자동 시작</summary>
    public bool AutoStart { get; set; } = true;

    private static readonly string ConfigPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ToastCast", "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg?.Routines.Count > 0) return cfg;
            }
        }
        catch { /* 파일 손상 시 기본값 사용 */ }

        return new AppConfig { Routines = Routine.Defaults() };
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { /* 저장 실패 무시 */ }
    }
}
