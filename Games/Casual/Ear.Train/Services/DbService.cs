using System.IO;
using EarTrain.Models;
using Microsoft.Data.Sqlite;

namespace EarTrain.Services;

public class DbService : IDisposable
{
    private readonly SqliteConnection _conn;

    public DbService()
    {
        string dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EarTrain", "earTrain.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        InitSchema();
    }

    void InitSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS elo_records (
                key TEXT PRIMARY KEY,
                elo REAL NOT NULL DEFAULT 1200,
                total INTEGER NOT NULL DEFAULT 0,
                correct INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS quiz_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                mode TEXT NOT NULL,
                correct TEXT NOT NULL,
                answered TEXT NOT NULL,
                is_correct INTEGER NOT NULL,
                time TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    // ─── ELO ─────────────────────────────────────────────────────────────
    public EloRecord GetElo(string key)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT elo, total, correct FROM elo_records WHERE key = $k";
        cmd.Parameters.AddWithValue("$k", key);
        using var rdr = cmd.ExecuteReader();
        if (rdr.Read())
            return new EloRecord { Key = key, Elo = rdr.GetDouble(0), Total = rdr.GetInt32(1), Correct = rdr.GetInt32(2) };
        return new EloRecord { Key = key };
    }

    public void SaveElo(EloRecord r)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO elo_records (key, elo, total, correct) VALUES ($k, $e, $t, $c)
            ON CONFLICT(key) DO UPDATE SET elo=$e, total=$t, correct=$c
            """;
        cmd.Parameters.AddWithValue("$k", r.Key);
        cmd.Parameters.AddWithValue("$e", r.Elo);
        cmd.Parameters.AddWithValue("$t", r.Total);
        cmd.Parameters.AddWithValue("$c", r.Correct);
        cmd.ExecuteNonQuery();
    }

    public List<EloRecord> GetAllElo()
    {
        var list = new List<EloRecord>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT key, elo, total, correct FROM elo_records ORDER BY elo DESC";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            list.Add(new EloRecord { Key = rdr.GetString(0), Elo = rdr.GetDouble(1), Total = rdr.GetInt32(2), Correct = rdr.GetInt32(3) });
        return list;
    }

    // ─── Quiz History ─────────────────────────────────────────────────────
    public void SaveResult(QuizResult r)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO quiz_history (mode, correct, answered, is_correct, time) VALUES ($m, $c, $a, $i, $t)";
        cmd.Parameters.AddWithValue("$m", r.Mode.ToString());
        cmd.Parameters.AddWithValue("$c", r.Correct);
        cmd.Parameters.AddWithValue("$a", r.Answered);
        cmd.Parameters.AddWithValue("$i", r.IsCorrect ? 1 : 0);
        cmd.Parameters.AddWithValue("$t", r.Time.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public List<QuizResult> GetRecentHistory(int limit = 50)
    {
        var list = new List<QuizResult>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT mode, correct, answered, is_correct, time FROM quiz_history ORDER BY id DESC LIMIT $l";
        cmd.Parameters.AddWithValue("$l", limit);
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            list.Add(new QuizResult(
                Enum.Parse<TrainMode>(rdr.GetString(0)),
                rdr.GetString(1), rdr.GetString(2),
                rdr.GetInt32(3) == 1,
                DateTime.Parse(rdr.GetString(4))));
        return list;
    }

    public void Dispose() => _conn.Dispose();
}
