using SharpCompress.Archives;
using System.Text;

namespace ZipPeek.Services;

public class SearchResult
{
    public string EntryKey { get; init; } = "";
    public string FileName { get; init; } = "";
    public int Line { get; init; }
    public string Snippet { get; init; } = "";
    public bool IsNameMatch { get; init; }
    public string Icon => IsNameMatch ? "📁" : "📄";
}

public class SearchService
{
    /// <summary>
    /// 아카이브 내 파일 이름 또는 내용(텍스트)을 검색.
    /// contentSearch=true 시 텍스트 파일 내용도 grep.
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(
        string archivePath, string query, bool contentSearch,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var results = new List<SearchResult>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            using var archive = ArchiveFactory.Open(archivePath);
            var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
            int idx = 0;

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(++idx * 100 / entries.Count);

                var key = entry.Key?.Replace('\\', '/') ?? "";
                var name = Path.GetFileName(key);

                // 이름 매칭
                if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchResult
                    {
                        EntryKey = key,
                        FileName = key,
                        IsNameMatch = true,
                        Snippet = name
                    });
                }

                // 내용 검색 (텍스트 파일만)
                if (!contentSearch) continue;
                var ext = Path.GetExtension(name).ToLowerInvariant();
                if (!IsTextCandidate(ext, entry.Size)) continue;

                try
                {
                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using var s = entry.OpenEntryStream();
                        s.CopyTo(ms);
                        data = ms.ToArray();
                    }

                    string text;
                    try { text = Encoding.UTF8.GetString(data); }
                    catch { continue; }

                    var lines = text.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(new SearchResult
                            {
                                EntryKey = key,
                                FileName = key,
                                Line = i + 1,
                                Snippet = lines[i].Trim()
                            });
                        }
                    }
                }
                catch { /* 읽기 실패 무시 */ }
            }

            return results;
        }, ct);
    }

    private static bool IsTextCandidate(string ext, long size)
    {
        if (size > 2 * 1024 * 1024) return false; // 2MB 초과 스킵
        return ext is ".txt" or ".md" or ".log" or ".csv" or ".json" or ".xml"
            or ".yaml" or ".yml" or ".toml" or ".ini" or ".cfg" or ".config"
            or ".cs" or ".py" or ".js" or ".ts" or ".java" or ".cpp" or ".c"
            or ".h" or ".go" or ".rs" or ".html" or ".htm" or ".css" or ".sh"
            or ".bat" or ".ps1" or ".sql" or ".properties" or ".env";
    }
}
