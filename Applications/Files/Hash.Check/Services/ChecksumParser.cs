namespace HashCheck.Services;

public class ChecksumEntry
{
    public string ExpectedHash { get; init; } = "";
    public string FilePath     { get; init; } = "";  // 상대 경로
    public HashAlgorithmKind Algorithm { get; init; }
    public string? ActualHash  { get; set; }
    public bool? IsMatch       { get; set; }
    public string StatusIcon   => IsMatch switch { true => "✔", false => "✘", _ => "—" };
    public string StatusColor  => IsMatch switch { true => "#22C55E", false => "#EF4444", _ => "#888899" };
}

/// <summary>
/// .md5 / .sha1 / .sha256 / .sha512 / .checksum / .sfv 파일 파싱.
/// BSD 형식 (SHA256 (file) = hash) 및 GNU 형식 (hash  file) 모두 지원.
/// </summary>
public static class ChecksumParser
{
    public static (List<ChecksumEntry> entries, HashAlgorithmKind algo) Parse(string checksumFilePath)
    {
        var ext = Path.GetExtension(checksumFilePath).ToLowerInvariant();
        var algo = ext switch
        {
            ".md5"    => HashAlgorithmKind.MD5,
            ".sha1"   => HashAlgorithmKind.SHA1,
            ".sha256" => HashAlgorithmKind.SHA256,
            ".sha512" => HashAlgorithmKind.SHA512,
            _         => HashAlgorithmKind.SHA256
        };

        var lines = File.ReadAllLines(checksumFilePath);
        var entries = new List<ChecksumEntry>();

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith(';')) continue;

            // BSD 형식: MD5 (filename) = hash
            var bsd = TryParseBsd(line);
            if (bsd.HasValue)
            {
                var (detectedAlgo, file, hash) = bsd.Value;
                entries.Add(new ChecksumEntry { Algorithm = detectedAlgo, FilePath = file, ExpectedHash = hash });
                continue;
            }

            // SFV 형식: filename CRC32
            if (ext == ".sfv")
            {
                var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                    entries.Add(new ChecksumEntry { Algorithm = HashAlgorithmKind.MD5, FilePath = parts[0], ExpectedHash = parts[1] });
                continue;
            }

            // GNU 형식: hash  filename (구분자가 공백 2개 또는 1개)
            var gnu = TryParseGnu(line, algo);
            if (gnu != null)
            {
                entries.Add(gnu);
            }
        }

        return (entries, algo);
    }

    private static (HashAlgorithmKind algo, string file, string hash)? TryParseBsd(string line)
    {
        // SHA256 (file.txt) = abcdef...
        var eqIdx = line.LastIndexOf(" = ", StringComparison.Ordinal);
        if (eqIdx < 0) return null;
        var hashPart = line[(eqIdx + 3)..].Trim();
        var left = line[..eqIdx].Trim();

        int parenOpen  = left.IndexOf('(');
        int parenClose = left.LastIndexOf(')');
        if (parenOpen < 0 || parenClose < 0) return null;

        var algoStr = left[..parenOpen].Trim().ToUpperInvariant().Replace("-", "");
        var file = left[(parenOpen + 1)..parenClose];
        var algo = algoStr switch
        {
            "MD5"    => HashAlgorithmKind.MD5,
            "SHA1"   => HashAlgorithmKind.SHA1,
            "SHA256" => HashAlgorithmKind.SHA256,
            "SHA512" => HashAlgorithmKind.SHA512,
            _ => (HashAlgorithmKind?)null
        };
        if (algo is null) return null;
        return (algo.Value, file, hashPart);
    }

    private static ChecksumEntry? TryParseGnu(string line, HashAlgorithmKind defaultAlgo)
    {
        // hash[공백 1~2개]filename
        var idx = line.IndexOf(' ');
        if (idx < 0) return null;
        var hash = line[..idx];
        var rest = line[(idx + 1)..].TrimStart('*').Trim(); // * = binary mode 표시
        if (string.IsNullOrEmpty(rest)) return null;

        // 해시 길이로 알고리즘 추론
        var algo = hash.Length switch
        {
            32  => HashAlgorithmKind.MD5,
            40  => HashAlgorithmKind.SHA1,
            64  => HashAlgorithmKind.SHA256,
            128 => HashAlgorithmKind.SHA512,
            _   => defaultAlgo
        };
        return new ChecksumEntry { Algorithm = algo, FilePath = rest, ExpectedHash = hash };
    }
}
