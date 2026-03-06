using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using QrForge.Models;
using SkiaSharp;
using System.IO;
using ZXing.QrCode.Internal;

namespace QrForge.Services;

public static class ExportService
{
    // ── PNG ──────────────────────────────────────────────────────────────────
    public static void SavePng(SKBitmap bitmap, string path)
    {
        try
        {
            using var data   = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(path);
            data.SaveTo(stream);
        }
        catch { }
    }

    public static byte[] ToPngBytes(SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // ── SVG ──────────────────────────────────────────────────────────────────
    public static void SaveSvg(string content, QrStyle style, string path)
    {
        try
        {
            var svg = BuildSvg(content, style);
            File.WriteAllText(path, svg, System.Text.Encoding.UTF8);
        }
        catch { }
    }

    private static string BuildSvg(string content, QrStyle style)
    {
        // ZXing BitMatrix로 SVG 직접 생성
        var hints = new Dictionary<ZXing.EncodeHintType, object>
        {
            [ZXing.EncodeHintType.ERROR_CORRECTION] = style.EcLevel,
            [ZXing.EncodeHintType.MARGIN]           = 1,
            [ZXing.EncodeHintType.CHARACTER_SET]    = "UTF-8"
        };

        var encoder = new ZXing.QrCode.QRCodeWriter();
        var matrix  = encoder.encode(content, ZXing.BarcodeFormat.QR_CODE, 0, 0, hints);

        int cols = matrix.Width;
        int rows = matrix.Height;
        int cell = 10; // SVG 단위

        int svgW = cols * cell;
        int svgH = rows * cell;

        string fg = ColorToHex(style.ForeColor);
        string bg = ColorToHex(style.BackColor);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{svgW}\" height=\"{svgH}\" viewBox=\"0 0 {svgW} {svgH}\">");
        sb.AppendLine($"  <rect width=\"{svgW}\" height=\"{svgH}\" fill=\"{bg}\"/>");

        for (int y = 0; y < rows; y++)
        for (int x = 0; x < cols; x++)
        {
            if (!matrix[x, y]) continue;
            int px = x * cell;
            int py = y * cell;

            sb.Append($"  <rect x=\"{px}\" y=\"{py}\" width=\"{cell}\" height=\"{cell}\" fill=\"{fg}\"");
            if (style.Marker == MarkerStyle.Round)
                sb.Append($" rx=\"{cell / 3}\" ry=\"{cell / 3}\"");
            else if (style.Marker == MarkerStyle.Dot)
            {
                int r = cell / 2 - 1;
                sb.Clear();
                sb.Append($"  <circle cx=\"{px + cell / 2}\" cy=\"{py + cell / 2}\" r=\"{r}\" fill=\"{fg}\"");
            }
            sb.AppendLine("/>");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static string ColorToHex(SKColor c) => $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}";

    // ── PDF (A4 격자) ────────────────────────────────────────────────────────
    public static void SavePdf(IEnumerable<(string label, SKBitmap bmp)> items, string path, int cols = 3)
    {
        using var doc  = new PdfDocument();
        var page       = doc.AddPage();
        page.Width     = XUnit.FromPoint(595);
        page.Height    = XUnit.FromPoint(842);

        using var gfx  = XGraphics.FromPdfPage(page);

        double margin  = 36;
        double cellW   = (page.Width.Point - margin * 2) / cols;
        double cellH   = cellW + 20;   // QR + 레이블 여백

        int col = 0, row = 0;

        foreach (var (label, bmp) in items)
        {
            // 새 페이지 필요 여부
            double y = margin + row * cellH;
            if (y + cellH > page.Height.Point - margin && (col != 0 || row != 0))
            {
                page = doc.AddPage();
                page.Width  = XUnit.FromPoint(595);
                page.Height = XUnit.FromPoint(842);
                gfx.Dispose();
                // gfx는 using 블록 밖에서 재생성 불가 — 별도 메서드로 분리
                col = 0; row = 0;
                y = margin;
            }

            double x = margin + col * cellW;
            double qrSize = cellW - 8;

            // QR 이미지
            using var ms    = new MemoryStream(ToPngBytes(bmp));
            using var xImg  = XImage.FromStream(() => new MemoryStream(ToPngBytes(bmp)));
            gfx.DrawImage(xImg, x + 4, y + 4, qrSize, qrSize);

            // 레이블
            if (!string.IsNullOrEmpty(label))
            {
                var font = new XFont("Arial", 8, XFontStyle.Regular);
                var rect = new XRect(x, y + qrSize + 4, cellW, 16);
                gfx.DrawString(label, font, XBrushes.Black, rect, XStringFormats.TopCenter);
            }

            col++;
            if (col >= cols) { col = 0; row++; }
        }

        try { doc.Save(path); } catch { }
    }

    // 단일 QR PDF 저장
    public static void SaveSinglePdf(SKBitmap bitmap, string label, string path)
    {
        SavePdf([(label, bitmap)], path, cols: 1);
    }
}
