namespace LogMerge.Services;

/// <summary>50+ 타임스탬프 포맷 자동 감지 파서</summary>
public static class TimestampParser
{
    private static readonly string[] DateFormats =
    [
        // ISO 8601 variants
        "yyyy-MM-ddTHH:mm:ss.fffffffzzz",
        "yyyy-MM-ddTHH:mm:ss.ffffffzzz",
        "yyyy-MM-ddTHH:mm:ss.fffffzzz",
        "yyyy-MM-ddTHH:mm:ss.ffffzzz",
        "yyyy-MM-ddTHH:mm:ss.fffzzz",
        "yyyy-MM-ddTHH:mm:ss.ffzzz",
        "yyyy-MM-ddTHH:mm:ss.fzzz",
        "yyyy-MM-ddTHH:mm:sszzz",
        "yyyy-MM-ddTHH:mm:ss.fffffff",
        "yyyy-MM-ddTHH:mm:ss.ffffff",
        "yyyy-MM-ddTHH:mm:ss.fffff",
        "yyyy-MM-ddTHH:mm:ss.ffff",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss.ff",
        "yyyy-MM-ddTHH:mm:ss.f",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss",
        // Space-separated date+time
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss.ff",
        "yyyy-MM-dd HH:mm:ss.f",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy/MM/dd HH:mm:ss.fff",
        "yyyy/MM/dd HH:mm:ss",
        // Slash date
        "MM/dd/yyyy HH:mm:ss.fff",
        "MM/dd/yyyy HH:mm:ss",
        "dd/MM/yyyy HH:mm:ss",
        // Compact
        "yyyyMMddHHmmss",
        "yyyyMMdd HHmmss",
        // .NET default
        "yyyy-MM-dd HH:mm:ss,fff",
        "yyyy-MM-dd HH:mm:ss,ff",
        "yyyy-MM-dd HH:mm:ss,f",
        // Log4j/Logback
        "dd MMM yyyy HH:mm:ss.fff",
        "dd MMM yyyy HH:mm:ss",
        "MMM dd HH:mm:ss",
        "MMM  d HH:mm:ss",
        // Unix syslog
        "MMM dd HH:mm:ss",
        // Apache common log
        "dd/MMM/yyyy:HH:mm:ss zzz",
        "dd/MMM/yyyy:HH:mm:ss",
        // Windows event log
        "MM/dd/yyyy hh:mm:ss tt",
        "M/d/yyyy h:mm:ss tt",
        // Korean style
        "yyyy년 M월 d일 H시 m분 s초",
        "yyyy.MM.dd HH:mm:ss",
        "yyyy.MM.dd HH:mm:ss.fff",
    ];

    // 타임스탬프 위치 추출 정규식들 (ordered by specificity)
    private static readonly (Regex Re, string Group)[] TimestampPatterns =
    [
        // ISO 8601 at start
        (new Regex(@"^(?<ts>\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d{1,7})?(?:Z|[+-]\d{2}:?\d{2})?)", RegexOptions.Compiled), "ts"),
        // [HH:mm:ss] bracket style
        (new Regex(@"^\[(?<ts>\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d{1,7})?)\]", RegexOptions.Compiled), "ts"),
        (new Regex(@"^\[(?<ts>\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?)\]", RegexOptions.Compiled), "ts"),
        // Unbracketed HH:mm:ss at start
        (new Regex(@"^(?<ts>\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?)\s", RegexOptions.Compiled), "ts"),
        // Syslog: "MMM dd HH:mm:ss"
        (new Regex(@"^(?<ts>[A-Z][a-z]{2}\s+\d{1,2}\s+\d{2}:\d{2}:\d{2})", RegexOptions.Compiled), "ts"),
        // Anywhere in line: ISO
        (new Regex(@"(?<ts>\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:[.,]\d{1,7})?(?:Z|[+-]\d{2}:?\d{2})?)", RegexOptions.Compiled), "ts"),
        // Anywhere: slash date-time
        (new Regex(@"(?<ts>\d{2}/\d{2}/\d{4}\s\d{2}:\d{2}:\d{2})", RegexOptions.Compiled), "ts"),
    ];

    // 레벨 감지 패턴
    private static readonly (Regex Re, LogLevel Level)[] LevelPatterns =
    [
        (new Regex(@"\b(FATAL|CRITICAL|CRIT)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), LogLevel.Fatal),
        (new Regex(@"\b(ERROR|ERR)\b",            RegexOptions.IgnoreCase | RegexOptions.Compiled), LogLevel.Error),
        (new Regex(@"\b(WARN|WARNING)\b",         RegexOptions.IgnoreCase | RegexOptions.Compiled), LogLevel.Warn),
        (new Regex(@"\b(INFO|INFORMATION)\b",     RegexOptions.IgnoreCase | RegexOptions.Compiled), LogLevel.Info),
        (new Regex(@"\b(DEBUG|DBG)\b",            RegexOptions.IgnoreCase | RegexOptions.Compiled), LogLevel.Debug),
        (new Regex(@"\b(TRACE|TRC|VERBOSE|VRB)\b",RegexOptions.IgnoreCase | RegexOptions.Compiled), LogLevel.Trace),
    ];

    // UUID/TraceId 추출
    private static readonly Regex CorrelationIdRe = new(
        @"\b([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|" +
        @"[0-9a-fA-F]{32}|" +              // 32-char hex
        @"[0-9a-fA-F]{16})\b",             // 16-char hex TraceId
        RegexOptions.Compiled);

    public static DateTime? ParseTimestamp(string line)
    {
        foreach (var (re, group) in TimestampPatterns)
        {
            var m = re.Match(line);
            if (!m.Success) continue;

            var raw = m.Groups[group].Value;
            foreach (var fmt in DateFormats)
            {
                if (DateTime.TryParseExact(raw, fmt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowInnerWhite | DateTimeStyles.AssumeLocal,
                    out var dt))
                    return dt;
            }

            // TryParse fallback
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowInnerWhite | DateTimeStyles.AssumeLocal,
                out var dt2))
                return dt2;
        }
        return null;
    }

    public static LogLevel ParseLevel(string line)
    {
        foreach (var (re, level) in LevelPatterns)
            if (re.IsMatch(line)) return level;
        return LogLevel.None;
    }

    public static List<string> ExtractCorrelationIds(string line)
    {
        var ids = new List<string>();
        foreach (Match m in CorrelationIdRe.Matches(line))
            ids.Add(m.Value);
        return ids;
    }
}
