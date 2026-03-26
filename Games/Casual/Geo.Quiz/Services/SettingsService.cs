using System.IO;
using System.Text.Json;
using Geo.Quiz.Models;

namespace Geo.Quiz.Services;

/// <summary>마지막 퀴즈 설정을 JSON 파일로 영속 관리.</summary>
public static class SettingsService
{
    static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GeoQuiz", "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(_path)) return new AppSettings();
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings));
        }
        catch { }
    }
}

public class AppSettings
{
    public string Mode          { get; set; } = nameof(QuizMode.Capital);
    public string Continent     { get; set; } = "전체";
    public int    QuestionCount { get; set; } = 10;
    public bool   TimerMode     { get; set; } = false;
}
