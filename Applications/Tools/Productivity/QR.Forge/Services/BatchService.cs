using QrForge.Models;
using SkiaSharp;
using System.IO;

namespace QrForge.Services;

public record BatchItem(string Name, string Content);

public static class BatchService
{
    public static List<BatchItem> ParseCsv(string csvPath)
    {
        var items = new List<BatchItem>();
        var lines = File.ReadAllLines(csvPath, System.Text.Encoding.UTF8);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var idx = line.IndexOf(',');
            if (idx < 0) continue;

            var name    = line[..idx].Trim().Trim('"');
            var content = line[(idx + 1)..].Trim().Trim('"');

            // 헤더 행 건너뛰기 (name/content 등)
            if (name.Equals("name", StringComparison.OrdinalIgnoreCase) &&
                content.Equals("content", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(content))
                items.Add(new BatchItem(name, content));
        }

        return items;
    }

    public static async Task GenerateAsync(
        IEnumerable<BatchItem> items,
        QrStyle style,
        string outputFolder,
        IProgress<(int done, int total, string current)>? progress = null,
        CancellationToken ct = default)
    {
        var list = items.ToList();
        int total = list.Count;

        Directory.CreateDirectory(outputFolder);

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();

            var item   = list[i];
            var bitmap = QrService.Render(item.Content, style);
            if (bitmap != null)
            {
                string safeName = string.Concat(item.Name.Split(Path.GetInvalidFileNameChars()));
                string outPath  = Path.Combine(outputFolder, $"{safeName}.png");
                ExportService.SavePng(bitmap, outPath);
                bitmap.Dispose();
            }

            progress?.Report((i + 1, total, item.Name));
            await Task.Yield();
        }
    }
}
