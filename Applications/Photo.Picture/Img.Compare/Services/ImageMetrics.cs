using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImgCompare.Services;

public record ImageMetrics(
    double Mse,         // 평균 제곱 오차
    double Psnr,        // 피크 신호 대 잡음비 (dB)
    double Ssim,        // 구조적 유사도 (-1~1, 높을수록 유사)
    double Mae,         // 평균 절대 오차
    double DiffPercent  // 다른 픽셀 비율 (%)
);

public static class ImageMetricsCalculator
{
    public static ImageMetrics Calculate(WriteableBitmap a, WriteableBitmap b)
    {
        int w = Math.Min(a.PixelWidth, b.PixelWidth);
        int h = Math.Min(a.PixelHeight, b.PixelHeight);
        long n = (long)w * h;

        var arrA = GetPixelBytes(a, w, h);
        var arrB = GetPixelBytes(b, w, h);

        double mse = 0, mae = 0;
        long diffCount = 0;

        double sumMuA = 0, sumMuB = 0, sumSigmaA2 = 0, sumSigmaB2 = 0, sumSigmaAB = 0;

        // MSE / MAE / DiffCount
        for (int i = 0; i < n; i++)
        {
            int base4 = i * 4;
            // Luminance (ITU-R BT.709)
            double la = 0.2126 * arrA[base4 + 2] + 0.7152 * arrA[base4 + 1] + 0.0722 * arrA[base4];
            double lb = 0.2126 * arrB[base4 + 2] + 0.7152 * arrB[base4 + 1] + 0.0722 * arrB[base4];
            double diff = la - lb;
            mse += diff * diff;
            mae += Math.Abs(diff);
            if (Math.Abs(la - lb) > 5) diffCount++;
        }
        mse /= n;
        mae /= n;
        double psnr = mse < 1e-10 ? double.PositiveInfinity : 10 * Math.Log10(255.0 * 255.0 / mse);
        double diffPct = diffCount * 100.0 / n;

        // 간소화된 SSIM (이미지 전체를 하나의 블록으로 처리)
        for (int i = 0; i < n; i++)
        {
            int b4 = i * 4;
            double la = 0.2126 * arrA[b4 + 2] + 0.7152 * arrA[b4 + 1] + 0.0722 * arrA[b4];
            double lb = 0.2126 * arrB[b4 + 2] + 0.7152 * arrB[b4 + 1] + 0.0722 * arrB[b4];
            sumMuA += la; sumMuB += lb;
        }
        double muA = sumMuA / n, muB = sumMuB / n;
        for (int i = 0; i < n; i++)
        {
            int b4 = i * 4;
            double la = 0.2126 * arrA[b4 + 2] + 0.7152 * arrA[b4 + 1] + 0.0722 * arrA[b4];
            double lb = 0.2126 * arrB[b4 + 2] + 0.7152 * arrB[b4 + 1] + 0.0722 * arrB[b4];
            sumSigmaA2 += (la - muA) * (la - muA);
            sumSigmaB2 += (lb - muB) * (lb - muB);
            sumSigmaAB += (la - muA) * (lb - muB);
        }
        double sigmaA2 = sumSigmaA2 / n, sigmaB2 = sumSigmaB2 / n, sigmaAB = sumSigmaAB / n;
        const double c1 = 6.5025, c2 = 58.5225; // (0.01*255)^2 / (0.03*255)^2
        double ssim = ((2 * muA * muB + c1) * (2 * sigmaAB + c2)) /
                      ((muA * muA + muB * muB + c1) * (sigmaA2 + sigmaB2 + c2));

        return new ImageMetrics(mse, psnr, ssim, mae, diffPct);
    }

    public static WriteableBitmap BuildDiffHeatmap(WriteableBitmap a, WriteableBitmap b, double amplify = 5.0)
    {
        int w = Math.Min(a.PixelWidth, b.PixelWidth);
        int h = Math.Min(a.PixelHeight, b.PixelHeight);

        var arrA = GetPixelBytes(a, w, h);
        var arrB = GetPixelBytes(b, w, h);
        var result = new byte[w * h * 4];

        for (int i = 0; i < w * h; i++)
        {
            int b4 = i * 4;
            double dR = Math.Abs(arrA[b4 + 2] - arrB[b4 + 2]) * amplify;
            double dG = Math.Abs(arrA[b4 + 1] - arrB[b4 + 1]) * amplify;
            double dB = Math.Abs(arrA[b4] - arrB[b4]) * amplify;
            double maxD = Math.Max(dR, Math.Max(dG, dB));

            // 히트맵: 파랑(0) → 녹색 → 빨강(255) 스케일
            result[b4] = 0;
            result[b4 + 1] = (byte)Math.Clamp(255 - maxD, 0, 255);
            result[b4 + 2] = (byte)Math.Clamp(maxD, 0, 255);
            result[b4 + 3] = 255;
        }

        var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        wb.WritePixels(new Int32Rect(0, 0, w, h), result, w * 4, 0);
        return wb;
    }

    static byte[] GetPixelBytes(WriteableBitmap wb, int w, int h)
    {
        var converted = new FormatConvertedBitmap(wb, PixelFormats.Bgra32, null, 0);
        var arr = new byte[w * h * 4];
        converted.CopyPixels(new Int32Rect(0, 0, w, h), arr, w * 4, 0);
        return arr;
    }

    public static WriteableBitmap LoadToWriteable(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(path);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        var wb = new WriteableBitmap(bmp);
        return wb;
    }
}
