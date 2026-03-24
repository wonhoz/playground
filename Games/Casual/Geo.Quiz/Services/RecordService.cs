using System.IO;
using System.Text.Json;

namespace Geo.Quiz.Services;

/// <summary>모드/대륙별 최고 점수를 JSON 파일로 영속 관리.</summary>
public static class RecordService
{
    static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GeoQuiz", "records.json");

    static Dictionary<string, int> _records = [];

    static RecordService()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        Load();
    }

    /// <summary>현재 점수가 최고 기록이면 저장. 반환값: 갱신 여부.</summary>
    public static bool TryUpdate(string mode, string continent, int score)
    {
        var key = $"{mode}|{continent}";
        if (_records.TryGetValue(key, out int best) && best >= score) return false;
        _records[key] = score;
        Save();
        return true;
    }

    /// <summary>해당 모드/대륙 최고 점수. 기록 없으면 0.</summary>
    public static int GetBest(string mode, string continent) =>
        _records.TryGetValue($"{mode}|{continent}", out int v) ? v : 0;

    static void Load()
    {
        if (!File.Exists(_path)) return;
        try { _records = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(_path)) ?? []; }
        catch { _records = []; }
    }

    static void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(_records)); }
        catch { }
    }
}
