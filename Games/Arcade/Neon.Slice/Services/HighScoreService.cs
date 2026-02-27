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
        var best = mode switch
        {
            GameMode.Classic    => _data.ClassicBest,
            GameMode.TimeAttack => _data.TimeAttackBest,
            GameMode.Zen        => _data.ZenBest,
            _                   => 0
        };

        if (score <= best) return false;

        switch (mode)
        {
            case GameMode.Classic:    _data.ClassicBest = score;    break;
            case GameMode.TimeAttack: _data.TimeAttackBest = score; break;
            case GameMode.Zen:        _data.ZenBest = score;        break;
        }

        Save();
        return true;
    }

    public int GetBest(GameMode mode) => mode switch
    {
        GameMode.Classic    => _data.ClassicBest,
        GameMode.TimeAttack => _data.TimeAttackBest,
        GameMode.Zen        => _data.ZenBest,
        _                   => 0
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
