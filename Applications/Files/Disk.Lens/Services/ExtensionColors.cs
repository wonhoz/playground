namespace DiskLens.Services;

/// <summary>확장자별 색상 카테고리 매핑</summary>
public static class ExtensionColors
{
    private static readonly Dictionary<string, Color> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        // 비디오 — 파랑
        { ".mp4",  Color.FromRgb(0x42, 0x8B, 0xCA) },
        { ".mkv",  Color.FromRgb(0x42, 0x8B, 0xCA) },
        { ".avi",  Color.FromRgb(0x42, 0x8B, 0xCA) },
        { ".mov",  Color.FromRgb(0x42, 0x8B, 0xCA) },
        { ".wmv",  Color.FromRgb(0x42, 0x8B, 0xCA) },
        { ".flv",  Color.FromRgb(0x42, 0x8B, 0xCA) },
        { ".webm", Color.FromRgb(0x42, 0x8B, 0xCA) },
        { ".m4v",  Color.FromRgb(0x42, 0x8B, 0xCA) },
        { ".ts",   Color.FromRgb(0x42, 0x8B, 0xCA) },

        // 오디오 — 보라
        { ".mp3",  Color.FromRgb(0x9B, 0x59, 0xB6) },
        { ".flac", Color.FromRgb(0x9B, 0x59, 0xB6) },
        { ".wav",  Color.FromRgb(0x9B, 0x59, 0xB6) },
        { ".aac",  Color.FromRgb(0x9B, 0x59, 0xB6) },
        { ".ogg",  Color.FromRgb(0x9B, 0x59, 0xB6) },
        { ".m4a",  Color.FromRgb(0x9B, 0x59, 0xB6) },
        { ".wma",  Color.FromRgb(0x9B, 0x59, 0xB6) },

        // 이미지 — 녹색
        { ".jpg",  Color.FromRgb(0x27, 0xAE, 0x60) },
        { ".jpeg", Color.FromRgb(0x27, 0xAE, 0x60) },
        { ".png",  Color.FromRgb(0x27, 0xAE, 0x60) },
        { ".gif",  Color.FromRgb(0x27, 0xAE, 0x60) },
        { ".bmp",  Color.FromRgb(0x27, 0xAE, 0x60) },
        { ".webp", Color.FromRgb(0x27, 0xAE, 0x60) },
        { ".svg",  Color.FromRgb(0x27, 0xAE, 0x60) },
        { ".tiff", Color.FromRgb(0x27, 0xAE, 0x60) },
        { ".ico",  Color.FromRgb(0x27, 0xAE, 0x60) },
        { ".psd",  Color.FromRgb(0x27, 0xAE, 0x60) },
        { ".raw",  Color.FromRgb(0x27, 0xAE, 0x60) },

        // 문서 — 황금색
        { ".pdf",  Color.FromRgb(0xF3, 0x9C, 0x12) },
        { ".docx", Color.FromRgb(0xF3, 0x9C, 0x12) },
        { ".doc",  Color.FromRgb(0xF3, 0x9C, 0x12) },
        { ".xlsx", Color.FromRgb(0xF3, 0x9C, 0x12) },
        { ".xls",  Color.FromRgb(0xF3, 0x9C, 0x12) },
        { ".pptx", Color.FromRgb(0xF3, 0x9C, 0x12) },
        { ".ppt",  Color.FromRgb(0xF3, 0x9C, 0x12) },
        { ".txt",  Color.FromRgb(0xF3, 0x9C, 0x12) },
        { ".md",   Color.FromRgb(0xF3, 0x9C, 0x12) },
        { ".csv",  Color.FromRgb(0xF3, 0x9C, 0x12) },

