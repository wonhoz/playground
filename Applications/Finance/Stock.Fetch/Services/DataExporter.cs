using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml;
using Stock.Fetch.Models;

namespace Stock.Fetch.Services;

/// <summary>지원 내보내기 포맷.</summary>
public enum ExportFormat
{
    Csv,
    Tsv,
    Json,
    Xml,
    Markdown,
}

/// <summary>내보내기/표시 컬럼 식별자.</summary>
public enum CandleColumn
{
    Date,
    Open,
    Close,
    Low,
    High,
    Volume,
}

/// <summary>
/// <see cref="StockSeries"/>를 CSV·TSV·JSON·XML·Markdown으로 직렬화·저장한다.
/// 컬럼 순서는 날짜-시가-종가-저가-고가-거래량이며, 선택한 컬럼만 출력할 수 있다.
/// </summary>
public static class DataExporter
{
    /// <summary>컬럼 메타: 식별자·영문 키(CSV/JSON)·한글 라벨(Markdown)·숫자 여부.</summary>
    public sealed record ColumnSpec(CandleColumn Id, string Key, string Label, bool Numeric);

    /// <summary>전체 컬럼(기본 표시/출력 순서: 날짜-시가-종가-저가-고가-거래량).</summary>
    public static readonly IReadOnlyList<ColumnSpec> AllColumns = new[]
    {
        new ColumnSpec(CandleColumn.Date,   "date",   "날짜",   false),
        new ColumnSpec(CandleColumn.Open,   "open",   "시가",   true),
        new ColumnSpec(CandleColumn.Close,  "close",  "종가",   true),
        new ColumnSpec(CandleColumn.Low,    "low",    "저가",   true),
        new ColumnSpec(CandleColumn.High,   "high",   "고가",   true),
        new ColumnSpec(CandleColumn.Volume, "volume", "거래량", true),
    };

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Extension(ExportFormat fmt) => fmt switch
    {
        ExportFormat.Csv => ".csv",
        ExportFormat.Tsv => ".tsv",
        ExportFormat.Json => ".json",
        ExportFormat.Xml => ".xml",
        ExportFormat.Markdown => ".md",
        _ => ".txt"
    };

    public static string FilterLabel(ExportFormat fmt) => fmt switch
    {
        ExportFormat.Csv => "CSV (쉼표 구분)|*.csv",
        ExportFormat.Tsv => "TSV (탭 구분)|*.tsv",
        ExportFormat.Json => "JSON|*.json",
        ExportFormat.Xml => "XML|*.xml",
        ExportFormat.Markdown => "Markdown 표|*.md",
        _ => "텍스트|*.txt"
    };

    /// <summary>선택 컬럼(null이면 전체)을 지정 포맷 문자열로 직렬화.</summary>
    public static string Serialize(StockSeries series, ExportFormat fmt, IReadOnlyList<CandleColumn>? columns = null)
    {
        var specs = Resolve(columns);
        return fmt switch
        {
            ExportFormat.Csv => Delimited(series, specs, ','),
            ExportFormat.Tsv => Delimited(series, specs, '\t'),
            ExportFormat.Json => ToJson(series, specs),
            ExportFormat.Xml => ToXml(series, specs),
            ExportFormat.Markdown => ToMarkdown(series, specs),
            _ => throw new ArgumentOutOfRangeException(nameof(fmt))
        };
    }

