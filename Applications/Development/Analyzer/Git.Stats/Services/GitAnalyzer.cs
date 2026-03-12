using System.IO;
using System.Text.RegularExpressions;
using GitStats.Models;
using LibGit2Sharp;

namespace GitStats.Services;

public class GitAnalyzer
{
    private readonly string _repoPath;

    public GitAnalyzer(string repoPath) => _repoPath = repoPath;

    public static bool IsValidRepo(string path)
        => Repository.IsValid(path);

    public List<CommitInfo> GetAllCommits(IProgress<int>? progress = null)
    {
        var result = new List<CommitInfo>();
        using var repo = new Repository(_repoPath);
        var commits = repo.Commits.QueryBy(new CommitFilter { SortBy = CommitSortStrategies.Time }).ToList();
        int total = commits.Count;

        for (int i = 0; i < total; i++)
        {
            var c = commits[i];
            int add = 0, del = 0, files = 0;

            try
            {
                var parent = c.Parents.FirstOrDefault();
                var diff = parent != null
                    ? repo.Diff.Compare<PatchStats>(parent.Tree, c.Tree)
                    : repo.Diff.Compare<PatchStats>(null, c.Tree);
                add   = diff.TotalLinesAdded;
                del   = diff.TotalLinesDeleted;
                files = diff.Count();
            }
            catch { /* 일부 커밋 차이 계산 오류 무시 */ }

            result.Add(new CommitInfo(
                c.Sha[..7],
                c.Author.Name,
                c.Author.Email,
                c.MessageShort,
                c.Author.When,
                add, del, files));

            if (i % 50 == 0) progress?.Report(i * 100 / total);
        }

        progress?.Report(100);
        return result;
    }

    public List<DayActivity> GetHeatmapData(List<CommitInfo> commits)
    {
        return commits
            .GroupBy(c => DateOnly.FromDateTime(c.When.LocalDateTime))
            .Select(g => new DayActivity(g.Key, g.Count()))
            .OrderBy(d => d.Date)
            .ToList();
    }

    public List<HotFile> GetHotFiles(int topN = 20)
    {
        using var repo = new Repository(_repoPath);
        var fileStats = new Dictionary<string, (int Changes, int Add, int Del)>();

        var commits = repo.Commits.QueryBy(new CommitFilter { SortBy = CommitSortStrategies.Time }).ToList();
        foreach (var c in commits)
        {
            try
            {
                var parent = c.Parents.FirstOrDefault();
                var patch = parent != null
                    ? repo.Diff.Compare<Patch>(parent.Tree, c.Tree)
                    : repo.Diff.Compare<Patch>(null, c.Tree);

                foreach (var entry in patch)
                {
                    var path = entry.Path;
                    fileStats.TryGetValue(path, out var cur);
                    fileStats[path] = (cur.Changes + 1, cur.Add + entry.LinesAdded, cur.Del + entry.LinesDeleted);
                }
            }
            catch { }
        }

        return fileStats
            .Select(kv => new HotFile(kv.Key, kv.Value.Changes, kv.Value.Add, kv.Value.Del))
            .OrderByDescending(f => f.Changes)
            .Take(topN)
            .ToList();
    }

    public List<AuthorStat> GetAuthorStats(List<CommitInfo> commits)
    {
        return commits
            .GroupBy(c => c.Email)
            .Select(g => new AuthorStat(
                g.First().Author,
                g.Key,
                g.Count(),
                g.Sum(c => c.Additions),
                g.Sum(c => c.Deletions)))
            .OrderByDescending(a => a.Commits)
            .ToList();
    }

    public List<LanguageStat> GetLanguageStats()
    {
        using var repo = new Repository(_repoPath);
        var stats = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        var tip = repo.Head.Tip;
        if (tip == null) return new List<LanguageStat>();

        CountTreeLines(repo, tip.Tree, stats);

        var total = stats.Values.Sum();
        if (total == 0) return new List<LanguageStat>();

        return stats
            .OrderByDescending(kv => kv.Value)
            .Take(10)
            .Select(kv => new LanguageStat(kv.Key, kv.Value, GetLangColor(kv.Key)))
            .ToList();
    }

    private void CountTreeLines(Repository repo, Tree tree, Dictionary<string, long> stats)
    {
        foreach (var entry in tree)
        {
            if (entry.TargetType == TreeEntryTargetType.Blob)
            {
                var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                if (string.IsNullOrEmpty(ext)) continue;
                var blob = (Blob)entry.Target;
                if (blob.IsBinary) continue;
                using var reader = new StreamReader(blob.GetContentStream());
                long lines = 0;
                while (reader.ReadLine() != null) lines++;
                stats.TryGetValue(ext, out var cur);
                stats[ext] = cur + lines;
            }
            else if (entry.TargetType == TreeEntryTargetType.Tree)
            {
                CountTreeLines(repo, (Tree)entry.Target, stats);
            }
        }
    }

    public List<KeywordStat> GetKeywords(List<CommitInfo> commits, int topN = 30)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a","an","the","and","or","in","on","at","to","of","for","with",
            "is","was","are","be","by","from","fix","add","update","remove",
            "change","minor","feat","refactor","docs","test","style","build",
            "ci","chore","revert","wip"
        };

        var words = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in commits)
        {
            foreach (var word in Regex.Split(c.Message, @"[^\w]+"))
            {
                if (word.Length < 3 || stopWords.Contains(word)) continue;
                words.TryGetValue(word, out var cur);
                words[word] = cur + 1;
            }
        }

        return words
            .OrderByDescending(kv => kv.Value)
            .Take(topN)
            .Select(kv => new KeywordStat(kv.Key, kv.Value))
            .ToList();
    }

    private static string GetLangColor(string ext) => ext switch
    {
        ".cs"   => "#4A9EFF",
        ".ts"   => "#007ACC",
        ".js"   => "#F7DF1E",
        ".py"   => "#3776AB",
        ".java" => "#F89820",
        ".cpp"  or ".cc" => "#659AD2",
        ".c"    => "#555555",
        ".go"   => "#00ADD8",
        ".rs"   => "#CE4A00",
        ".kt"   => "#A97BFF",
        ".swift" => "#FA7343",
        ".xaml" or ".xml" or ".html" => "#E44D26",
        ".css"  => "#264DE4",
        ".json" => "#CBB96A",
        ".md"   => "#A0A0A0",
        _       => "#808080"
    };
}
