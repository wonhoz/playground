using System.IO;
using System.Text;
using TableCraft.Models;

namespace TableCraft.Services;

/// <summary>CSV/TSV 파싱, 구분자 자동 감지, 컬럼 타입 추론, 최근 파일 관리</summary>
public static class CsvParser
{
    private static readonly string RecentFile =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "TableCraft", "recent.txt");

    // ── 파일 로드 ─────────────────────────────────────────────────────
    public static async Task<(string[] Headers, string[][] Rows, char Delimiter)> LoadAsync(
        string path,
        IProgress<(int Rows, long Bytes)>? progress = null,
        CancellationToken ct = default)
    {
        var encoding  = DetectEncoding(path);
        var delimiter = DetectDelimiter(path, encoding);
        var rows      = new List<string[]>(65536);
        string[]? headers = null;
        long bytesRead = 0;

        await Task.Run(() =>
        {
            using var reader = new StreamReader(path, encoding);
            foreach (var row in ParseCsv(reader, delimiter))
            {
                ct.ThrowIfCancellationRequested();
                if (headers is null)
                {
                    headers = row;
                }
                else
                {
                    // 컬럼 수가 헤더와 다를 경우 패딩/자르기
                    if (row.Length < headers.Length)
                    {
                        var padded = new string[headers.Length];
                        Array.Copy(row, padded, row.Length);
                        rows.Add(padded);
                    }
                    else if (row.Length > headers.Length)
                    {
                        rows.Add(row[..headers.Length]);
                    }
                    else
                    {
                        rows.Add(row);
                    }
                }

                bytesRead += row.Sum(s => s.Length) + headers?.Length ?? 0;
                if (rows.Count % 10000 == 0)
                    progress?.Report((rows.Count, bytesRead));
            }
        }, ct);

        return (headers ?? [], rows.ToArray(), delimiter);
    }

    // ── RFC 4180 CSV 파서 ─────────────────────────────────────────────
    public static IEnumerable<string[]> ParseCsv(TextReader reader, char delimiter)
    {
        var row   = new List<string>(32);
        var field = new StringBuilder(64);
        bool inQuote = false;
        int c;

        while ((c = reader.Read()) >= 0)
        {
            char ch = (char)c;

            if (inQuote)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();    // escaped quote ""
                        field.Append('"');
                    }
                    else
                    {
                        inQuote = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }
            }
            else
            {
                if (ch == '"')
                {
                    inQuote = true;
                }
                else if (ch == delimiter)
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
                else if (ch == '\n')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    if (row.Count > 0)
                    {
                        yield return row.ToArray();
                        row.Clear();
                    }
                }
                else if (ch != '\r')
                {
                    field.Append(ch);
                }
            }
        }

        // 마지막 행
        row.Add(field.ToString());
        if (row.Any(s => s.Length > 0))
            yield return row.ToArray();
    }

    // ── 구분자 자동 감지 ──────────────────────────────────────────────
    public static char DetectDelimiter(string path, Encoding? encoding = null)
    {
        encoding ??= DetectEncoding(path);
        try
        {
            using var reader = new StreamReader(path, encoding);
            var sample = reader.ReadLine() ?? "";
            foreach (var delim in new[] { '\t', ',', ';', '|' })
            {
                if (sample.Contains(delim)) return delim;
            }
        }
        catch { }
        return ',';
    }

    // ── 인코딩 감지 (BOM 기반) ────────────────────────────────────────
    public static Encoding DetectEncoding(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var bom = new byte[4];
            int n = fs.Read(bom, 0, 4);
            if (n >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return new UTF8Encoding(true);
            if (n >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;
            if (n >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;
        }
        catch { }
        return new UTF8Encoding(false);
    }

    // ── 컬럼 타입 추론 ────────────────────────────────────────────────
    public static ColumnType[] InferTypes(string[][] rows, int colCount, int sampleSize = 500)
    {
        var types = new ColumnType[colCount];
        int sample = Math.Min(rows.Length, sampleSize);

        for (int col = 0; col < colCount; col++)
        {
            int intCount  = 0, floatCount = 0, dateCount = 0, boolCount = 0, nonEmpty = 0;

            for (int r = 0; r < sample; r++)
            {
                var val = rows[r].Length > col ? rows[r][col].Trim() : "";
                if (val.Length == 0) continue;
                nonEmpty++;

                if (long.TryParse(val, out _)) intCount++;
                else if (double.TryParse(val, System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out _))
                    floatCount++;
                else if (DateTime.TryParse(val, out _)) dateCount++;
                else if (val.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                         val.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                         val == "1" || val == "0" || val == "Y" || val == "N")
                    boolCount++;
            }

            if (nonEmpty == 0) { types[col] = ColumnType.Text; continue; }

            double threshold = nonEmpty * 0.85;
            if (intCount   >= threshold) types[col] = ColumnType.Integer;
            else if ((intCount + floatCount) >= threshold) types[col] = ColumnType.Float;
            else if (dateCount  >= threshold) types[col] = ColumnType.Date;
            else if (boolCount  >= threshold) types[col] = ColumnType.Boolean;
            else types[col] = ColumnType.Text;
        }

        return types;
    }

    // ── 최근 파일 ─────────────────────────────────────────────────────
    public static List<string> GetRecentFiles()
    {
        try
        {
            if (!File.Exists(RecentFile)) return [];
            return File.ReadAllLines(RecentFile, Encoding.UTF8)
                       .Where(File.Exists)
                       .Take(10)
                       .ToList();
        }
        catch { return []; }
    }

    public static void AddRecentFile(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(RecentFile)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var list = GetRecentFiles();
            list.Remove(path);
            list.Insert(0, path);
            File.WriteAllLines(RecentFile, list.Take(10), Encoding.UTF8);
        }
        catch { }
    }
}
