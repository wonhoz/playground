namespace Dict.Cast.Services;

using Microsoft.Data.Sqlite;
using Dict.Cast.Models;

public class DictionaryService
{
    readonly string _dbPath;

    public DictionaryService(string dbPath) => _dbPath = dbPath;

    public bool IsReady => File.Exists(_dbPath) && new FileInfo(_dbPath).Length > 1024 * 100;

    public List<WordSense> Lookup(string word)
    {
        if (!IsReady || string.IsNullOrWhiteSpace(word)) return [];
        word = word.Trim().ToLowerInvariant();

        var result = new List<WordSense>();
        using var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT s.pos, s.definition, s.synonyms, s.antonyms, s.examples
            FROM   entries e
            JOIN   synsets s ON e.synset_id = s.id
            WHERE  e.word = @w
            ORDER  BY s.pos, s.id";
        cmd.Parameters.AddWithValue("@w", word);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var pos  = PosLabel(reader.GetString(0));
            var def  = reader.GetString(1);
            var syns = DeserializeList(reader.IsDBNull(2) ? null : reader.GetString(2));
            var ants = DeserializeList(reader.IsDBNull(3) ? null : reader.GetString(3));
            var exs  = DeserializeList(reader.IsDBNull(4) ? null : reader.GetString(4));
            result.Add(new WordSense(pos, def, syns, ants, exs));
        }
        return result;
    }

    public List<string> Suggest(string prefix, int limit = 8)
    {
        if (!IsReady || string.IsNullOrWhiteSpace(prefix)) return [];
        prefix = prefix.Trim().ToLowerInvariant();

        var result = new List<string>();
        using var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT word FROM entries WHERE word LIKE @p ORDER BY word LIMIT @lim";
        cmd.Parameters.AddWithValue("@p", prefix + "%");
        cmd.Parameters.AddWithValue("@lim", limit);

        using var r = cmd.ExecuteReader();
        while (r.Read()) result.Add(r.GetString(0));
        return result;
    }

    static string PosLabel(string pos) => pos switch
    {
        "n" => "noun", "v" => "verb", "a" or "s" => "adjective", "r" => "adverb", _ => pos
    };

    static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
