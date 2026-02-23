using System.Globalization;
using System.Text.RegularExpressions;
using LogLens.Models;

namespace LogLens.Services;

public static partial class LogParser
{
    private static readonly (Regex Pattern, string Format)[] TimestampPatterns =
    [
        (TsIso(), "yyyy-MM-ddTHH:mm:ss"),
        (TsIsoMs(), "yyyy-MM-ddTHH:mm:ss.fff"),
        (TsSpaced(), "yyyy-MM-dd HH:mm:ss"),
        (TsSpacedMs(), "yyyy-MM-dd HH:mm:ss.fff"),
        (TsSlash(), "yyyy/MM/dd HH:mm:ss"),
    ];

    private static readonly Regex LevelPattern = LevelRegex();

    public static LogEntry Parse(string line, int lineNumber)
    {
        var ts = ExtractTimestamp(line);
        var level = ExtractLevel(line);
        return new LogEntry { LineNumber = lineNumber, Text = line, Timestamp = ts, Level = level };
    }

    private static DateTime? ExtractTimestamp(string line)
    {
        foreach (var (pattern, format) in TimestampPatterns)
        {
            var m = pattern.Match(line);
            if (m.Success && DateTime.TryParseExact(m.Value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
        }
        return null;
    }

    private static LogLevel ExtractLevel(string line)
    {
        var m = LevelPattern.Match(line);
        if (!m.Success) return LogLevel.None;

        return m.Value.ToUpperInvariant() switch
        {
            "FATAL" or "CRIT" or "CRITICAL" => LogLevel.Fatal,
            "ERR" or "ERROR" => LogLevel.Error,
            "WARN" or "WARNING" => LogLevel.Warn,
            "INF" or "INFO" or "INFORMATION" => LogLevel.Info,
            "DBG" or "DEBUG" => LogLevel.Debug,
            "TRC" or "TRACE" or "VERBOSE" => LogLevel.Trace,
            _ => LogLevel.None
        };
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}")]
    private static partial Regex TsIsoMs();
    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}")]
    private static partial Regex TsIso();
    [GeneratedRegex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}")]
    private static partial Regex TsSpacedMs();
    [GeneratedRegex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}")]
    private static partial Regex TsSpaced();
    [GeneratedRegex(@"\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}")]
    private static partial Regex TsSlash();
    [GeneratedRegex(@"\b(FATAL|CRIT|CRITICAL|ERR|ERROR|WARN|WARNING|INF|INFO|INFORMATION|DBG|DEBUG|TRC|TRACE|VERBOSE)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LevelRegex();
}
