namespace WordCloud.Services;

public static class CloudGeneratorService
{
    // 생성 해상도: 모니터 물리 해상도 이상
    public static (int w, int h) GetExportSize()
    {
        var bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds;
        int w = Math.Max(bounds?.Width  ?? 1920, 2560);
        int h = Math.Max(bounds?.Height ?? 1080, 1600);
        return (w, h);
    }

    public static async Task<(SKBitmap? bitmap, Sdcb.WordClouds.WordCloud? wc)> GenerateAsync(
        Dictionary<string, int> frequencies,
        CloudConfig config,
        int exportW,
        int exportH)
    {
        if (frequencies.Count == 0) return (null, null);

        return await Task.Run(() => Generate(frequencies, config, exportW, exportH));
    }

    private static (SKBitmap? bitmap, Sdcb.WordClouds.WordCloud? wc) Generate(
        Dictionary<string, int> frequencies,
        CloudConfig config,
        int exportW,
        int exportH)
    {
        var wordScores = frequencies
            .Select(kv => new WordScore(kv.Key, kv.Value))
            .ToArray();

        int colorCount = 0;
        int total      = wordScores.Length;
        int themeIndex = config.ThemeIndex;

        var orientation = config.Orientation switch
        {
            TextOrientation.Horizontal => TextOrientations.HorizontalOnly,
            TextOrientation.Vertical   => TextOrientations.VerticalOnly,
            TextOrientation.Mixed      => TextOrientations.PreferHorizontal,
            TextOrientation.Random     => TextOrientations.Random,
            _                          => TextOrientations.PreferHorizontal,
        };

        var options = new WordCloudOptions(exportW, exportH, wordScores)
        {
            FontManager       = new FontManager([config.FontName, "맑은 고딕", "Malgun Gothic", "Arial"]),
            TextOrientation   = orientation,
            FontColorAccessor = ctx =>
            {
                var idx = System.Threading.Interlocked.Increment(ref colorCount) - 1;
                return ColorTheme.GetColor(themeIndex, idx, total);
            },
        };

        // 마스크 생성 (Rectangle 제외)
        if (config.Shape != CloudShape.Rectangle)
        {
            var mask = MaskService.Generate(config.Shape, exportW, exportH);
            options.Mask = MaskOptions.CreateWithBackgroundColor(mask, SKColors.White);
        }

        var wordCloud = Sdcb.WordClouds.WordCloud.Create(options);

        // 투명 배경으로 렌더링 (PNG/SVG/클립보드 모두 투명, JPEG는 내보내기 시 합성)
        var bgBmp = new SKBitmap(new SKImageInfo(exportW, exportH, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var bgCanvas = new SKCanvas(bgBmp);
        bgCanvas.Clear(SKColors.Transparent);

        var bitmap = wordCloud.ToSKBitmap(bgBmp);
        return (bitmap, wordCloud);
    }
}
