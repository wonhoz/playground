using System.Globalization;
using System.Text;

namespace Quant.Lab.Core.Data;

public sealed class CsvDataLoader : IDataLoader
{
    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd", "yyyyMMdd"
    };

    private readonly string _path;

    public CsvDataLoader(string path) => _path = path;

    public IReadOnlyList<OhlcBar> Load()
    {
        if (!File.Exists(_path))
            throw new FileNotFoundException($"CSV 파일을 찾을 수 없습니다: {_path}", _path);

        var lines = File.ReadAllLines(_path, DetectEncoding(_path));
        if (lines.Length == 0)
            throw new InvalidDataException($"CSV가 비어 있습니다: {_path}");

        var firstCols = SplitRow(lines[0]);
        bool hasHeader = !TryParseDate(firstCols[0], out _);

        var (dateIdx, openIdx, highIdx, lowIdx, closeIdx, volIdx) =
            hasHeader ? ResolveHeader(firstCols) : (0, 1, 2, 3, 4, 5);

        var bars = new List<OhlcBar>(lines.Length);
        foreach (var line in lines.Skip(hasHeader ? 1 : 0))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var c = SplitRow(line);
            if (!TryParseDate(c[dateIdx], out var d)) continue;

            bars.Add(new OhlcBar(
                d,
                ParseDecimal(c[openIdx]),
                ParseDecimal(c[highIdx]),
                ParseDecimal(c[lowIdx]),
                ParseDecimal(c[closeIdx]),
                volIdx >= 0 && volIdx < c.Length ? ParseLong(c[volIdx]) : 0));
        }

        if (bars.Count == 0)
            throw new InvalidDataException($"CSV에서 유효한 행을 읽지 못했습니다: {_path}");

        return bars.OrderBy(b => b.Date).ToList();
    }

    private static (int d, int o, int h, int l, int c, int v) ResolveHeader(string[] cols)
    {
        int FindAny(params string[] names)
            => Array.FindIndex(cols, c => names.Any(n => string.Equals(c.Trim().Trim('"'), n, StringComparison.OrdinalIgnoreCase)));

        int d = FindAny("Date", "Time", "Timestamp", "Day");
        int o = FindAny("Open");
        int h = FindAny("High");
        int l = FindAny("Low");
        int c = FindAny("Close", "Adj Close", "AdjClose");
        int v = FindAny("Volume", "Vol");

        if (d < 0 || o < 0 || h < 0 || l < 0 || c < 0)
            throw new InvalidDataException("CSV 헤더에서 Date/Open/High/Low/Close 컬럼을 찾지 못했습니다.");
        return (d, o, h, l, c, v);
    }

    private static string[] SplitRow(string line) => line.Split(',');

    private static bool TryParseDate(string s, out DateOnly date)
    {
        s = s.Trim().Trim('"');
        if (DateOnly.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        return DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static decimal ParseDecimal(string s)
        => decimal.Parse(s.Trim().Trim('"').Replace(",", ""), CultureInfo.InvariantCulture);

    private static long ParseLong(string s)
    {
        s = s.Trim().Trim('"').Replace(",", "");
        return string.IsNullOrEmpty(s) ? 0 : long.Parse(s, CultureInfo.InvariantCulture);
    }

    private static Encoding DetectEncoding(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        reader.Peek();
        return reader.CurrentEncoding;
    }
}