    /// <summary>UTF-8(BOM)로 저장. 엑셀에서 한글 메타가 깨지지 않도록 BOM을 포함한다.</summary>
    public static async Task SaveAsync(StockSeries series, ExportFormat fmt,
        IReadOnlyList<CandleColumn>? columns, string path, CancellationToken ct = default)
    {
        string content = Serialize(series, fmt, columns);
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct);
    }

    // 선택 컬럼 → 정의 순서(AllColumns) 유지하며 ColumnSpec 목록으로.
    private static IReadOnlyList<ColumnSpec> Resolve(IReadOnlyList<CandleColumn>? columns)
    {
        if (columns is null || columns.Count == 0) return AllColumns;
        var set = columns.ToHashSet();
        return AllColumns.Where(c => set.Contains(c.Id)).ToList();
    }

    // ────────────────────────────── CSV / TSV ──────────────────────────────
    private static string Delimited(StockSeries s, IReadOnlyList<ColumnSpec> cols, char sep)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(sep, cols.Select(c => c.Key))).Append('\n');
        foreach (var c in s.Candles)
        {
            sb.Append(string.Join(sep, cols.Select(col => Cell(c, col.Id)))).Append('\n');
        }
        return sb.ToString();
    }

    // ────────────────────────────── JSON ──────────────────────────────
    private static string ToJson(StockSeries s, IReadOnlyList<ColumnSpec> cols)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteString("code", s.Code);
            w.WriteString("name", s.Name);
            w.WriteString("market", s.Market);
            w.WriteString("source", s.SourceLabel);
            w.WriteNumber("count", s.Candles.Count);
            w.WriteStartArray("candles");
            foreach (var c in s.Candles)
            {
                w.WriteStartObject();
                foreach (var col in cols)
                {
                    switch (col.Id)
                    {
                        case CandleColumn.Date: w.WriteString("date", c.Date.ToString("yyyy-MM-dd")); break;
                        case CandleColumn.Volume: w.WriteNumber("volume", c.Volume); break;
                        default:
                            // 정수 가격은 trailing zero 없이(343000.000→343000)
                            decimal p = Price(c, col.Id);
                            if (p == Math.Truncate(p)) w.WriteNumber(col.Key, (long)p);
                            else w.WriteNumber(col.Key, p);
                            break;
                    }
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    // ────────────────────────────── XML ──────────────────────────────
    private static string ToXml(StockSeries s, IReadOnlyList<ColumnSpec> cols)
    {
        using var ms = new MemoryStream();
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using (var w = XmlWriter.Create(ms, settings))
        {
            w.WriteStartDocument();
            w.WriteStartElement("stock");
            w.WriteAttributeString("code", s.Code);
            w.WriteAttributeString("name", s.Name);
            w.WriteAttributeString("market", s.Market);
            w.WriteAttributeString("source", s.SourceLabel);
            foreach (var c in s.Candles)
            {
                w.WriteStartElement("candle");
                foreach (var col in cols) w.WriteAttributeString(col.Key, Cell(c, col.Id));
                w.WriteEndElement();
            }
            w.WriteEndElement();
            w.WriteEndDocument();
        }
        return new UTF8Encoding(false).GetString(ms.ToArray());
    }

    // ────────────────────────────── Markdown ──────────────────────────────
    private static string ToMarkdown(StockSeries s, IReadOnlyList<ColumnSpec> cols)
    {
        var sb = new StringBuilder();
        string title = string.IsNullOrEmpty(s.Name) ? s.Code : $"{s.Name} ({s.Code})";
        sb.Append("# ").Append(title).Append(" — ").Append(s.SourceLabel).Append('\n').Append('\n');
        sb.Append("| ").Append(string.Join(" | ", cols.Select(c => c.Label))).Append(" |\n");
        sb.Append('|').Append(string.Concat(cols.Select(_ => "------|"))).Append('\n');
        foreach (var c in s.Candles)
        {
            sb.Append("| ").Append(string.Join(" | ", cols.Select(col => Cell(c, col.Id, thousands: true)))).Append(" |\n");
        }
        return sb.ToString();
    }

    // ────────────────────────────── 값 변환 ──────────────────────────────
    /// <summary>셀 표시 문자열. thousands=true(Markdown)면 거래량에 천단위 콤마.</summary>
    private static string Cell(Candle c, CandleColumn col, bool thousands = false) => col switch
    {
        CandleColumn.Date => c.Date.ToString("yyyy-MM-dd"),
        CandleColumn.Volume => thousands ? c.Volume.ToString("N0", Inv) : c.Volume.ToString(Inv),
        _ => Num(Price(c, col))
    };

    private static decimal Price(Candle c, CandleColumn col) => col switch
    {
        CandleColumn.Open => c.Open,
        CandleColumn.Close => c.Close,
        CandleColumn.Low => c.Low,
        CandleColumn.High => c.High,
        _ => 0m
    };

    /// <summary>불필요한 소수점 0을 제거한 숫자 문자열(예: 53000, 53.34).</summary>
    private static string Num(decimal d)
        => d == Math.Truncate(d)
            ? ((long)d).ToString(Inv)
            : d.ToString("0.####", Inv);
}
