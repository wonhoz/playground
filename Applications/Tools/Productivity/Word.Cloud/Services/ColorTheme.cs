namespace WordCloud.Services;

public static class ColorTheme
{
    private static readonly Random _rng = new();

    public static readonly string[] Names =
    [
        "Viridis", "Ocean", "Sunset", "Forest",
        "Pastel",  "Mono",  "Rainbow", "Random"
    ];

    private static readonly SKColor[][] _palettes =
    [
        // Viridis
        [
            new SKColor(0x44, 0x01, 0x54),
            new SKColor(0x3B, 0x52, 0x8B),
            new SKColor(0x21, 0x90, 0x8C),
            new SKColor(0x5D, 0xC8, 0x63),
            new SKColor(0xFD, 0xE7, 0x25),
        ],
        // Ocean
        [
            new SKColor(0x03, 0x04, 0x5E),
            new SKColor(0x00, 0x77, 0xB6),
            new SKColor(0x00, 0xB4, 0xD8),
            new SKColor(0x90, 0xE0, 0xEF),
            new SKColor(0xCA, 0xF0, 0xF8),
        ],
        // Sunset
        [
            new SKColor(0xD6, 0x28, 0x28),
            new SKColor(0xF7, 0x7F, 0x00),
            new SKColor(0xFC, 0xBF, 0x49),
            new SKColor(0xE6, 0x39, 0x46),
            new SKColor(0xEA, 0xE2, 0xB7),
        ],
        // Forest
        [
            new SKColor(0x1B, 0x43, 0x32),
            new SKColor(0x2D, 0x6A, 0x4F),
            new SKColor(0x40, 0x91, 0x6C),
            new SKColor(0x74, 0xC6, 0x9D),
            new SKColor(0xB7, 0xE4, 0xC7),
        ],
        // Pastel
        [
            new SKColor(0xFF, 0xB3, 0xC1),
            new SKColor(0xCD, 0xB4, 0xDB),
            new SKColor(0xA2, 0xD2, 0xFF),
            new SKColor(0xBD, 0xE0, 0xFE),
            new SKColor(0xCA, 0xFF, 0xBF),
        ],
        // Mono (Cyan 명도 5단계)
        [
            new SKColor(0x03, 0x41, 0x4A),
            new SKColor(0x06, 0x6B, 0x7A),
            new SKColor(0x06, 0xB6, 0xD4),
            new SKColor(0x67, 0xE8, 0xF9),
            new SKColor(0xB2, 0xEE, 0xF8),
        ],
        // Rainbow
        [
            new SKColor(0xFF, 0x00, 0x00),
            new SKColor(0xFF, 0x77, 0x00),
            new SKColor(0xFF, 0xFF, 0x00),
            new SKColor(0x00, 0xFF, 0x00),
            new SKColor(0x00, 0x00, 0xFF),
            new SKColor(0x8B, 0x00, 0xFF),
        ],
        // Random (placeholder)
        [],
    ];

    public static SKColor[] GetPalette(int themeIndex)
    {
        if (themeIndex < 0 || themeIndex >= _palettes.Length)
            themeIndex = 0;
        return _palettes[themeIndex];
    }

    public static SKColor GetColor(int themeIndex, int index, int total)
    {
        if (themeIndex == 7) // Random
            return new SKColor(
                (byte)_rng.Next(80, 256),
                (byte)_rng.Next(80, 256),
                (byte)_rng.Next(80, 256));

        var palette = GetPalette(themeIndex);
        if (palette.Length == 0) return SKColors.White;
        return palette[index % palette.Length];
    }
}
