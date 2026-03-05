using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfForge.Services;

public class PdfMergeService
{
    public void Merge(IEnumerable<string> inputPaths, string outputPath)
    {
        using var output = new PdfDocument();
        foreach (var path in inputPaths)
        {
            using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            for (int i = 0; i < doc.PageCount; i++)
                output.AddPage(doc.Pages[i]);
        }
        output.Save(outputPath);
    }
}
