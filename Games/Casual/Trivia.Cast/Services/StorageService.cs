namespace TriviaCast.Services;

public class StorageService
{
    private readonly string _dbPath;
    private Microsoft.Data.Sqlite.SqliteConnection? _conn;

    public StorageService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Playground", "Trivia.Cast");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "trivia.db");
        Init();
    }

    private void Init()
    {
        _conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}");
        _conn.Open();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS scores (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                mode      TEXT    NOT NULL,
                score     INTEGER NOT NULL,
                total     INTEGER NOT NULL,
                streak    INTEGER NOT NULL,
                date      TEXT    NOT NULL
            );
            CREATE TABLE IF NOT EXISTS wrong_answers (
                id        INTEGER PRIMARY KEY AUTOINCREMENT,
                category  TEXT NOT NULL,
                question  TEXT NOT NULL,
                correct   TEXT NOT NULL,
                chosen    TEXT NOT NULL,
                date      TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS daily_challenge (
                date      TEXT PRIMARY KEY,
                score     INTEGER NOT NULL,
                total     INTEGER NOT NULL
            );";
        cmd.ExecuteNonQuery();
    }

    public void SaveScore(string mode, int score, int total, int maxStreak)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "INSERT INTO scores (mode, score, total, streak, date) VALUES ($m, $s, $t, $k, $d)";
        cmd.Parameters.AddWithValue("$m", mode);
        cmd.Parameters.AddWithValue("$s", score);
        cmd.Parameters.AddWithValue("$t", total);
        cmd.Parameters.AddWithValue("$k", maxStreak);
        cmd.Parameters.AddWithValue("$d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }

    public void SaveWrongAnswer(string category, string question, string correct, string chosen)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "INSERT INTO wrong_answers (category, question, correct, chosen, date) VALUES ($c, $q, $a, $ch, $d)";
        cmd.Parameters.AddWithValue("$c", category);
        cmd.Parameters.AddWithValue("$q", question);
        cmd.Parameters.AddWithValue("$a", correct);
        cmd.Parameters.AddWithValue("$ch", chosen);
        cmd.Parameters.AddWithValue("$d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }

    public bool IsDailyChallengeCompleted(string date)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM daily_challenge WHERE date = $d";
        cmd.Parameters.AddWithValue("$d", date);
        return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
    }

    public void SaveDailyChallenge(string date, int score, int total)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO daily_challenge (date, score, total) VALUES ($d, $s, $t)";
        cmd.Parameters.AddWithValue("$d", date);
        cmd.Parameters.AddWithValue("$s", score);
        cmd.Parameters.AddWithValue("$t", total);
        cmd.ExecuteNonQuery();
    }

    public (int score, int total)? GetDailyChallenge(string date)
    {
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "SELECT score, total FROM daily_challenge WHERE date = $d";
        cmd.Parameters.AddWithValue("$d", date);
        using var r = cmd.ExecuteReader();
        if (r.Read()) return (r.GetInt32(0), r.GetInt32(1));
        return null;
    }

    public List<(string mode, int score, int total, int streak, string date)> GetTopScores(int limit = 10)
    {
        var list = new List<(string, int, int, int, string)>();
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "SELECT mode, score, total, streak, date FROM scores ORDER BY score DESC, date DESC LIMIT $l";
        cmd.Parameters.AddWithValue("$l", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add((r.GetString(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetString(4)));
        return list;
    }

    public List<(string category, string question, string correct, string chosen)> GetWrongAnswers(int limit = 50)
    {
        var list = new List<(string, string, string, string)>();
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = "SELECT category, question, correct, chosen FROM wrong_answers ORDER BY date DESC LIMIT $l";
        cmd.Parameters.AddWithValue("$l", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)));
        return list;
    }

    public void Dispose() => _conn?.Dispose();
}
