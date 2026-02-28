using System.IO;
using FileDuplicates.Models;

namespace FileDuplicates.Services;

public record ScanProgress(int Done, int Total, string CurrentFile);

public static class FileScanner
{
    /// <summary>옵션에 따라 Hash + Image 스캔을 실행하고 중복 그룹 목록을 반환합니다.</summary>
    public static async Task<List<DuplicateGroup>> ScanAsync(
        ScanOptions               options,
        IProgress<ScanProgress>?  progress,
        CancellationToken         ct)
    {
        // 1단계: 파일 수집 (UI 차단 방지: Task.Run으로 배경 스레드에서 수행)
        var searchOption = options.IncludeSubfolders
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        progress?.Report(new ScanProgress(0, 0, "파일 목록 수집 중..."));

        var allFiles = await Task.Run(() =>
            options.Folders
                .Where(Directory.Exists)
                .SelectMany(f =>
                {
                    try   { return Directory.EnumerateFiles(f, "*", searchOption); }
                    catch { return []; }
                })
                .Distinct()
                .ToList(), ct);

        var total = allFiles.Count;
        var done  = 0;

        var fileProgress = new Progress<string>(name =>
            progress?.Report(new ScanProgress(Interlocked.Increment(ref done), total, name)));

        var results = new List<DuplicateGroup>();

        // 2단계: SHA256 해시 스캔
        if (options.EnableHashScan)
        {
            var groups = await HashScanner.ScanAsync(allFiles, fileProgress, ct);
            results.AddRange(groups);

            // 이미 Hash 중복으로 묶인 파일은 Image 스캔에서 제외
            var hashDupPaths = results
                .SelectMany(g => g.Files.Select(f => f.Path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            allFiles = allFiles
                .Where(f => !hashDupPaths.Contains(f))
                .ToList();
        }

        // 3단계: 유사 이미지 스캔
        if (options.EnableImageScan)
        {
            var groups = await ImageScanner.ScanAsync(
                allFiles, options.SimilarityThreshold, fileProgress, ct);
            results.AddRange(groups);
        }

        return results
            .OrderByDescending(g => g.TotalSize)
            .ToList();
    }
}
