namespace LogMerge.Models;

/// <summary>하나의 로그 소스 파일 정보</summary>
public class LogSource
{
    private static int _nextId;

    public int    Id       { get; } = System.Threading.Interlocked.Increment(ref _nextId);
    public string FilePath { get; set; } = "";
    public string Label    { get; set; } = "";
    public Color  Color    { get; set; } = Colors.White;
    public bool   IsEnabled { get; set; } = true;

    // UI 바인딩용 Brush (Frozen)
    private SolidColorBrush? _brush;
    public SolidColorBrush Brush
    {
        get
        {
            if (_brush == null)
            {
                _brush = new SolidColorBrush(Color);
                _brush.Freeze();
            }
            return _brush;
        }
    }

    // 배지 배경 (반투명)
    private SolidColorBrush? _badgeBrush;
    public SolidColorBrush BadgeBrush
    {
        get
        {
            if (_badgeBrush == null)
            {
                _badgeBrush = new SolidColorBrush(Color.FromArgb(40, Color.R, Color.G, Color.B));
                _badgeBrush.Freeze();
            }
            return _badgeBrush;
        }
    }
}
