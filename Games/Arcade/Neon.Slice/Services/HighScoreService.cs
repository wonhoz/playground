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
        }
        catch
        {
            _data = new();
        }
    }

    public bool TryUpdate(GameMode mode, int score)
    {
        var best  = GetBest(mode);
        var top3  = GetTop3List(mode);
        var dates = GetTop3DateList(mode);
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        // Top 3 업데이트 (점수·날짜 동기화)
        var combined = top3.Zip(dates.Concat(Enumerable.Repeat("", 3)))
                           .Select(x => (score: x.First, date: x.Second))
                           .Append((score, today))
                           .OrderByDescending(x => x.score)
                           .Take(3)
                           .ToList();

        top3.Clear();
        dates.Clear();
        foreach (var (s, d) in combined) { top3.Add(s); dates.Add(d); }

        var isNewBest = score > best;
        if (isNewBest)
        {
            switch (mode)
            {
                case GameMode.Classic:    _data.ClassicBest    = score; break;
                case GameMode.TimeAttack: _data.TimeAttackBest = score; break;
                case GameMode.Zen:        _data.ZenBest        = score; break;
            }
        }

        Save();
        return isNewBest;
    }

    public void SaveSettings(string mode, string difficulty, double width, double height)
    {
        _data.LastMode       = mode;
        _data.LastDifficulty = difficulty;
        _data.WindowWidth    = width;
        _data.WindowHeight   = height;
        Save();
    }

    public int GetBest(GameMode mode) => mode switch
    {
        GameMode.Classic    => _data.ClassicBest,
        GameMode.TimeAttack => _data.TimeAttackBest,
        GameMode.Zen        => _data.ZenBest,
        _                   => 0
    };

    public List<int>    GetTop3(GameMode mode)      => [..GetTop3List(mode)];
    public List<string> GetTop3Dates(GameMode mode) => [..GetTop3DateList(mode)];

    private List<int> GetTop3List(GameMode mode) => mode switch
    {
        GameMode.Classic    => _data.ClassicTop3,
        GameMode.TimeAttack => _data.TimeAttackTop3,
        GameMode.Zen        => _data.ZenTop3,
        _                   => []
    };

    private List<string> GetTop3DateList(GameMode mode) => mode switch
    {
        GameMode.Classic    => _data.ClassicTop3Dates,
        GameMode.TimeAttack => _data.TimeAttackTop3Dates,
        GameMode.Zen        => _data.ZenTop3Dates,
        _                   => []
    };

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
