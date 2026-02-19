using System.IO;
using System.Security.Cryptography;
using FileDuplicates.Models;

namespace FileDuplicates.Services;

public static class HashScanner
{
    /// <summary>파일 목록에서 SHA256 기반 중복 그룹을 반환합니다.</summary>
    public static async Task<List<DuplicateGroup>> ScanAsync(
        IEnumerable<string> files,
        IProgress<string>?  progress,
        CancellationToken   ct)
    {
        // 1단계: 크기로 사전 필터 (크기가 다르면 중복 불가)
        var sizeGroups = files
            .Where(f => { try { return new FileInfo(f).Length > 0; } catch { return false; } })
            .GroupBy(f => new FileInfo(f).Length)
            .Where(g => g.Count() > 1)
            .ToList();

        // 2단계: 같은 크기 그룹 내에서 SHA256 비교
        var results = new List<DuplicateGroup>();

        foreach (var sg in sizeGroups)
        {
            ct.ThrowIfCancellationRequested();

            var hashMap = new Dictionary<string, List<FileEntry>>();

            foreach (var path in sg)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(Path.GetFileName(path));

                try
                {
                    var hash = await ComputeSHA256Async(path, ct);
                    if (!hashMap.TryGetValue(hash, out var list))
                        hashMap[hash] = list = [];
                    list.Add(new FileEntry
                    {
                        Path         = path,
                        Size         = new FileInfo(path).Length,
                        LastModified = File.GetLastWriteTime(path)
                    });
                }
                catch { /* 읽기 오류는 건너뜀 */ }
            }

            results.AddRange(
                hashMap.Values
                    .Where(g => g.Count > 1)
                    .Select(g => new DuplicateGroup { Type = GroupType.Hash, Files = g }));
        }

        return results;
    }

    private static async Task<string> ComputeSHA256Async(string path, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        var bytes = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(bytes);
    }
}
