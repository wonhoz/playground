namespace LogMerge.Models;

public enum LogLevel { None, Trace, Debug, Info, Warn, Error, Fatal }

/// <summary>통합 타임라인의 단일 로그 줄</summary>
public class LogEntry
{
    // Frozen 브러시 (스레드 안전)
    private static readonly SolidColorBrush BrFatal  = Freeze(0xFF, 0x47, 0x47);
    private static readonly SolidColorBrush BrError  = Freeze(0xFF, 0x6B, 0x6B);
    private static readonly SolidColorBrush BrWarn   = Freeze(0xFF, 0xD9, 0x3D);
    private static readonly SolidColorBrush BrInfo   = Freeze(0x00, 0xC8, 0xFF);
    private static readonly SolidColorBrush BrDebug  = Freeze(0x6E, 0xA8, 0xFE);
    private static readonly SolidColorBrush BrTrace  = Freeze(0x88, 0x88, 0x98);
    private static readonly SolidColorBrush BrNone   = Freeze(0xC0, 0xC0, 0xD0);

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var b2 = new SolidColorBrush(Color.FromRgb(r, g, b));
        b2.Freeze();
        return b2;
    }

    public int       Id             { get; set; }
    public LogSource Source         { get; set; } = null!;
    public string    Raw            { get; set; } = "";
    public DateTime? Timestamp      { get; set; }
    public LogLevel  Level          { get; set; } = LogLevel.None;
    public List<string> CorrelationIds { get; set; } = [];

    // UI 상태
    public bool IsHighlighted { get; set; }

    public string TimestampDisplay => Timestamp.HasValue
        ? Timestamp.Value.ToString("HH:mm:ss.fff")
        : "──────────";

    public SolidColorBrush LevelBrush => Level switch
    {
        LogLevel.Fatal => BrFatal,
        LogLevel.Error => BrError,
        LogLevel.Warn  => BrWarn,
        LogLevel.Info  => BrInfo,
        LogLevel.Debug => BrDebug,
        LogLevel.Trace => BrTrace,
        _              => BrNone,
    };

    // 하이라이트 배경 (Correlation ID 매칭 시)
    private static readonly SolidColorBrush BrHighlight = Freeze(0x33, 0x20, 0x00);
    private static readonly SolidColorBrush BrNormal    = Freeze(0x00, 0x00, 0x00);

    public SolidColorBrush RowBackground => IsHighlighted ? BrHighlight : BrNormal;
}
