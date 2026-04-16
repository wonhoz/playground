using System.IO;
using System.Text.Json;

namespace ScreenRecorder.Models;

public class RecordingSettings
{
    public int FrameRate { get; set; } = 15;
    public string OutputFormat { get; set; } = "mp4";
    public string OutputFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    public bool ShowCursor { get; set; } = true;
    public int LastRegionX { get; set; } = -1;
    public int LastRegionY { get; set; } = -1;
    public int LastRegionWidth { get; set; } = 0;
    public int LastRegionHeight { get; set; } = 0;
    public int MaxRecordingSeconds { get; set; } = 0;  // 0 = 무제한
    public string FileNamePrefix { get; set; } = "recording";
    public bool RecordAudio { get; set; } = false;
    public string AudioDevice { get; set; } = "";  // 빈 문자열 = 기본 장치

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenRecorder", "settings.json");

    public static RecordingSettings CreateDefault() => new();

    public static RecordingSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<RecordingSettings>(json) ?? new();
            }
        }
        catch { /* 손상된 설정 파일 무시 */ }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* 저장 실패 무시 */ }
    }
}
