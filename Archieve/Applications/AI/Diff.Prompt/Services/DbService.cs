using System.IO;
using DiffPrompt.Models;
using Microsoft.Data.Sqlite;

namespace DiffPrompt.Services;

public class DbService : IDisposable
{
    private readonly SqliteConnection _conn;

    public DbService()
    {
        string dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DiffPrompt", "experiments.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        InitSchema();
    }

    void InitSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS experiments (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL DEFAULT '',
                user_message TEXT NOT NULL DEFAULT '',
                prompt_a TEXT NOT NULL DEFAULT '',
                prompt_b TEXT NOT NULL DEFAULT '',
                model_a TEXT NOT NULL DEFAULT 'claude-sonnet-4-6',
                model_b TEXT NOT NULL DEFAULT 'claude-sonnet-4-6',
                output_a TEXT NOT NULL DEFAULT '',
                output_b TEXT NOT NULL DEFAULT '',
                tokens_a INTEGER NOT NULL DEFAULT 0,
                tokens_b INTEGER NOT NULL DEFAULT 0,
                cost_a REAL NOT NULL DEFAULT 0,
                cost_b REAL NOT NULL DEFAULT 0,
                latency_a_ms REAL NOT NULL DEFAULT 0,
                latency_b_ms REAL NOT NULL DEFAULT 0,
                winner_vote INTEGER,
                created_at TEXT NOT NULL,
                tags TEXT NOT NULL DEFAULT '',
                notes TEXT NOT NULL DEFAULT ''
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public int SaveExperiment(Experiment e)
    {
        using var cmd = _conn.CreateCommand();
        if (e.Id == 0)
        {
            cmd.CommandText = """
                INSERT INTO experiments
                (name,user_message,prompt_a,prompt_b,model_a,model_b,
                 output_a,output_b,tokens_a,tokens_b,cost_a,cost_b,
                 latency_a_ms,latency_b_ms,winner_vote,created_at,tags,notes)
                VALUES ($n,$um,$pa,$pb,$ma,$mb,$oa,$ob,$ta,$tb,$ca,$cb,$la,$lb,$w,$cr,$tg,$nt)
                """;
        }
        else
        {
            cmd.CommandText = """
                UPDATE experiments SET
                name=$n,user_message=$um,prompt_a=$pa,prompt_b=$pb,model_a=$ma,model_b=$mb,
                output_a=$oa,output_b=$ob,tokens_a=$ta,tokens_b=$tb,cost_a=$ca,cost_b=$cb,
                latency_a_ms=$la,latency_b_ms=$lb,winner_vote=$w,tags=$tg,notes=$nt
                WHERE id=$id
                """;
            cmd.Parameters.AddWithValue("$id", e.Id);
        }
        cmd.Parameters.AddWithValue("$n", e.Name);
        cmd.Parameters.AddWithValue("$um", e.UserMessage);
        cmd.Parameters.AddWithValue("$pa", e.PromptA);
        cmd.Parameters.AddWithValue("$pb", e.PromptB);
        cmd.Parameters.AddWithValue("$ma", e.ModelA);
        cmd.Parameters.AddWithValue("$mb", e.ModelB);
        cmd.Parameters.AddWithValue("$oa", e.OutputA);
        cmd.Parameters.AddWithValue("$ob", e.OutputB);
        cmd.Parameters.AddWithValue("$ta", e.TokensA);
        cmd.Parameters.AddWithValue("$tb", e.TokensB);
        cmd.Parameters.AddWithValue("$ca", e.CostA);
        cmd.Parameters.AddWithValue("$cb", e.CostB);
        cmd.Parameters.AddWithValue("$la", e.LatencyAMs);
        cmd.Parameters.AddWithValue("$lb", e.LatencyBMs);
        cmd.Parameters.AddWithValue("$w", (object?)e.WinnerVote ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cr", e.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$tg", e.Tags);
        cmd.Parameters.AddWithValue("$nt", e.Notes);
        cmd.ExecuteNonQuery();

        if (e.Id == 0)
        {
            using var idCmd = _conn.CreateCommand();
            idCmd.CommandText = "SELECT last_insert_rowid()";
            return (int)(long)idCmd.ExecuteScalar()!;
        }
        return e.Id;
    }

    public List<Experiment> GetAll()
    {
        var list = new List<Experiment>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id,name,user_message,model_a,model_b,tokens_a,tokens_b,cost_a,cost_b,winner_vote,created_at,tags FROM experiments ORDER BY id DESC";
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
            list.Add(new Experiment
            {
                Id = rdr.GetInt32(0), Name = rdr.GetString(1),
                UserMessage = rdr.GetString(2),
                ModelA = rdr.GetString(3), ModelB = rdr.GetString(4),
                TokensA = rdr.GetInt32(5), TokensB = rdr.GetInt32(6),
                CostA = rdr.GetDouble(7), CostB = rdr.GetDouble(8),
                WinnerVote = rdr.IsDBNull(9) ? null : rdr.GetInt32(9),
                CreatedAt = DateTime.Parse(rdr.GetString(10)),
                Tags = rdr.GetString(11)
            });
        return list;
    }

    public Experiment? GetById(int id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM experiments WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        using var rdr = cmd.ExecuteReader();
        if (!rdr.Read()) return null;
        return new Experiment
        {
            Id = rdr.GetInt32(0), Name = rdr.GetString(1),
            UserMessage = rdr.GetString(2),
            PromptA = rdr.GetString(3), PromptB = rdr.GetString(4),
            ModelA = rdr.GetString(5), ModelB = rdr.GetString(6),
            OutputA = rdr.GetString(7), OutputB = rdr.GetString(8),
            TokensA = rdr.GetInt32(9), TokensB = rdr.GetInt32(10),
            CostA = rdr.GetDouble(11), CostB = rdr.GetDouble(12),
            LatencyAMs = rdr.GetDouble(13), LatencyBMs = rdr.GetDouble(14),
            WinnerVote = rdr.IsDBNull(15) ? null : rdr.GetInt32(15),
            CreatedAt = DateTime.Parse(rdr.GetString(16)),
            Tags = rdr.GetString(17), Notes = rdr.GetString(18)
        };
    }

    public void Delete(int id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM experiments WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();
}
