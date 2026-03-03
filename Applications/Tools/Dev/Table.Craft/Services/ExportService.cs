using System.IO;
using System.Text;

namespace TableCraft.Services;

/// <summary>CSV/TSV 내보내기</summary>
public static class ExportService
{
    public static async Task ExportAsync(
        string path,
        string[] headers,
        string[][] rows,
        int[] indices,
        char delimiter = ',',
        CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(true));

            // 헤더
            writer.WriteLine(BuildRow(headers, delimiter));

            // 데이터
            foreach (var i in indices)
            {
                ct.ThrowIfCancellationRequested();
                writer.WriteLine(BuildRow(rows[i], delimiter));
            }
        }, ct);
    }

    private static string BuildRow(string[] fields, char delimiter)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0) sb.Append(delimiter);
            var f = fields[i];
            // 구분자, 따옴표, 줄바꿈이 포함되면 인용
            if (f.Contains(delimiter) || f.Contains('"') || f.Contains('\n') || f.Contains('\r'))
            {
                sb.Append('"');
                sb.Append(f.Replace("\"", "\"\""));
                sb.Append('"');
            }
            else
            {
                sb.Append(f);
            }
        }
        return sb.ToString();
    }
}
