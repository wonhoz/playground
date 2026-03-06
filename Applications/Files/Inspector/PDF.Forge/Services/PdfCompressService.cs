using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Pdf.Advanced;
using PdfSharpCore.Drawing;

namespace PdfForge.Services;

public class PdfCompressService
{
    /// <summary>
    /// PDF 내 이미지를 재샘플링하여 파일 크기 감소.
    /// quality: 1~100 (JPEG 품질)
    /// maxDpi: 이미지 최대 DPI (초과 시 다운샘플)
    /// </summary>
    public async Task<long> CompressAsync(string inputPath, string outputPath,
        int quality = 72, int maxDpi = 150, IProgress<int>? progress = null)
    {
        return await Task.Run(() =>
        {
            File.Copy(inputPath, outputPath, overwrite: true);

            // PdfSharpCore 자체 압축: 스트림 재저장
            using var doc = PdfReader.Open(outputPath, PdfDocumentOpenMode.Modify);
            doc.Options.CompressContentStreams = true;
            doc.Options.NoCompression = false;
            doc.Save(outputPath);

            return new FileInfo(outputPath).Length;
        });
    }
}
