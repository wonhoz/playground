namespace Dict.Cast.Services;

using Microsoft.Data.Sqlite;

/// <summary>
/// WordNet 3.1 데이터 파일을 다운로드하고 SQLite DB로 변환합니다.
/// 최초 1회 실행 후 %LocalAppData%\Dict.Cast\dict.db에 캐시됩니다.
/// </summary>
public class WordNetBuilder
{
    const string DownloadUrl = "https://wordnetcode.princeton.edu/3.1/WNdb-3.1.zip";

    readonly string _dbPath;
    readonly string _tempDir;

    public WordNetBuilder(string dbPath)
    {
        _dbPath  = dbPath;
        _tempDir = Path.Combine(Path.GetDirectoryName(dbPath)!, "wn_temp");
    }

    public async Task BuildAsync(IProgress<(string Message, int Percent)> progress,
                                 CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            Directory.CreateDirectory(_tempDir);

            var zipPath = Path.Combine(_tempDir, "WNdb-3.1.zip");
            if (!File.Exists(zipPath))
            {
                progress.Report(("WordNet 3.1 다운로드 중 (약 12MB)...", 2));
                await DownloadAsync(zipPath, progress, ct);
            }

            progress.Report(("압축 해제 중...", 40));
            var dictDir = Path.Combine(_tempDir, "dict");
            if (!Directory.Exists(dictDir))
                await Task.Run(() => ExtractDict(zipPath, dictDir), ct);

            progress.Report(("사전 데이터베이스 빌드 중...", 50));
            await BuildDbAsync(dictDir, progress, ct);

            progress.Report(("임시 파일 정리...", 98));
            try { Directory.Delete(_tempDir, true); } catch { }

            progress.Report(("완료", 100));
        }
        catch
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
            throw;
        }
    }

    // ── Download ────────────────────────────────────────────────────────────

    async Task DownloadAsync(string zipPath, IProgress<(string, int)> progress, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var resp = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? -1;
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(zipPath);

        var buf = new byte[81920];
        long downloaded = 0;
        int  read;
        while ((read = await src.ReadAsync(buf, ct)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, read), ct);
            downloaded += read;
            if (total > 0)
            {
                int pct = (int)(2 + downloaded * 36 / total);
                progress.Report(($"다운로드 중... {downloaded / 1024 / 1024.0:F1} MB / {total / 1024 / 1024.0:F1} MB", pct));
            }
        }
    }

    // ── Extract ─────────────────────────────────────────────────────────────

    static void ExtractDict(string zipPath, string dictDir)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            // WordNet zip contains "dict/" folder at root
            if (!entry.FullName.StartsWith("dict/", StringComparison.OrdinalIgnoreCase)) continue;
            var dest = Path.Combine(dictDir, Path.GetFileName(entry.FullName));
            if (string.IsNullOrEmpty(entry.Name)) continue;
            entry.ExtractToFile(dest, overwrite: true);
        }
    }

    // ── Parse & Build DB ────────────────────────────────────────────────────

    sealed record SynsetData(string Pos, string Definition, List<string> Examples, List<string> Words);

    async Task BuildDbAsync(string dictDir, IProgress<(string, int)> progress, CancellationToken ct)
    {
        var synsets    = new Dictionary<string, SynsetData>();   // "pos:offset" → data
        var antonymMap = new Dictionary<string, List<string>>(); // "pos:offset" → antonym keys
        var entryRows  = new List<(string word, string key)>();

        // POS: n=noun, v=verb, a=adjective(open), s=adjective(satellite), r=adverb
        (string file, string pos)[] posFiles =
        [
            ("data.noun", "n"), ("data.verb", "v"),
            ("data.adj",  "a"), ("data.adv",  "r")
        ];

        for (int pi = 0; pi < posFiles.Length; pi++)
        {
            var (fileName, posChar) = posFiles[pi];
            var filePath = Path.Combine(dictDir, fileName);
            if (!File.Exists(filePath)) continue;

            int basePct = 50 + pi * 10;
            progress.Report(($"{fileName} 파싱 중...", basePct));

            await Task.Run(() => ParseDataFile(filePath, posChar, synsets, antonymMap, entryRows), ct);
        }

        progress.Report(("SQLite 데이터베이스 생성 중...", 92));

        await Task.Run(() =>
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    PRAGMA journal_mode=WAL;
                    CREATE TABLE IF NOT EXISTS synsets (
                        id         INTEGER PRIMARY KEY AUTOINCREMENT,
                        key        TEXT UNIQUE NOT NULL,
                        pos        TEXT NOT NULL,
                        definition TEXT NOT NULL,
                        examples   TEXT,
                        synonyms   TEXT,
                        antonyms   TEXT
                    );
                    CREATE TABLE IF NOT EXISTS entries (
                        id        INTEGER PRIMARY KEY AUTOINCREMENT,
                        word      TEXT NOT NULL,
                        synset_id INTEGER NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS idx_entries_word ON entries(word);
                ";
                cmd.ExecuteNonQuery();
            }

            // Insert synsets
            var keyToId = new Dictionary<string, long>(synsets.Count);
            using (var tx = conn.BeginTransaction())
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO synsets(key,pos,definition,examples,synonyms)
                    VALUES(@k,@p,@d,@ex,@sy)
                    RETURNING id";
                var pk  = cmd.Parameters.Add("@k",  SqliteType.Text);
                var pp  = cmd.Parameters.Add("@p",  SqliteType.Text);
                var pd  = cmd.Parameters.Add("@d",  SqliteType.Text);
                var pex = cmd.Parameters.Add("@ex", SqliteType.Text);
                var psy = cmd.Parameters.Add("@sy", SqliteType.Text);

                foreach (var (key, data) in synsets)
                {
                    pk.Value  = key;
                    pp.Value  = data.Pos;
                    pd.Value  = data.Definition;
                    pex.Value = data.Examples.Count > 0
                        ? JsonSerializer.Serialize(data.Examples)
                        : (object)DBNull.Value;
                    psy.Value = data.Words.Count > 1
                        ? JsonSerializer.Serialize(data.Words)
                        : (object)DBNull.Value;
                    keyToId[key] = (long)cmd.ExecuteScalar()!;
                }
                tx.Commit();
            }

            // Resolve antonyms and update
            using (var tx = conn.BeginTransaction())
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE synsets SET antonyms=@a WHERE id=@i";
                var pa = cmd.Parameters.Add("@a", SqliteType.Text);
                var pi = cmd.Parameters.Add("@i", SqliteType.Integer);

                foreach (var (key, antKeys) in antonymMap)
                {
                    if (!keyToId.TryGetValue(key, out var id)) continue;
                    var antWords = antKeys
                        .Where(k => synsets.ContainsKey(k))
                        .SelectMany(k => synsets[k].Words)
                        .Distinct()
                        .ToList();
                    if (antWords.Count == 0) continue;
                    pa.Value = JsonSerializer.Serialize(antWords);
                    pi.Value = id;
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }

            // Insert entries
            using (var tx = conn.BeginTransaction())
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO entries(word,synset_id) VALUES(@w,@s)";
                var pw = cmd.Parameters.Add("@w", SqliteType.Text);
                var ps = cmd.Parameters.Add("@s", SqliteType.Integer);

                foreach (var (word, key) in entryRows)
                {
                    if (!keyToId.TryGetValue(key, out var sid)) continue;
                    pw.Value = word;
                    ps.Value = sid;
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }

            // Optimize
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "ANALYZE; PRAGMA optimize;";
                cmd.ExecuteNonQuery();
            }
        }, ct);
    }

    static void ParseDataFile(
        string filePath, string posChar,
        Dictionary<string, SynsetData> synsets,
        Dictionary<string, List<string>> antonymMap,
        List<(string, string)> entryRows)
    {
        foreach (var line in File.ReadLines(filePath))
        {
            if (line.Length == 0 || line[0] == ' ') continue; // header lines start with space

            int pipeIdx = line.IndexOf(" | ", StringComparison.Ordinal);
            if (pipeIdx < 0) continue;

            var header = line.AsSpan(0, pipeIdx);
            var gloss  = line[(pipeIdx + 3)..].Trim();
            var (def, examples) = ParseGloss(gloss);

            // Tokenize header
            var parts = header.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;

            string offset = parts[0];
            // parts[1]=lex_filenum, parts[2]=ss_type
            if (!int.TryParse(parts[3], System.Globalization.NumberStyles.HexNumber, null, out int wCnt))
                continue;

            var words = new List<string>(wCnt);
            int idx = 4;
            for (int w = 0; w < wCnt && idx + 1 < parts.Length; w++, idx += 2)
                words.Add(parts[idx].Replace('_', ' ').ToLowerInvariant());

            if (idx >= parts.Length) continue;
            if (!int.TryParse(parts[idx++], System.Globalization.NumberStyles.HexNumber, null, out int pCnt))
                continue;

            var antonymOffsets = new List<string>();
            for (int p = 0; p < pCnt && idx + 3 < parts.Length; p++, idx += 4)
            {
                string sym    = parts[idx];
                string tgtOff = parts[idx + 1];
                string tgtPos = parts[idx + 2];
                string srcTgt = parts[idx + 3];
                // Lexical antonym: sym="!", source/target="0000" means applies to all
                if (sym == "!" && srcTgt == "0000")
                    antonymOffsets.Add(tgtPos + ":" + tgtOff);
            }

            // Use actual pos char from ss_type field (handles 's' satellite adj)
            string ssType = parts.Length > 2 ? parts[2] : posChar;

            string key = ssType + ":" + offset;
            lock (synsets)
            {
                synsets[key] = new SynsetData(ssType, def, examples, words);
                if (antonymOffsets.Count > 0)
                    antonymMap[key] = antonymOffsets;
                foreach (var w in words)
                    entryRows.Add((w, key));
            }
        }
    }

    static (string def, List<string> examples) ParseGloss(string gloss)
    {
        var examples = new List<string>();
        var exMatches = Regex.Matches(gloss, "\"([^\"]+)\"");
        foreach (Match m in exMatches)
            examples.Add(m.Groups[1].Value);

        // Definition: text before first semicolon or quote
        var def = gloss;
        int semi  = gloss.IndexOf(';');
        int quote = gloss.IndexOf('"');

        if (semi >= 0 && (quote < 0 || semi < quote))
            def = gloss[..semi].Trim();
        else if (quote >= 0)
            def = gloss[..quote].Trim();

        def = def.TrimEnd(';', ' ', ',');
        return (def, examples);
    }
}
