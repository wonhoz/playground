using QrForge.Models;
using SkiaSharp;
using System.IO;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using ZXing.Rendering;

namespace QrForge.Services;

public static class QrService
{
    private const int DefaultSize = 512;
    private const float LogoRatio = 0.20f;   // 중앙 20% 로고 영역
    private const float LogoPad   = 0.03f;   // 흰 테두리 비율

    public static SKBitmap? Render(string content, QrStyle style)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.ERROR_CORRECTION] = style.EcLevel,
            [EncodeHintType.MARGIN]           = 1,
            [EncodeHintType.CHARACTER_SET]    = "UTF-8"
        };

        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                ErrorCorrection = style.EcLevel,
                Margin          = 1,
                Width           = DefaultSize,
                Height          = DefaultSize
            }
        };
        writer.Options.Hints.Add(EncodeHintType.CHARACTER_SET, "UTF-8");

        PixelData pixelData;
        try { pixelData = writer.Write(content); }
        catch { return null; }

        var bitmap = new SKBitmap(pixelData.Width, pixelData.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);

        // 배경 채우기
        canvas.Clear(style.BackColor);

        // 전경 그리기
        using var paint = new SKPaint { IsAntialias = true };

        int moduleW = pixelData.Width;
        int moduleH = pixelData.Height;

        // 픽셀 데이터에서 모듈 크기 추정 (여백 제외)
        // ZXing PixelData는 BGRA
        int cellSize = EstimateCellSize(pixelData);

        switch (style.Marker)
        {
            case MarkerStyle.Square:
                DrawSquareModules(canvas, pixelData, style, paint, cellSize);
                break;
            case MarkerStyle.Round:
                DrawRoundModules(canvas, pixelData, style, paint, cellSize);
                break;
            case MarkerStyle.Dot:
                DrawDotModules(canvas, pixelData, style, paint, cellSize);
                break;
        }

        // 로고 합성
        if (!string.IsNullOrEmpty(style.LogoPath) && File.Exists(style.LogoPath))
            DrawLogo(canvas, bitmap.Width, bitmap.Height, style);

        return bitmap;
    }

    // ZXing이 반환하는 픽셀 배열의 단색 블록 크기를 추정
    private static int EstimateCellSize(PixelData pd)
    {
        // 첫 번째 검은 픽셀 행 시작 이후 연속 검은 픽셀 수로 추정
        int w = pd.Width;
        var pixels = pd.Pixels;
        for (int x = 0; x < w; x++)
        {
            byte b = pixels[x * 4];     // B channel
            byte g = pixels[x * 4 + 1]; // G channel
            byte r = pixels[x * 4 + 2]; // R channel
            if (r < 128 && g < 128 && b < 128)
            {
                // 첫 검은 픽셀 발견 — 연속 폭 계산
                int cnt = 1;
                while (x + cnt < w)
                {
                    byte bb = pixels[(x + cnt) * 4];
                    byte bg = pixels[(x + cnt) * 4 + 1];
                    byte br = pixels[(x + cnt) * 4 + 2];
                    if (br < 128 && bg < 128 && bb < 128) cnt++;
                    else break;
                }
                return Math.Max(cnt, 1);
            }
        }
        return 1;
    }

    private static void DrawSquareModules(SKCanvas canvas, PixelData pd, QrStyle style, SKPaint paint, int cellSize)
    {
        paint.Style = SKPaintStyle.Fill;
        int w = pd.Width;
        int h = pd.Height;
        var pixels = pd.Pixels;

        for (int y = 0; y < h; y += cellSize)
        for (int x = 0; x < w; x += cellSize)
        {
            if (IsBlack(pixels, x, y, w))
            {
                paint.Color = style.ForeColor;
                canvas.DrawRect(x, y, cellSize, cellSize, paint);
            }
        }
    }

    private static void DrawRoundModules(SKCanvas canvas, PixelData pd, QrStyle style, SKPaint paint, int cellSize)
    {
        paint.Style = SKPaintStyle.Fill;
        int w = pd.Width;
        int h = pd.Height;
        var pixels = pd.Pixels;
        float r = cellSize * 0.35f;

        for (int y = 0; y < h; y += cellSize)
        for (int x = 0; x < w; x += cellSize)
        {
            if (IsBlack(pixels, x, y, w))
            {
                paint.Color = style.ForeColor;
                canvas.DrawRoundRect(x, y, cellSize, cellSize, r, r, paint);
            }
        }
    }

    private static void DrawDotModules(SKCanvas canvas, PixelData pd, QrStyle style, SKPaint paint, int cellSize)
    {
        paint.Style = SKPaintStyle.Fill;
        int w = pd.Width;
        int h = pd.Height;
        var pixels = pd.Pixels;
        float rad = cellSize * 0.4f;

        for (int y = 0; y < h; y += cellSize)
        for (int x = 0; x < w; x += cellSize)
        {
            if (IsBlack(pixels, x, y, w))
            {
                paint.Color = style.ForeColor;
                float cx = x + cellSize * 0.5f;
                float cy = y + cellSize * 0.5f;
                canvas.DrawCircle(cx, cy, rad, paint);
            }
        }
    }

    private static bool IsBlack(byte[] pixels, int x, int y, int w)
    {
        int idx = (y * w + x) * 4;
        if (idx + 2 >= pixels.Length) return false;
        return pixels[idx + 2] < 128; // R channel
    }

    private static void DrawLogo(SKCanvas canvas, int bmpW, int bmpH, QrStyle style)
    {
        using var logoBmp = SKBitmap.Decode(style.LogoPath);
        if (logoBmp == null) return;

        float logoSize = bmpW * LogoRatio;
        float padSize  = bmpW * LogoPad;
        float totalSize = logoSize + padSize * 2;

        float left = (bmpW - totalSize) / 2f;
        float top  = (bmpH - totalSize) / 2f;

        using var paint = new SKPaint { IsAntialias = true };

        // 흰 배경 원
        paint.Color = SKColors.White;
        canvas.DrawRoundRect(left, top, totalSize, totalSize, padSize, padSize, paint);

        // 로고 그리기
        var logoRect = new SKRect(left + padSize, top + padSize,
                                  left + padSize + logoSize, top + padSize + logoSize);
        canvas.DrawBitmap(logoBmp, logoRect, paint);
    }
}
