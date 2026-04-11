namespace FolderPurge.Services;

public class ScanProgress
{
    public int Scanned { get; set; }
    public int Total { get; set; }
    public string CurrentPath { get; set; } = string.Empty;
    public double Percent => Total > 0 ? Math.Min(100.0, Scanned * 100.0 / Total) : 0;
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
            // 1단계: 전체 디렉토리 수 추정 (EnumerateDirectories 스트리밍으로 초기 지연 최소화)
            foreach (var root in rootList)
            {
                _ct.ThrowIfCancellationRequested();
                try
                {
                    foreach (var _ in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
                    {
                        prog.Total++;
                        if (_ct.IsCancellationRequested) break;
                    }
                    prog.Total++; // root 자체
                }
                catch { prog.Total++; }
            }
            progress?.Report(prog);

            // 2단계: 실제 스캔
            foreach (var root in rootList)
            {
                _ct.ThrowIfCancellationRequested();
                ScanDirectory(root, results, prog, progress);
            }
            progress?.Report(prog);
        }, _ct);

        return results;
    }

    private void ScanDirectory(
        string path,
        List<FolderEntry> results,
        ScanProgress prog,
        IProgress<ScanProgress>? progress)
    {
        _ct.ThrowIfCancellationRequested();

        string folderName = Path.GetFileName(path);

        // 제외 폴더 건너뜀
        if (_opts.ExcludedFolderNames.Contains(folderName)) return;

        prog.CurrentPath = path;
        prog.Scanned++;
        if (prog.Scanned % 50 == 0) progress?.Report(prog);

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

        // ── 0바이트 파일 탐지 (나이 필터 미적용) ──
        if (_opts.ScanEmptyFiles)
        {
            foreach (var f in files)
            {
                _ct.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(f);
                    if (fi.Length == 0)
                        results.Add(new FolderEntry
                        {
                            Path      = f,
                            Kind      = FolderKind.EmptyFile,
                            SizeBytes = 0,
                            ItemCount = 0
                        });
                }
                catch { /* 접근 불가 파일은 건너뜀 */ }
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

        // ── 하위 재귀 ──
        foreach (var sub in subDirs)
            ScanDirectory(sub, results, prog, progress);

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

    private bool IsRecentlyModified(string path)
    {
        try { return (DateTime.Now - Directory.GetLastWriteTime(path)).TotalDays < _opts.MinAgeDays; }
        catch { return false; }
    }

    // ── 헬퍼: 폴더 전체 크기 + 항목 수 단일 순회 계산 ──
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
