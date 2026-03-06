namespace LogLens.Services;

public static partial class LogParserService
{
    // 타임스탬프 패턴 (우선순위 순)
    private static readonly Regex[] TimestampPatterns =
    [
        // ISO 8601 (밀리초 포함)
        new(@"(\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?)",
            RegexOptions.Compiled),
        // 대괄호 포함: [2026-01-01 00:00:00]
        new(@"\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}(?:\.\d+)?)\]",
            RegexOptions.Compiled),
        // 슬래시 형식: 2026/01/01 00:00:00
        new(@"(\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2})",
            RegexOptions.Compiled),
        // syslog 형식: Jan  1 00:00:00
        new(@"^(\w{3} +\d{1,2} \d{2}:\d{2}:\d{2})",
            RegexOptions.Compiled),
    ];

    private static readonly Regex LevelPattern = LevelRegex();

    public static LogLevel DetectLevel(string line)
    {
        var m = LevelPattern.Match(line);
        if (!m.Success) return LogLevel.None;

        return m.Value.ToUpperInvariant() switch
        {
            "FATAL" or "CRIT" or "CRITICAL"       => LogLevel.Fatal,
            "ERR"   or "ERROR"                     => LogLevel.Error,
            "WARN"  or "WARNING"                   => LogLevel.Warn,
            "INF"   or "INFO" or "INFORMATION"     => LogLevel.Info,
            "DBG"   or "DEBUG"                     => LogLevel.Debug,
            "TRC"   or "TRACE" or "VERBOSE"        => LogLevel.Trace,
            _ => LogLevel.None
        };
    }

    public static DateTime? ParseTimestamp(string line)
    {
        foreach (var pattern in TimestampPatterns)
        {
            var m = pattern.Match(line);
            if (!m.Success) continue;
            if (DateTime.TryParse(m.Groups[1].Value, out var dt))
            {
                return dt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Local)
                    : dt;
            }
        }
        return null;
    }

    public static bool IsJsonLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith('{') && trimmed.TrimEnd().EndsWith('}');
    }

    public static LogLine Parse(int number, string raw)
    {
        return new LogLine
        {
            Number    = number,
            Raw       = raw,
            Level     = DetectLevel(raw),
            IsJson    = IsJsonLine(raw),
            Timestamp = ParseTimestamp(raw),
        };
    }

    [GeneratedRegex(@"\b(FATAL|CRIT|CRITICAL|ERR|ERROR|WARN|WARNING|INF|INFO|INFORMATION|DBG|DEBUG|TRC|TRACE|VERBOSE)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex LevelRegex();
}
