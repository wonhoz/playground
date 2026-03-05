using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Drawing;

namespace PdfForge.Services;

public class PdfWatermarkService
{
    /// <summary>
    /// 텍스트 워터마크를 각 페이지 중앙에 대각선으로 삽입.
    /// opacity: 0.0~1.0, rotation: 도(degrees), fontSize: pt
    /// </summary>
    public void AddTextWatermark(string inputPath, string outputPath,
        string text, double fontSize = 64, int rotation = -45,
        byte r = 128, byte g = 128, byte b = 128, double opacity = 0.25)
    {
        using var doc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);
        var font = new XFont("Arial", fontSize, XFontStyle.Bold);
        var brush = new XSolidBrush(XColor.FromArgb((int)(opacity * 255), r, g, b));
        var format = new XStringFormat
        {
            Alignment = XStringAlignment.Center,
            LineAlignment = XLineAlignment.Center
        };

        foreach (var page in doc.Pages)
        {
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            var cx = page.Width / 2;
            var cy = page.Height / 2;
            gfx.RotateAtTransform(rotation, new XPoint(cx, cy));
            gfx.DrawString(text, font, brush,
                new XRect(0, 0, page.Width, page.Height), format);
        }

        doc.Save(outputPath);
    }

    /// <summary>
    /// 이미지 워터마크를 각 페이지 중앙에 삽입.
    /// scale: 페이지 너비 대비 비율 (0.0~1.0), opacity는 XImage 자체가 지원하지 않으므로 전처리된 이미지 사용.
    /// </summary>
    public void AddImageWatermark(string inputPath, string outputPath,
        string imagePath, double scale = 0.4)
    {
        using var doc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);
        using var xImage = XImage.FromFile(imagePath);

        foreach (var page in doc.Pages)
        {
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            double w = page.Width * scale;
            double h = xImage.PixelHeight * w / xImage.PixelWidth;
            double x = (page.Width - w) / 2;
            double y = (page.Height - h) / 2;
            gfx.DrawImage(xImage, x, y, w, h);
        }

        doc.Save(outputPath);
    }
}