        // 코드 — 주황
        { ".cs",   Color.FromRgb(0xE6, 0x7E, 0x22) },
        { ".js",   Color.FromRgb(0xE6, 0x7E, 0x22) },
        { ".ts",   Color.FromRgb(0xE6, 0x7E, 0x22) },
        { ".py",   Color.FromRgb(0xE6, 0x7E, 0x22) },
        { ".java", Color.FromRgb(0xE6, 0x7E, 0x22) },
        { ".cpp",  Color.FromRgb(0xE6, 0x7E, 0x22) },
        { ".c",    Color.FromRgb(0xE6, 0x7E, 0x22) },
        { ".h",    Color.FromRgb(0xE6, 0x7E, 0x22) },
        { ".go",   Color.FromRgb(0xE6, 0x7E, 0x22) },
        { ".rs",   Color.FromRgb(0xE6, 0x7E, 0x22) },
        { ".html", Color.FromRgb(0xE6, 0x7E, 0x22) },
        { ".css",  Color.FromRgb(0xE6, 0x7E, 0x22) },
        { ".xml",  Color.FromRgb(0xE6, 0x7E, 0x22) },
        { ".json", Color.FromRgb(0xE6, 0x7E, 0x22) },

        // 압축 — 노랑
        { ".zip",  Color.FromRgb(0xF1, 0xC4, 0x0F) },
        { ".rar",  Color.FromRgb(0xF1, 0xC4, 0x0F) },
        { ".7z",   Color.FromRgb(0xF1, 0xC4, 0x0F) },
        { ".tar",  Color.FromRgb(0xF1, 0xC4, 0x0F) },
        { ".gz",   Color.FromRgb(0xF1, 0xC4, 0x0F) },
        { ".bz2",  Color.FromRgb(0xF1, 0xC4, 0x0F) },
        { ".xz",   Color.FromRgb(0xF1, 0xC4, 0x0F) },
        { ".iso",  Color.FromRgb(0xF1, 0xC4, 0x0F) },

        // 실행 파일 — 빨강
        { ".exe",  Color.FromRgb(0xE7, 0x4C, 0x3C) },
        { ".dll",  Color.FromRgb(0xE7, 0x4C, 0x3C) },
        { ".msi",  Color.FromRgb(0xE7, 0x4C, 0x3C) },
        { ".bat",  Color.FromRgb(0xE7, 0x4C, 0x3C) },
        { ".cmd",  Color.FromRgb(0xE7, 0x4C, 0x3C) },
        { ".ps1",  Color.FromRgb(0xE7, 0x4C, 0x3C) },
        { ".sh",   Color.FromRgb(0xE7, 0x4C, 0x3C) },

        // 데이터/DB — 시안
        { ".db",   Color.FromRgb(0x16, 0xA0, 0x85) },
        { ".sql",  Color.FromRgb(0x16, 0xA0, 0x85) },
        { ".sqlite", Color.FromRgb(0x16, 0xA0, 0x85) },
        { ".mdf",  Color.FromRgb(0x16, 0xA0, 0x85) },
        { ".bak",  Color.FromRgb(0x16, 0xA0, 0x85) },
    };

    // 시스템/기타 — 회색
    private static readonly Color _systemColor  = Color.FromRgb(0x60, 0x60, 0x70);
    private static readonly Color _folderColor  = Color.FromRgb(0x2C, 0x3E, 0x50);
    private static readonly Color _unknownColor = Color.FromRgb(0x55, 0x55, 0x66);

    public static Color Get(string extension, bool isDirectory = false)
    {
        if (isDirectory) return _folderColor;
        if (string.IsNullOrEmpty(extension)) return _unknownColor;
        return _map.TryGetValue(extension, out var c) ? c : _systemColor;
    }

    /// <summary>범례용 카테고리 목록 (색상, 이름)</summary>
    public static IReadOnlyList<(Color Color, string Label)> Categories { get; } =
    [
        (Color.FromRgb(0x42, 0x8B, 0xCA), "비디오"),
        (Color.FromRgb(0x9B, 0x59, 0xB6), "오디오"),
        (Color.FromRgb(0x27, 0xAE, 0x60), "이미지"),
        (Color.FromRgb(0xF3, 0x9C, 0x12), "문서"),
        (Color.FromRgb(0xE6, 0x7E, 0x22), "코드"),
        (Color.FromRgb(0xF1, 0xC4, 0x0F), "압축"),
        (Color.FromRgb(0xE7, 0x4C, 0x3C), "실행"),
        (Color.FromRgb(0x16, 0xA0, 0x85), "데이터"),
        (Color.FromRgb(0x60, 0x60, 0x70), "시스템"),
        (Color.FromRgb(0x2C, 0x3E, 0x50), "폴더"),
    ];
}
