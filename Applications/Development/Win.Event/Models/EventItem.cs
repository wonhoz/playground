namespace WinEvent.Models;

public sealed class EventItem
{
    public DateTime TimeCreated { get; init; }

    /// <summary>0=LogAlways, 1=Critical, 2=Error, 3=Warning, 4=Information, 5=Verbose</summary>
    public int Level { get; init; }

    public string LevelName { get; init; } = "";
    public long EventId { get; init; }
    public string ProviderName { get; init; } = "";
    public string LogName { get; init; } = "";
    public string MessageShort { get; init; } = "";
    public string MessageFull { get; init; } = "";
    public string MachineName { get; init; } = "";
    public long RecordId { get; init; }

    // ── UI 표시용 계산 속성 ──────────────────────────────────────

    public string TimeDisplay => TimeCreated.ToString("yyyy-MM-dd HH:mm:ss");

    public string LevelTag => Level switch
    {
        1 => "위험",
        2 => "오류",
        3 => "경고",
        4 => "정보",
        5 => "Verbose",
        _ => LevelName
    };

    public Brush LevelColor => Level switch
    {
        1 => new SolidColorBrush(Color.FromRgb(0xFF, 0x32, 0x32)),  // 위험 — 빨강
        2 => new SolidColorBrush(Color.FromRgb(0xFF, 0x60, 0x40)),  // 오류 — 주황빨강
        3 => new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00)),  // 경고 — 앰버
        4 => new SolidColorBrush(Color.FromRgb(0x80, 0xC0, 0xFF)),  // 정보 — 하늘
        _ => new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x90))   // Verbose — 회색
    };

    public Brush RowBackground => Level switch
    {
        1 => new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0x20, 0x20)),
        2 => new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0x50, 0x30)),
        3 => new SolidColorBrush(Color.FromArgb(0x0E, 0xFF, 0xB0, 0x00)),
        _ => Brushes.Transparent
    };
}
