namespace WordCloud.Services;

public static class ExportService
{
    // PNG: 투명 배경 그대로 저장
    public static void SavePng(SKBitmap bitmap)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "PNG로 저장 (투명 배경)",
            Filter     = "PNG 이미지|*.png",
            DefaultExt = "png",
            FileName   = $"wordcloud_{DateTime.Now:yyyyMMdd_HHmmss}"
        };
        if (dlg.ShowDialog() != true) return;

        using var stream = File.OpenWrite(dlg.FileName);
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
    }

    // JPEG: 투명 배경을 bgColor로 합성 후 저장
    public static void SaveJpeg(SKBitmap bitmap, SKColor bgColor)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "JPEG로 저장",
            Filter     = "JPEG 이미지|*.jpg;*.jpeg",
            DefaultExt = "jpg",
            FileName   = $"wordcloud_{DateTime.Now:yyyyMMdd_HHmmss}"
        };
        if (dlg.ShowDialog() != true) return;

        // JPEG는 투명 불가 → bgColor로 합성
        using var composite = new SKBitmap(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var c = new SKCanvas(composite);
        c.Clear(bgColor);
        c.DrawBitmap(bitmap, 0, 0);

        using var stream = File.OpenWrite(dlg.FileName);
        composite.Encode(stream, SKEncodedImageFormat.Jpeg, 95);
    }

    // SVG: Sdcb.WordCloud의 ToSvg() 활용
    public static void SaveSvg(Sdcb.WordClouds.WordCloud wordCloud)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "SVG 벡터로 저장",
            Filter     = "SVG 파일|*.svg",
            DefaultExt = "svg",
            FileName   = $"wordcloud_{DateTime.Now:yyyyMMdd_HHmmss}"
        };
        if (dlg.ShowDialog() != true) return;

        File.WriteAllText(dlg.FileName, wordCloud.ToSvg(), System.Text.Encoding.UTF8);
    }

    // 클립보드: 투명 배경 PNG
    public static void CopyToClipboard(SKBitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Encode(ms, SKEncodedImageFormat.Png, 100);
        ms.Position = 0;

        var decoder = BitmapDecoder.Create(ms,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();

        System.Windows.Clipboard.SetImage(frame);
    }
}
