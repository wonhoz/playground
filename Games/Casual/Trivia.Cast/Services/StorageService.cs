namespace TriviaCast.Services;

public class StorageService : IDisposable
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

    private bool _initialized = false;

    private void Init()
    {
        _conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}");
        _conn.Open();
        _initialized = true;

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
            );
            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();
    }

    public void SaveScore(string mode, int score, int total, int maxStreak)
    {
        if (!_initialized || _conn is null) return;
        using var cmd = _conn.CreateCommand();
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
        if (!_initialized || _conn is null) return;
        // 오늘 날짜 기준으로 동일 질문 중복 저장 방지
        using var check = _conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM wrong_answers WHERE question = $q AND date >= $today";
        check.Parameters.AddWithValue("$q", question);
        check.Parameters.AddWithValue("$today", DateTime.Now.ToString("yyyy-MM-dd"));
        if (Convert.ToInt64(check.ExecuteScalar()!) > 0) return;

        using var cmd = _conn.CreateCommand();
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
        if (!_initialized || _conn is null) return false;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM daily_challenge WHERE date = $d";
        cmd.Parameters.AddWithValue("$d", date);
        return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
    }

    public void SaveDailyChallenge(string date, int score, int total)
    {
        if (!_initialized || _conn is null) return;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO daily_challenge (date, score, total) VALUES ($d, $s, $t)";
        cmd.Parameters.AddWithValue("$d", date);
        cmd.Parameters.AddWithValue("$s", score);
        cmd.Parameters.AddWithValue("$t", total);
        cmd.ExecuteNonQuery();
    }

    public (int score, int total)? GetDailyChallenge(string date)
    {
        if (!_initialized || _conn is null) return null;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT score, total FROM daily_challenge WHERE date = $d";
        cmd.Parameters.AddWithValue("$d", date);
        using var r = cmd.ExecuteReader();
        if (r.Read()) return (r.GetInt32(0), r.GetInt32(1));
        return null;
    }

    public List<(string mode, int score, int total, int streak, string date)> GetTopScores(int limit = 10)
    {
        var list = new List<(string, int, int, int, string)>();
        if (!_initialized || _conn is null) return list;
        using var cmd = _conn.CreateCommand();
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
        if (!_initialized || _conn is null) return list;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT category, question, correct, chosen FROM wrong_answers ORDER BY date DESC LIMIT $l";
        cmd.Parameters.AddWithValue("$l", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)));
        return list;
    }

    public string? GetSetting(string key)
    {
        if (!_initialized || _conn is null) return null;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $k";
        cmd.Parameters.AddWithValue("$k", key);
        var result = cmd.ExecuteScalar();
        return result as string;
    }

    public void SetSetting(string key, string value)
    {
        if (!_initialized || _conn is null) return;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES ($k, $v)";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    public (int totalGames, int totalScore, int bestScore) GetOverallStats()
    {
        if (!_initialized || _conn is null) return (0, 0, 0);
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(score),0), COALESCE(MAX(score),0) FROM scores";
        using var r = cmd.ExecuteReader();
        if (r.Read()) return (r.GetInt32(0), r.GetInt32(1), r.GetInt32(2));
        return (0, 0, 0);
    }

    public List<(string category, int correct, int total)> GetCategoryStats()
    {
        var list = new List<(string, int, int)>();
        if (!_initialized || _conn is null) return list;
        using var cmd = _conn.CreateCommand();
        // 오답 노트에서 카테고리별 오답 수, QuestionDatabase 전체 대비 정답률 계산
        cmd.CommandText = @"
            SELECT category, COUNT(*) as wrong
            FROM wrong_answers
            GROUP BY category
            ORDER BY wrong DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add((r.GetString(0), 0, r.GetInt32(1)));
        return list;
    }

    public void Dispose() => _conn?.Dispose();
}
