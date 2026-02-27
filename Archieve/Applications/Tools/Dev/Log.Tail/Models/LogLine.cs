namespace LogTail.Models;

public class LogLine
{
    // 스레드 안전 정적 브러시 (Frozen → 어느 스레드에서나 사용 가능)
    private static readonly SolidColorBrush BrFatal = Freeze(0xFF, 0x47, 0x47);
    private static readonly SolidColorBrush BrError = Freeze(0xFF, 0x6B, 0x6B);
    private static readonly SolidColorBrush BrWarn  = Freeze(0xFF, 0xD9, 0x3D);
    private static readonly SolidColorBrush BrInfo  = Freeze(0x22, 0xC5, 0x5E);
    private static readonly SolidColorBrush BrDebug = Freeze(0x6E, 0xA8, 0xFE);
    private static readonly SolidColorBrush BrNone  = Freeze(0xC0, 0xC0, 0xD0);

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public int      Number    { get; set; }
    public string   Raw       { get; set; } = "";
    public LogLevel Level     { get; set; } = LogLevel.None;
    public bool     IsJson    { get; set; }
    public DateTime? Timestamp { get; set; }

    public string NumberStr => Number.ToString();

    public SolidColorBrush LevelBrush => Level switch
    {
        LogLevel.Fatal => BrFatal,
        LogLevel.Error => BrError,
        LogLevel.Warn  => BrWarn,
        LogLevel.Info  => BrInfo,
        LogLevel.Debug => BrDebug,
        _              => BrNone,
    };

    // JSON 라인 표시 접두어
    public string DisplayText => IsJson ? $"▸ {Raw}" : Raw;
}
