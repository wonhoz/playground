using System.IO;
using StrForge.Models;

namespace StrForge.Services;

public class FileReplaceResult
{
    public string FilePath { get; set; } = string.Empty;
    public string OriginalContent { get; set; } = string.Empty;
    public string NewContent { get; set; } = string.Empty;
    public int MatchCount { get; set; }
    public bool HasChanges => OriginalContent != NewContent;
    public bool IsSelected { get; set; } = true;
    public string FileName => Path.GetFileName(FilePath);
    public string RelativePath { get; set; } = string.Empty;
}

public static class ReplaceEngine
{
    public static List<FileReplaceResult> Preview(
        List<string> filePaths,
        string rootPath,
        List<ReplaceRule> rules,
        IProgress<(int current, int total)>? progress = null)
    {
        var results = new List<FileReplaceResult>();
        var enabledRules = rules.Where(r => r.IsEnabled && r.CompiledRegex != null).ToList();
        if (enabledRules.Count == 0) return results;

        for (int i = 0; i < filePaths.Count; i++)
        {
            progress?.Report((i + 1, filePaths.Count));
            var path = filePaths[i];
            try
            {
                var (content, _) = FileScanner.ReadFile(path);
                var modified = content;
                var totalMatches = 0;
                foreach (var rule in enabledRules)
                {
                    var matches = rule.CompiledRegex!.Matches(modified).Count;
                    totalMatches += matches;
                    modified = rule.Apply(modified);
                }

                if (modified != content)
                {
                    results.Add(new FileReplaceResult
                    {
                        FilePath = path,
                        RelativePath = Path.GetRelativePath(rootPath, path),
                        OriginalContent = content,
                        NewContent = modified,
                        MatchCount = totalMatches,
                        IsSelected = true
                    });
                }
            }
            catch { /* 읽기 오류 무시 */ }
        }
        return results;
    }

    public static (int applied, int skipped) Apply(
        List<FileReplaceResult> results,
        bool backupOriginal)
    {
        int applied = 0, skipped = 0;
        foreach (var r in results.Where(r => r.IsSelected && r.HasChanges))
        {
            try
            {
                if (backupOriginal)
                    File.WriteAllText(r.FilePath + ".bak", r.OriginalContent);
                var (_, encoding) = FileScanner.ReadFile(r.FilePath);
                FileScanner.WriteFile(r.FilePath, r.NewContent, encoding);
                applied++;
            }
            catch { skipped++; }
        }
        return (applied, skipped);
    }
}
