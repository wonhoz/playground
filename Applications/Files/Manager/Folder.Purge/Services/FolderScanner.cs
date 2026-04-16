namespace FolderPurge.Services;

public class ScanProgress
{
    public int Scanned { get; set; }
    public int Total { get; set; }
    public string CurrentPath { get; set; } = string.Empty;
    public double Percent => Total > 0 ? Math.Min(100.0, Scanned * 100.0 / Total) : 0;
    public int SkippedReparsePoints { get; set; }  // B2: 정션/링크 건너뜀 카운터
}

public class FolderScanner
{
    private readonly ScanOptions _opts;
    private readonly CancellationToken _ct;

    public FolderScanner(ScanOptions opts, CancellationToken ct = default)
    {
        _opts = opts;
        _ct = ct;
    }

    public async Task<List<FolderEntry>> ScanAsync(
        IEnumerable<string> roots,
        IProgress<ScanProgress>? progress = null)
    {
        var results  = new List<FolderEntry>();
        var prog     = new ScanProgress();
        var rootList = roots.Where(Directory.Exists).ToList();

        await Task.Run(() =>
        {
            // 1단계: 전체 디렉토리 수 추정 (진행률용)
            foreach (var root in rootList)
            {
                _ct.ThrowIfCancellationRequested();
                try
                {
                    prog.Total++;
                    foreach (var _ in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
                    {
                        prog.Total++;
                        if (_ct.IsCancellationRequested) break;
                    }
                }
                catch { prog.Total = Math.Max(prog.Total, 1); }
            }
            progress?.Report(prog);

            // 2단계: 실제 스캔
            foreach (var root in rootList)
            {
                _ct.ThrowIfCancellationRequested();
                ScanDirectory(root, results, prog, progress, depth: 0);
            }
            progress?.Report(prog);
        }, _ct);

        return results;
    }

    private void ScanDirectory(
        string path,
        List<FolderEntry> results,
        ScanProgress prog,
        IProgress<ScanProgress>? progress,
        int depth)
    {
        _ct.ThrowIfCancellationRequested();

        // B3: 깊이 제한 (0 = 무제한)
        if (_opts.MaxDepth > 0 && depth > _opts.MaxDepth) return;

        string folderName = Path.GetFileName(path);

        // 제외 폴더 건너뜀 (정확한 이름 + 와일드카드 패턴) — B1
        if (IsExcludedFolder(folderName)) return;

        prog.CurrentPath = path;
        prog.Scanned++;
        if (prog.Scanned % 10 == 0) progress?.Report(prog);  // LOW3: 10개 단위

        string[] subDirs;
        string[] files;

        try
        {
            subDirs = Directory.GetDirectories(path);
            files   = Directory.GetFiles(path);
        }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException)                 { return; }
        catch (Exception)                   { return; }

        // 최근 수정 폴더 제외 옵션 — 탐지만 스킵, 하위 재귀는 계속
        bool skipDetection = _opts.ExcludeRecentFolders && IsRecentlyModified(path);

        // ── 파일 탐지: 0바이트 파일 + 패턴 파일 (나이 필터 미적용) ──
        if (_opts.ScanEmptyFiles || _opts.FileDetectPatterns.Count > 0)
        {
            foreach (var f in files)
            {
                _ct.ThrowIfCancellationRequested();
                try
                {
                    var fi    = new FileInfo(f);
                    var fname = fi.Name;

                    if (_opts.ScanEmptyFiles && fi.Length == 0)
                    {
                        results.Add(new FolderEntry
                        {
                            Path      = f,
                            Kind      = FolderKind.EmptyFile,
                            SizeBytes = 0,
                            ItemCount = 0
                        });
                    }
                    else if (_opts.FileDetectPatterns.Count > 0 && MatchesAnyPattern(fname, _opts.FileDetectPatterns))
                    {
                        // B5: 패턴 파일 탐지 (0바이트가 아닌 경우)
                        results.Add(new FolderEntry
                        {
                            Path      = f,
                            Kind      = FolderKind.PatternFile,
                            SizeBytes = fi.Length,
                            ItemCount = 0
                        });
                    }
                }
                catch { /* 접근 불가 파일 건너뜀 */ }
            }
        }

        // ── VS 아티팩트 탐지 ──
        if (!skipDetection && _opts.ScanVsArtifacts)
        {
            bool hasOnlyArtifactDirs = subDirs.Length > 0
                && subDirs.All(d => _opts.VsArtifactNames.Contains(Path.GetFileName(d)));
            bool hasOnlyArtifactFiles = files.Length == 0
                || files.All(f => _opts.VsArtifactFileExtensions.Contains(Path.GetExtension(f)));

            if (hasOnlyArtifactDirs && hasOnlyArtifactFiles && subDirs.Length >= 1)
            {
                GetDirectorySizeAndCount(path, out long totalSize, out int totalItems);
                results.Add(new FolderEntry
                {
                    Path      = path,
                    Kind      = FolderKind.VsArtifact,
                    SizeBytes = totalSize,
                    ItemCount = totalItems
                });
                return; // 이 폴더 자체를 삭제 대상으로 등록, 하위 재귀 불필요
            }
        }

        // ── 하위 재귀 (심볼릭 링크·정션 포인트 건너뜀 — 무한 루프 방지) ──
        foreach (var sub in subDirs)
        {
            if (new DirectoryInfo(sub).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                prog.SkippedReparsePoints++;  // B2: 건너뜀 카운터
                continue;
            }
            ScanDirectory(sub, results, prog, progress, depth + 1);
        }

        // ── 빈 폴더 탐지 ──
        if (!skipDetection && _opts.ScanEmptyFolders && subDirs.Length == 0 && files.Length == 0)
        {
            results.Add(new FolderEntry
            {
                Path      = path,
                Kind      = FolderKind.Empty,
                SizeBytes = 0,
                ItemCount = 0
            });
        }
    }

    // B1: 정확한 이름 + 와일드카드 패턴 모두 체크
    private bool IsExcludedFolder(string folderName)
    {
        if (_opts.ExcludedFolderNames.Contains(folderName)) return true;
        foreach (var pattern in _opts.ExcludePatterns)
            if (MatchesWildcard(folderName, pattern)) return true;
        return false;
    }

    private static bool MatchesWildcard(string name, string pattern) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            name,
            "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*").Replace("\\?", ".") + "$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static bool MatchesAnyPattern(string name, IEnumerable<string> patterns)
    {
        foreach (var p in patterns)
            if (MatchesWildcard(name, p)) return true;
        return false;
    }

    private bool IsRecentlyModified(string path)
    {
        try { return (DateTime.Now - Directory.GetLastWriteTime(path)).TotalDays < _opts.MinAgeDays; }
        catch { return false; }
    }

    // 폴더 전체 크기 + 항목 수 단일 순회 계산
    private static void GetDirectorySizeAndCount(string path, out long size, out int count)
    {
        size = 0;
        count = 0;
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
            {
                count++;
                try
                {
                    if (File.Exists(entry))
                        size += new FileInfo(entry).Length;
                }
                catch { /* 건너뜀 */ }
            }
        }
        catch { /* 건너뜀 */ }
    }
}
