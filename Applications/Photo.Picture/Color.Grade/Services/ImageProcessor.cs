namespace Color.Grade.Services;

using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class ImageProcessor
{
    /// <summary>조정값 + LUT를 픽셀 배열에 적용해 새 WriteableBitmap 반환.</summary>
    public static WriteableBitmap Process(
        BitmapSource source,
        ImageAdjustments adj,
        LutInfo? lut,
        Lut3D?   lut3d)
    {
        // BGRx32 형식으로 통일
        var src = new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0);
        int w = src.PixelWidth, h = src.PixelHeight;
        int stride = w * 4;
        var pixels = new byte[stride * h];
        src.CopyPixels(pixels, stride, 0);

        // 사전 계산 LUT (1D, 256엔트리) — 조정값만 적용
        var rLut = new byte[256];
        var gLut = new byte[256];
        var bLut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            float fv = i / 255f;
            rLut[i] = ToByte(ApplyAdj(fv, adj, 0));
            gLut[i] = ToByte(ApplyAdj(fv, adj, 1));
            bLut[i] = ToByte(ApplyAdj(fv, adj, 2));
        }

        bool hasLut = lut3d != null;
        bool hasAlgoLut = lut?.AlgoApply != null;

        for (int pos = 0; pos < pixels.Length; pos += 4)
        {
            float b = bLut[pixels[pos]]     / 255f;
            float g = gLut[pixels[pos + 1]] / 255f;
            float r = rLut[pixels[pos + 2]] / 255f;

            if (hasLut)
            {
                var (nr, ng, nb) = lut3d!.Apply(r, g, b);
                r = nr; g = ng; b = nb;
            }
            else if (hasAlgoLut)
            {
                var (nr, ng, nb) = lut!.AlgoApply!(r, g, b);
                r = nr; g = ng; b = nb;
            }

            pixels[pos]     = ToByte(b);
            pixels[pos + 1] = ToByte(g);
            pixels[pos + 2] = ToByte(r);
        }

        var wb = new WriteableBitmap(w, h, source.DpiX, source.DpiY, PixelFormats.Bgr32, null);
        wb.WritePixels(new System.Windows.Int32Rect(0, 0, w, h), pixels, stride, 0);
        return wb;
    }

    // ── 보조 메서드 ───────────────────────────────────────────────────────

    static float ApplyAdj(float v, ImageAdjustments a, int ch)
    {
        // Exposure (EV)
        v *= MathF.Pow(2f, (float)a.Exposure);

        // Highlights (밝은 영역 조정)
        if (a.Highlights != 0)
        {
            float hlFactor = v * v; // 밝을수록 더 영향
            v += (float)(a.Highlights * 0.5 * hlFactor);
        }

        // Shadows (어두운 영역 조정)
        if (a.Shadows != 0)
        {
            float shFactor = (1f - v) * (1f - v);
            v += (float)(a.Shadows * 0.5 * shFactor);
        }

        v = Math.Clamp(v, 0f, 1f);

        // Contrast: S-curve
        if (a.Contrast != 0)
        {
            float c = (float)(1.0 + a.Contrast * 1.5);
            v = Math.Clamp((v - 0.5f) * c + 0.5f, 0f, 1f);
        }

        // Saturation (휘도 기반)
        if (a.Saturation != 0 && ch >= 0)
        {
            // 단일 채널로는 휘도 계산 불가 → 각 채널에 개별 적용 (근사)
            // 실제 채도는 RGB 모두 필요하므로 여기서는 채도 근사만 적용
            // 실제 처리는 Process()에서 RGB 함께 처리하는 방식 사용
        }

        // Temperature
        if (a.Temperature != 0)
        {
            float t = (float)a.Temperature * 0.12f;
            if (ch == 2) v = Math.Clamp(v + t, 0f, 1f);       // R 채널
            if (ch == 0) v = Math.Clamp(v - t, 0f, 1f);       // B 채널
        }

        return v;
    }

    static byte ToByte(float v) => (byte)(Math.Clamp(v * 255f + 0.5f, 0, 255));

    /// <summary>Saturation이 필요한 경우 RGB 함께 처리하는 오버로드.</summary>
    public static (float r, float g, float b) ApplySaturation(float r, float g, float b, double saturation)
    {
        if (saturation == 0) return (r, g, b);
        float lum = r * 0.299f + g * 0.587f + b * 0.114f;
        float s   = (float)(1.0 + saturation);
        return (
            Math.Clamp(lum + (r - lum) * s, 0f, 1f),
            Math.Clamp(lum + (g - lum) * s, 0f, 1f),
            Math.Clamp(lum + (b - lum) * s, 0f, 1f)
        );
    }

    /// <summary>전체 처리 (채도 포함 RGB 동시 처리).</summary>
    public static WriteableBitmap ProcessFull(
        BitmapSource source,
        ImageAdjustments adj,
        LutInfo? lut,
        Lut3D? lut3d)
    {
        var src = new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0);
        int w = src.PixelWidth, h = src.PixelHeight;
        int stride = w * 4;
        var pixels = new byte[stride * h];
        src.CopyPixels(pixels, stride, 0);

        bool hasLut     = lut3d != null;
        bool hasAlgoLut = lut?.AlgoApply != null && lut3d == null;

        for (int pos = 0; pos < pixels.Length; pos += 4)
        {
            float b = pixels[pos]     / 255f;
            float g = pixels[pos + 1] / 255f;
            float r = pixels[pos + 2] / 255f;

            // 1. Exposure
            float ev = MathF.Pow(2f, (float)adj.Exposure);
            r *= ev; g *= ev; b *= ev;

            // 2. Highlights
            if (adj.Highlights != 0)
            {
                float lum = r * 0.299f + g * 0.587f + b * 0.114f;
                float hf  = lum * lum;
                float delta = (float)(adj.Highlights * 0.5 * hf);
                r += delta; g += delta; b += delta;
            }

            // 3. Shadows
            if (adj.Shadows != 0)
            {
                float lum = r * 0.299f + g * 0.587f + b * 0.114f;
                float sf  = (1f - lum) * (1f - lum);
                float delta = (float)(adj.Shadows * 0.5 * sf);
                r += delta; g += delta; b += delta;
            }

            r = Math.Clamp(r, 0f, 1f);
            g = Math.Clamp(g, 0f, 1f);
            b = Math.Clamp(b, 0f, 1f);

            // 4. Contrast
            if (adj.Contrast != 0)
            {
                float c = (float)(1.0 + adj.Contrast * 1.5);
                r = Math.Clamp((r - 0.5f) * c + 0.5f, 0f, 1f);
                g = Math.Clamp((g - 0.5f) * c + 0.5f, 0f, 1f);
                b = Math.Clamp((b - 0.5f) * c + 0.5f, 0f, 1f);
            }

            // 5. Saturation
            if (adj.Saturation != 0)
                (r, g, b) = ApplySaturation(r, g, b, adj.Saturation);

            // 6. Temperature
            if (adj.Temperature != 0)
            {
                float t = (float)adj.Temperature * 0.12f;
                r = Math.Clamp(r + t, 0f, 1f);
                b = Math.Clamp(b - t, 0f, 1f);
            }

            // 7. LUT
            if (hasLut)
            {
                var (nr, ng, nb) = lut3d!.Apply(r, g, b);
                r = nr; g = ng; b = nb;
            }
            else if (hasAlgoLut)
            {
                var (nr, ng, nb) = lut!.AlgoApply!(r, g, b);
                r = nr; g = ng; b = nb;
            }

            pixels[pos]     = ToByte(b);
            pixels[pos + 1] = ToByte(g);
            pixels[pos + 2] = ToByte(r);
        }

        var wb = new WriteableBitmap(w, h, source.DpiX, source.DpiY, PixelFormats.Bgr32, null);
        wb.WritePixels(new System.Windows.Int32Rect(0, 0, w, h), pixels, stride, 0);
        return wb;
    }

    /// <summary>미리보기용 리사이즈.</summary>
    public static BitmapSource ResizeForPreview(BitmapSource source, int maxSize = 800)
    {
        if (source.PixelWidth <= maxSize && source.PixelHeight <= maxSize) return source;
        double scale = Math.Min((double)maxSize / source.PixelWidth,
                                (double)maxSize / source.PixelHeight);
        var tb = new TransformedBitmap(source,
            new System.Windows.Media.ScaleTransform(scale, scale));
        tb.Freeze();
        return tb;
    }
}
