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

/// <summary>
/// <see cref="StockSeries"/>를 CSV·TSV·JSON·XML·Markdown으로 직렬화·저장한다.
/// 컬럼은 date, open, high, low, close, volume 순으로 통일한다.
/// </summary>
public static class DataExporter
{
    private static readonly string[] Headers = ["date", "open", "high", "low", "close", "volume"];

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

    public static string Serialize(StockSeries series, ExportFormat fmt) => fmt switch
    {
        ExportFormat.Csv => Delimited(series, ','),
        ExportFormat.Tsv => Delimited(series, '\t'),
        ExportFormat.Json => ToJson(series),
        ExportFormat.Xml => ToXml(series),
        ExportFormat.Markdown => ToMarkdown(series),
        _ => throw new ArgumentOutOfRangeException(nameof(fmt))
    };

    /// <summary>UTF-8(BOM)로 저장. 엑셀에서 한글 메타가 깨지지 않도록 BOM을 포함한다.</summary>
    public static async Task SaveAsync(StockSeries series, ExportFormat fmt, string path, CancellationToken ct = default)
    {
        string content = Serialize(series, fmt);
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct);
    }

    // ────────────────────────────── CSV / TSV ──────────────────────────────
    private static string Delimited(StockSeries s, char sep)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(sep, Headers));
        foreach (var c in s.Candles)
        {
            sb.Append(c.Date.ToString("yyyy-MM-dd")).Append(sep)
              .Append(Num(c.Open)).Append(sep)
              .Append(Num(c.High)).Append(sep)
              .Append(Num(c.Low)).Append(sep)
              .Append(Num(c.Close)).Append(sep)
              .Append(c.Volume.ToString(CultureInfo.InvariantCulture))
              .Append('\n');
        }
        return sb.ToString();
    }

    // ────────────────────────────── JSON ──────────────────────────────
    private static string ToJson(StockSeries s)
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
                w.WriteString("date", c.Date.ToString("yyyy-MM-dd"));
                w.WriteNumber("open", c.Open);
                w.WriteNumber("high", c.High);
                w.WriteNumber("low", c.Low);
                w.WriteNumber("close", c.Close);
                w.WriteNumber("volume", c.Volume);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    // ────────────────────────────── XML ──────────────────────────────
    private static string ToXml(StockSeries s)
    {
        // 선언 인코딩을 실제 저장 인코딩(UTF-8)과 일치시키기 위해 UTF-8 스트림에 쓴다.
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
                w.WriteAttributeString("date", c.Date.ToString("yyyy-MM-dd"));
                w.WriteAttributeString("open", Num(c.Open));
                w.WriteAttributeString("high", Num(c.High));
                w.WriteAttributeString("low", Num(c.Low));
                w.WriteAttributeString("close", Num(c.Close));
                w.WriteAttributeString("volume", c.Volume.ToString(CultureInfo.InvariantCulture));
                w.WriteEndElement();
            }
            w.WriteEndElement();
            w.WriteEndDocument();
        }
        return new UTF8Encoding(false).GetString(ms.ToArray());
    }

    // ────────────────────────────── Markdown ──────────────────────────────
    private static string ToMarkdown(StockSeries s)
    {
        var sb = new StringBuilder();
        string title = string.IsNullOrEmpty(s.Name) ? s.Code : $"{s.Name} ({s.Code})";
        sb.Append("# ").Append(title).Append(" — ").Append(s.SourceLabel).Append('\n').Append('\n');
        sb.Append("| 날짜 | 시가 | 고가 | 저가 | 종가 | 거래량 |\n");
        sb.Append("|------|------|------|------|------|--------|\n");
        foreach (var c in s.Candles)
        {
            sb.Append("| ").Append(c.Date.ToString("yyyy-MM-dd"))
              .Append(" | ").Append(Num(c.Open))
              .Append(" | ").Append(Num(c.High))
              .Append(" | ").Append(Num(c.Low))
              .Append(" | ").Append(Num(c.Close))
              .Append(" | ").Append(c.Volume.ToString("N0", CultureInfo.InvariantCulture))
              .Append(" |\n");
        }
        return sb.ToString();
    }

    /// <summary>불필요한 소수점 0을 제거한 숫자 문자열(예: 53000, 53.34).</summary>
    private static string Num(decimal d)
        => d == Math.Truncate(d)
            ? ((long)d).ToString(CultureInfo.InvariantCulture)
            : d.ToString("0.####", CultureInfo.InvariantCulture);
}
