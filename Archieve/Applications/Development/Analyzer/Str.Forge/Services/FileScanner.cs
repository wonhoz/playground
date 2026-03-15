using System.IO;
using System.Text;
using StrForge.Models;

namespace StrForge.Services;

public static class FileScanner
{
    private static readonly string[] TextExtensions =
    [
        ".cs", ".vb", ".fs", ".csproj", ".sln", ".slnx",
        ".xaml", ".xml", ".html", ".htm", ".css", ".js", ".ts", ".jsx", ".tsx",
        ".json", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".config",
        ".md", ".txt", ".log", ".csv", ".tsv", ".sql",
        ".sh", ".bat", ".cmd", ".ps1", ".psm1",
        ".py", ".rb", ".go", ".java", ".kt", ".rs", ".c", ".cpp", ".h", ".hpp"
    ];

    public static List<string> ScanFiles(string rootPath, string globPattern)
    {
        if (!Directory.Exists(rootPath)) return [];
        var results = new List<string>();

        // glob 패턴을 파일 확장자 필터로 단순 해석
        // **.cs → *.cs 재귀, *.cs → 해당 폴더만
        var isRecursive = globPattern.StartsWith("**");
        var filePattern = isRecursive ? globPattern[3..] : globPattern; // **/ 제거
        if (string.IsNullOrEmpty(filePattern)) filePattern = "*";

        var option = isRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        try
        {
            results.AddRange(Directory.EnumerateFiles(rootPath, filePattern, option));
        }
        catch { /* 권한 없는 폴더 무시 */ }

        return results.Where(f => IsTextFile(f)).Take(5000).ToList();
    }

    private static bool IsTextFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return TextExtensions.Contains(ext) || ext == string.Empty;
    }

    public static (string content, Encoding encoding) ReadFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var encoding = DetectEncoding(bytes);
        return (encoding.GetString(bytes), encoding);
    }

    private static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(true); // UTF-8 BOM
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode; // UTF-16 LE
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode; // UTF-16 BE
        // UTF-8 without BOM 검증
        try { new UTF8Encoding(false, true).GetString(bytes); return new UTF8Encoding(false); }
        catch { return Encoding.GetEncoding(949); } // CP949 fallback
    }

    public static void WriteFile(string path, string content, Encoding encoding)
    {
        File.WriteAllText(path, content, encoding);
    }
}
