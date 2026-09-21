using System.Text.RegularExpressions;
using AIPForge.Models;

namespace AIPForge.Services;

public sealed record MissingFile(string FileKey, string SourcePath, string? ResolvedPath);

public sealed record CheckReport(
    int TotalFiles,
    int CheckedFiles,
    List<MissingFile> Missing,
    List<string> NewInSyncFolders,
    List<string> Notes);

/// <summary>
/// 빌드 전 점검: 프로젝트가 참조하는 원본 파일이 실제로 있는지,
/// 동기화 폴더에 프로젝트에 아직 반영되지 않은 새 파일이 있는지(= RefreshSync 필요) 확인.
/// </summary>
public static class ProjectValidator
{
    private const int MaxNewFilesListed = 500;

    public static CheckReport Run(AipDocument doc, CancellationToken ct)
    {
        var notes = new List<string>();
        var missing = new List<MissingFile>();
        var projectFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int total = 0, checkedCount = 0;

        foreach (var row in AipDocument.Rows(doc.FindComponent("MsiFilesComponent")))
        {
            ct.ThrowIfCancellationRequested();
            total++;
            var src = row.Get("SourcePath");
            var resolved = doc.ResolvePath(src);
            if (resolved == null) continue; // 경로 변수(<AI_...>) 사용 — 확인 불가
            checkedCount++;
            projectFiles.Add(resolved);
            if (!File.Exists(resolved))
                missing.Add(new MissingFile(row.Get("File") ?? "?", src ?? "", resolved));
        }

        var newFiles = new List<string>();
        foreach (var sync in doc.SyncFolders())
        {
            ct.ThrowIfCancellationRequested();
            if (sync.ResolvedPath == null) continue;
            if (!System.IO.Directory.Exists(sync.ResolvedPath))
            {
                notes.Add($"동기화 원본 폴더가 없습니다: {sync.ResolvedPath}");
                continue;
            }

            var row = AipDocument.Rows(doc.FindComponent("SynchronizedFolderComponent"))
                .FirstOrDefault(r => r.Get("SourcePath") == sync.SourcePath);
            var excludes = BuildExcludes(row?.Get("ExcludePattern"));

            foreach (var file in EnumerateFiles(sync.ResolvedPath, excludes, ct))
            {
                if (projectFiles.Contains(file)) continue;
                if (newFiles.Count < MaxNewFilesListed) newFiles.Add(file);
                else break;
            }
        }

        return new CheckReport(total, checkedCount, missing, newFiles, notes);
    }

    private static IEnumerable<string> EnumerateFiles(string root, List<Regex> excludes, CancellationToken ct)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();
            string[] files, dirs;
            try
            {
                files = System.IO.Directory.GetFiles(dir);
                dirs = System.IO.Directory.GetDirectories(dir);
            }
            catch
            {
                continue;
            }
            foreach (var f in files)
                if (!IsExcluded(Path.GetFileName(f), excludes)) yield return f;
            foreach (var d in dirs)
                if (!IsExcluded(Path.GetFileName(d), excludes)) stack.Push(d);
        }
    }

    /// <summary>AI ExcludePattern("*~|#*#|CVS|...") → 이름 매칭 정규식 목록</summary>
    private static List<Regex> BuildExcludes(string? pattern)
    {
        var list = new List<Regex>();
        if (string.IsNullOrEmpty(pattern)) return list;
        foreach (var p in pattern.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var rx = "^" + Regex.Escape(p).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            list.Add(new Regex(rx, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        }
        return list;
    }

    private static bool IsExcluded(string name, List<Regex> excludes)
    {
        foreach (var rx in excludes)
            if (rx.IsMatch(name)) return true;
        return false;
    }
}
