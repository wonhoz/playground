using System.Security.Cryptography;
using System.Text;

namespace HashCheck.Services;

public enum HashAlgorithmKind { MD5, SHA1, SHA256, SHA512 }

public class FileHashResult
{
    public string FilePath { get; init; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public long FileSize { get; init; }
    public Dictionary<HashAlgorithmKind, string> Hashes { get; init; } = [];
    public TimeSpan Elapsed { get; init; }

    public string SizeText => FileSize switch
    {
        >= 1_073_741_824 => $"{FileSize / 1_073_741_824.0:F2} GB",
        >= 1_048_576     => $"{FileSize / 1_048_576.0:F2} MB",
        >= 1_024         => $"{FileSize / 1024.0:F1} KB",
        _                => $"{FileSize} B"
    };
}

public class HashService
{
    /// <summary>단일 파일의 모든 알고리즘 해시를 동시에 계산.</summary>
    public async Task<FileHashResult> ComputeAllAsync(string path,
        IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fileSize = new FileInfo(path).Length;

        // 모든 알고리즘을 한 번 파일 읽기로 처리
        using var md5    = System.Security.Cryptography.MD5.Create();
        using var sha1   = System.Security.Cryptography.SHA1.Create();
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var sha512 = System.Security.Cryptography.SHA512.Create();

        var algos = new System.Security.Cryptography.HashAlgorithm[] { md5, sha1, sha256, sha512 };

        const int bufSize = 1024 * 1024; // 1MB
        var buf = new byte[bufSize];
        long read = 0;

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufSize, FileOptions.Asynchronous);

        int bytesRead;
        while ((bytesRead = await fs.ReadAsync(buf, ct)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var algo in algos)
                algo.TransformBlock(buf, 0, bytesRead, null, 0);
            read += bytesRead;
            progress?.Report(fileSize > 0 ? (int)(read * 100 / fileSize) : 0);
        }

        foreach (var algo in algos)
            algo.TransformFinalBlock([], 0, 0);

        sw.Stop();
        return new FileHashResult
        {
            FilePath = path,
            FileSize = fileSize,
            Elapsed  = sw.Elapsed,
            Hashes = new Dictionary<HashAlgorithmKind, string>
            {
                [HashAlgorithmKind.MD5]    = ToHex(md5.Hash!),
                [HashAlgorithmKind.SHA1]   = ToHex(sha1.Hash!),
                [HashAlgorithmKind.SHA256] = ToHex(sha256.Hash!),
                [HashAlgorithmKind.SHA512] = ToHex(sha512.Hash!),
            }
        };
    }

    /// <summary>단일 알고리즘으로 파일 해시 계산.</summary>
    public async Task<string> ComputeAsync(string path, HashAlgorithmKind kind,
        IProgress<int>? progress = null, CancellationToken ct = default)
    {
        using var algo = CreateAlgo(kind);
        const int bufSize = 1024 * 1024;
        var buf = new byte[bufSize];
        var fileSize = new FileInfo(path).Length;
        long read = 0;

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufSize, FileOptions.Asynchronous);

        int bytesRead;
        while ((bytesRead = await fs.ReadAsync(buf, ct)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            algo.TransformBlock(buf, 0, bytesRead, null, 0);
            read += bytesRead;
            progress?.Report(fileSize > 0 ? (int)(read * 100 / fileSize) : 0);
        }
        algo.TransformFinalBlock([], 0, 0);
        return ToHex(algo.Hash!);
    }

    private static System.Security.Cryptography.HashAlgorithm CreateAlgo(HashAlgorithmKind kind) =>
        kind switch
        {
            HashAlgorithmKind.MD5    => System.Security.Cryptography.MD5.Create(),
            HashAlgorithmKind.SHA1   => System.Security.Cryptography.SHA1.Create(),
            HashAlgorithmKind.SHA256 => System.Security.Cryptography.SHA256.Create(),
            HashAlgorithmKind.SHA512 => System.Security.Cryptography.SHA512.Create(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    public static string ToHex(byte[] bytes) =>
        Convert.ToHexString(bytes).ToLowerInvariant();

    /// <summary>해시 문자열 비교 (대소문자 무시, 공백 제거).</summary>
    public static bool HashEquals(string a, string b) =>
        a.Trim().Equals(b.Trim(), StringComparison.OrdinalIgnoreCase);
}
