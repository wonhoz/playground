using System.Text;
using System.Windows.Media.Imaging;

namespace ZipPeek.Services;

public enum PreviewKind { Text, Image, Hex, Binary }

public class PreviewResult
{
    public PreviewKind Kind { get; init; }
    public string? Text { get; init; }
    public BitmapSource? Image { get; init; }
    public byte[]? Raw { get; init; }
    public string Encoding { get; init; } = "UTF-8";
    public long FileSize { get; init; }
}

public class PreviewService
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".log", ".csv", ".json", ".xml", ".yaml", ".yml",
        ".toml", ".ini", ".cfg", ".config", ".properties", ".env",
        ".cs", ".py", ".js", ".ts", ".java", ".cpp", ".c", ".h",
        ".go", ".rs", ".kt", ".swift", ".rb", ".php", ".html", ".htm",
        ".css", ".scss", ".less", ".sql", ".sh", ".bat", ".ps1",
        ".gitignore", ".editorconfig", ".dockerfile", "dockerfile"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".tiff", ".tga"
    };

    private const int MaxTextPreviewBytes = 512 * 1024; // 512 KB

    public PreviewResult Preview(byte[] data, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        long size = data.Length;

        if (data.Length == 0)
            return new PreviewResult { Kind = PreviewKind.Text, Text = "(빈 파일)", FileSize = 0 };

        if (ImageExtensions.Contains(ext))
        {
            try
            {
                using var ms = new MemoryStream(data);
                var bitmap = BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                return new PreviewResult { Kind = PreviewKind.Image, Image = bitmap, FileSize = size };
            }
            catch { /* 이미지 디코딩 실패 시 Hex로 fallback */ }
        }

        if (TextExtensions.Contains(ext) || IsLikelyText(data))
        {
            var (text, enc) = DecodeText(data);
            return new PreviewResult
            {
                Kind = PreviewKind.Text,
                Text = text,
                Encoding = enc,
                FileSize = size
            };
        }

        return new PreviewResult { Kind = PreviewKind.Hex, Raw = data, FileSize = size };
    }

    private static bool IsLikelyText(byte[] data)
    {
        int check = Math.Min(data.Length, 4096);
        int nonPrintable = 0;
        for (int i = 0; i < check; i++)
        {
            byte b = data[i];
            if (b < 9 || (b > 13 && b < 32 && b != 27)) nonPrintable++;
        }
        return nonPrintable < check * 0.05;
    }

    private static (string text, string enc) DecodeText(byte[] data)
    {
        // BOM 감지
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            return (Encoding.UTF8.GetString(data, 3, Math.Min(data.Length - 3, MaxTextPreviewBytes)), "UTF-8 BOM");
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return (Encoding.Unicode.GetString(data, 2, Math.Min(data.Length - 2, MaxTextPreviewBytes)), "UTF-16 LE");
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            return (Encoding.BigEndianUnicode.GetString(data, 2, Math.Min(data.Length - 2, MaxTextPreviewBytes)), "UTF-16 BE");

        var slice = data.Length > MaxTextPreviewBytes ? data[..MaxTextPreviewBytes] : data;
        try { return (Encoding.UTF8.GetString(slice), "UTF-8"); }
        catch { return (Encoding.Default.GetString(slice), "System"); }
    }

    /// <summary>바이트 배열을 Hex 덤프 문자열로 변환.</summary>
    public static string ToHexDump(byte[] data, int maxBytes = 65536)
    {
        int len = Math.Min(data.Length, maxBytes);
        var sb = new StringBuilder(len * 4);
        const int lineWidth = 16;

        for (int i = 0; i < len; i += lineWidth)
        {
            sb.Append($"{i:X8}  ");
            int end = Math.Min(i + lineWidth, len);
            for (int j = i; j < end; j++)
            {
                sb.Append($"{data[j]:X2} ");
                if (j == i + 7) sb.Append(' ');
            }
            // 패딩
            for (int j = end; j < i + lineWidth; j++)
            {
                sb.Append("   ");
                if (j == i + 7) sb.Append(' ');
            }
            sb.Append(" |");
            for (int j = i; j < end; j++)
                sb.Append(data[j] >= 0x20 && data[j] < 0x7F ? (char)data[j] : '.');
            sb.AppendLine("|");
        }
        if (data.Length > maxBytes)
            sb.AppendLine($"... (이하 {data.Length - maxBytes:N0} 바이트 생략)");
        return sb.ToString();
    }
}
