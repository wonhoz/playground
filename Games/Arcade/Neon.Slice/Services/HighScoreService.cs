using System.IO;
using System.Text.Json;
using NeonSlice.Models;

namespace NeonSlice.Services;

public sealed class HighScoreService
{
    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NeonSlice", "highscores.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private HighScoreData _data = new();

    public HighScoreData Data => _data;

    public void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            _data = JsonSerializer.Deserialize<HighScoreData>(json) ?? new();
            MigrateLegacyIfNeeded();
        }
        catch
        {
            _data = new();
        }
    }

    // v1.0 → v1.1 — 모드별 Top3를 "Mode_Normal" 키로 이전
    private void MigrateLegacyIfNeeded()
    {
        if (_data.Top3ByKey.Count > 0) return; // 이미 새 포맷

        MigrateOne("Classic_Normal",    _data.ClassicTop3,    _data.ClassicTop3Dates);
        MigrateOne("TimeAttack_Normal", _data.TimeAttackTop3, _data.TimeAttackTop3Dates);
        MigrateOne("Zen_Normal",        _data.ZenTop3,        _data.ZenTop3Dates);

        // 레거시 필드 비움 (다음 Save 시 빈 배열로 저장)
        _data.ClassicTop3.Clear();    _data.ClassicTop3Dates.Clear();
        _data.TimeAttackTop3.Clear(); _data.TimeAttackTop3Dates.Clear();
        _data.ZenTop3.Clear();        _data.ZenTop3Dates.Clear();
        _data.ClassicBest = _data.TimeAttackBest = _data.ZenBest = 0;
    }

    private void MigrateOne(string key, List<int> scores, List<string> dates)
    {
        if (scores.Count == 0) return;
        _data.Top3ByKey[key]      = [..scores];
        _data.Top3DatesByKey[key] = [..dates.Concat(Enumerable.Repeat("", 3)).Take(scores.Count)];
    }

    private static string Key(GameMode mode, Difficulty diff) => $"{mode}_{diff}";

    public bool TryUpdate(GameMode mode, Difficulty diff, int score)
    {
        var key = Key(mode, diff);
        if (!_data.Top3ByKey.TryGetValue(key, out var top3))        top3  = _data.Top3ByKey[key]      = [];
        if (!_data.Top3DatesByKey.TryGetValue(key, out var dates))  dates = _data.Top3DatesByKey[key] = [];

        var prevBest = top3.Count > 0 ? top3[0] : 0;
        var today    = DateTime.Now.ToString("yyyy-MM-dd");

        var combined = top3.Zip(dates.Concat(Enumerable.Repeat("", 3)))
                           .Select(x => (score: x.First, date: x.Second))
                           .Append((score, today))
                           .OrderByDescending(x => x.score)
                           .Take(3)
                           .ToList();

        top3.Clear();  dates.Clear();
        foreach (var (s, d) in combined) { top3.Add(s); dates.Add(d); }

        Save();
        return score > prevBest;
    }

    public void SaveSettings(string mode, string difficulty, double width, double height)
    {
        _data.LastMode       = mode;
        _data.LastDifficulty = difficulty;
        _data.WindowWidth    = width;
        _data.WindowHeight   = height;
        Save();
    }

    public void SaveAudioSettings(double bgm, double sfx, bool muted)
    {
        _data.BgmVolume = bgm;
        _data.SfxVolume = sfx;
        _data.Muted     = muted;
        Save();
    }

    public int GetBest(GameMode mode, Difficulty diff)
    {
        var key = Key(mode, diff);
        return _data.Top3ByKey.TryGetValue(key, out var top3) && top3.Count > 0 ? top3[0] : 0;
    }

    public List<int> GetTop3(GameMode mode, Difficulty diff)
    {
        var key = Key(mode, diff);
        return _data.Top3ByKey.TryGetValue(key, out var top3) ? [..top3] : [];
    }

    public List<string> GetTop3Dates(GameMode mode, Difficulty diff)
    {
        var key = Key(mode, diff);
        return _data.Top3DatesByKey.TryGetValue(key, out var dates) ? [..dates] : [];
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_data, JsonOpts));
        }
        catch { /* 저장 실패 시 무시 */ }
    }
}
