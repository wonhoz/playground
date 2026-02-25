namespace WordCloud.Services;

public static class CloudGeneratorService
{
    private static readonly Random _rng = new();

    public static async Task<SKBitmap?> GenerateAsync(
        Dictionary<string, int> frequencies,
        CloudConfig config,
        SKBitmap? mask = null)
    {
        if (frequencies.Count == 0) return null;

        return await Task.Run(() => Generate(frequencies, config, mask));
    }

    private static SKBitmap Generate(
        Dictionary<string, int> frequencies,
        CloudConfig config,
        SKBitmap? mask)
    {
        int w = 800, h = 500;
        if (mask != null) { w = mask.Width; h = mask.Height; }

        var wordScores = frequencies
            .Select(kv => new WordScore(kv.Key, kv.Value))
            .ToArray();

        int colorCount = 0;
        int total = wordScores.Length;
        var themeIndex = config.ThemeIndex;

        var orientation = config.Orientation switch
        {
            TextOrientation.Horizontal => TextOrientations.HorizontalOnly,
            TextOrientation.Vertical   => TextOrientations.VerticalOnly,
            TextOrientation.Mixed      => TextOrientations.PreferHorizontal,
            TextOrientation.Random     => TextOrientations.Random,
            _                          => TextOrientations.PreferHorizontal,
        };

        var options = new WordCloudOptions(w, h, wordScores)
        {
            FontManager      = new FontManager([config.FontName, "맑은 고딕", "Malgun Gothic", "Arial"]),
            TextOrientation  = orientation,
            FontColorAccessor = ctx =>
            {
                var idx = System.Threading.Interlocked.Increment(ref colorCount) - 1;
                return ColorTheme.GetColor(themeIndex, idx, total);
            },
        };

        if (mask != null)
            options.Mask = MaskOptions.CreateWithBackgroundColor(mask, SKColors.White);

        var wordCloud = Sdcb.WordClouds.WordCloud.Create(options);

        var bgBmp = new SKBitmap(w, h);
        using var bgCanvas = new SKCanvas(bgBmp);
        bgCanvas.Clear(config.BgColor);

        return wordCloud.ToSKBitmap(bgBmp);
    }
}
