using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PdfForge.Services;

public class PdfPageService
{
    public void RotatePages(string inputPath, string outputPath, IEnumerable<int> pageIndices, int degrees)
    {
        using var doc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);
        foreach (var idx in pageIndices)
        {
            if (idx < 0 || idx >= doc.PageCount) continue;
            var page = doc.Pages[idx];
            page.Rotate = (page.Rotate + degrees + 360) % 360;
        }
        doc.Save(outputPath);
    }

    public void DeletePages(string inputPath, string outputPath, IEnumerable<int> pageIndices)
    {
        using var src = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        using var dst = new PdfDocument();
        var deleteSet = new HashSet<int>(pageIndices);
        for (int i = 0; i < src.PageCount; i++)
            if (!deleteSet.Contains(i))
                dst.AddPage(src.Pages[i]);
        dst.Save(outputPath);
    }

    public void ReorderPages(string inputPath, string outputPath, IList<int> newOrder)
    {
        using var src = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        using var dst = new PdfDocument();
        foreach (var idx in newOrder)
            if (idx >= 0 && idx < src.PageCount)
                dst.AddPage(src.Pages[idx]);
        dst.Save(outputPath);
    }

    public int GetPageCount(string path)
    {
        using var doc = PdfReader.Open(path, PdfDocumentOpenMode.InformationOnly);
        return doc.PageCount;
    }
}
