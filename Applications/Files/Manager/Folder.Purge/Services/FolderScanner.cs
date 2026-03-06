namespace FolderPurge.Services;

public class ScanProgress
{
    public int Scanned { get; set; }
    public string CurrentPath { get; set; } = string.Empty;
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
        var results = new List<FolderEntry>();
        var prog = new ScanProgress();

        await Task.Run(() =>
        {
            foreach (var root in roots)
            {
                _ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(root)) continue;
                ScanDirectory(root, results, prog, progress);
            }
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

        // ── 0바이트 파일 탐지 ──
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
        // 현재 폴더의 하위 폴더 중 bin/obj만 있는지 체크
        if (_opts.ScanVsArtifacts)
        {
            bool hasOnlyArtifactDirs = subDirs.Length > 0
                && subDirs.All(d => _opts.VsArtifactNames.Contains(Path.GetFileName(d)));
            bool hasNoFiles = files.Length == 0;

            // .csproj, .vbproj 등 프로젝트 파일이 없을 때도 포함
            // 단, bin/obj만 있고 파일 없으면 VS 아티팩트 폴더로 간주
            if (hasOnlyArtifactDirs && hasNoFiles && subDirs.Length >= 1)
            {
                long totalSize = GetDirectorySize(path);
                int  totalItems = GetItemCount(path);
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

        // ── 빈 폴더 탐지 (재귀 후 판단) ──
        // 하위 재귀 먼저
        foreach (var sub in subDirs)
            ScanDirectory(sub, results, prog, progress);

        // 이 폴더가 비어있는지 재확인 (재귀 후 하위가 비어서 삭제 대상 등록될 수 있으므로
        // 여기서는 원본 상태로 판단 — 빈 폴더는 처음부터 비어있어야 함)
        if (_opts.ScanEmptyFolders && subDirs.Length == 0 && files.Length == 0)
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

    // ── 헬퍼: 폴더 전체 크기 계산 ──
    private static long GetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { size += new FileInfo(f).Length; }
                catch { /* 건너뜀 */ }
            }
        }
        catch { /* 건너뜀 */ }
        return size;
    }

    private static int GetItemCount(string path)
    {
        int count = 0;
        try
        {
            count += Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
            count += Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Length;
        }
        catch { /* 건너뜀 */ }
        return count;
    }
}
