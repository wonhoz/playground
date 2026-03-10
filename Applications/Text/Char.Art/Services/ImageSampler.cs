namespace CharArt.Services;

/// <summary>
/// 이미지 → 밝기 행렬 변환 (WPF 내장 API 사용)
/// </summary>
public static class ImageSampler
{
    /// <summary>
    /// BitmapSource를 지정된 열 수로 샘플링해 밝기 행렬 반환.
    /// charAspect = charHeight / charWidth (Consolas ≈ 2.0, 전각 ≈ 1.0)
    /// </summary>
    public static float[,] Sample(BitmapSource source, int cols, double charAspect)
    {
        int srcW = source.PixelWidth;
        int srcH = source.PixelHeight;

        // rows: 문자 종횡비를 고려해 계산
        int rows = Math.Max(1, (int)Math.Round(cols * (double)srcH / srcW / charAspect));

        double scaleX = (double)cols / srcW;
        double scaleY = (double)rows / srcH;

        var scaled = new TransformedBitmap(source,
            new System.Windows.Media.ScaleTransform(scaleX, scaleY));

        var gray = new FormatConvertedBitmap(scaled, PixelFormats.Gray8, null, 0);

        var pixels = new byte[rows * cols];
        gray.CopyPixels(pixels, cols, 0); // stride = cols (Gray8 = 1 byte/pixel)

        var brightness = new float[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                brightness[r, c] = pixels[r * cols + c] / 255f;

        return brightness;
    }
}
