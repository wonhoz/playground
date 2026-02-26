namespace LogTail.Services;

public static class LogParserService
{
    // 타임스탬프 패턴 (우선순위 순)
    private static readonly Regex[] TimestampPatterns =
    [
        // ISO 8601: 2026-01-01T00:00:00.000Z / 2026-01-01 00:00:00.000
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

    public static LogLevel DetectLevel(string line)
    {
        if (line.Contains("FATAL",   StringComparison.OrdinalIgnoreCase)) return LogLevel.Fatal;
        if (line.Contains("ERROR",   StringComparison.OrdinalIgnoreCase)) return LogLevel.Error;
        if (line.Contains("WARN",    StringComparison.OrdinalIgnoreCase)) return LogLevel.Warn;
        if (line.Contains("INFO",    StringComparison.OrdinalIgnoreCase)) return LogLevel.Info;
        if (line.Contains("DEBUG",   StringComparison.OrdinalIgnoreCase)) return LogLevel.Debug;
        if (line.Contains("TRACE",   StringComparison.OrdinalIgnoreCase)) return LogLevel.Debug;
        if (line.Contains("VERBOSE", StringComparison.OrdinalIgnoreCase)) return LogLevel.Debug;
        return LogLevel.None;
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
}
