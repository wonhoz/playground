using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfForge.Services;

public class PdfSplitService
{
    /// <summary>페이지 범위마다 개별 PDF 저장. pages는 1-based.</summary>
    public List<string> SplitByRanges(string inputPath, IEnumerable<(int From, int To)> ranges, string outputDir)
    {
        using var src = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        var results = new List<string>();
        int idx = 1;
        foreach (var (from, to) in ranges)
        {
            using var doc = new PdfDocument();
            for (int p = from; p <= Math.Min(to, src.PageCount); p++)
                doc.AddPage(src.Pages[p - 1]);
            var outPath = Path.Combine(outputDir, $"split_{idx++:D3}.pdf");
            doc.Save(outPath);
            results.Add(outPath);
        }
        return results;
    }

    /// <summary>각 페이지를 개별 PDF로 저장.</summary>
    public List<string> SplitEachPage(string inputPath, string outputDir)
    {
        using var src = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        var results = new List<string>();
        for (int i = 0; i < src.PageCount; i++)
        {
            using var doc = new PdfDocument();
            doc.AddPage(src.Pages[i]);
            var outPath = Path.Combine(outputDir, $"page_{i + 1:D3}.pdf");
            doc.Save(outPath);
            results.Add(outPath);
        }
        return results;
    }
}
