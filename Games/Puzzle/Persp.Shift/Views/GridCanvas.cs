namespace PerspShift.Views;

/// <summary>5×5 실루엣 격자 렌더러 (WPF Canvas 상속)</summary>
public class GridCanvas : System.Windows.Controls.Canvas
{
    private const int    N    = 5;
    private const double Cell = 54;
    private const double Gap  = 3;

    public Action<int, int>? CellClicked { get; set; }

    private bool[,] _current = new bool[N, N];
    private bool[,] _target  = new bool[N, N];

    private readonly Rectangle[,] _rects = new Rectangle[N, N];

    private static readonly SolidColorBrush BrEmpty   = new(WpfColor.FromRgb(0x1A, 0x2A, 0x1A));
    private static readonly SolidColorBrush BrMatch   = new(WpfColor.FromRgb(0x4C, 0xAF, 0x50));
    private static readonly SolidColorBrush BrExcess  = new(WpfColor.FromRgb(0xF4, 0x43, 0x36));
    private static readonly SolidColorBrush BrMissing = new(WpfColor.FromArgb(50, 0x00, 0xBC, 0xD4));
    private static readonly SolidColorBrush BrTarget  = new(WpfColor.FromArgb(160, 0x00, 0xBC, 0xD4));

    public GridCanvas()
    {
        double size = Gap + N * (Cell + Gap);
        Width  = size;
        Height = size;
        Background = new SolidColorBrush(WpfColor.FromRgb(0x12, 0x1E, 0x12));

        for (int r = 0; r < N; r++)
        for (int c = 0; c < N; c++)
        {
            double x = Gap + c * (Cell + Gap);
            double y = Gap + r * (Cell + Gap);

            var rect = new Rectangle
            {
                Width            = Cell,
                Height           = Cell,
                RadiusX          = 5,
                RadiusY          = 5,
                Fill             = BrEmpty,
                Cursor           = Cursors.Hand,
            };

            SetLeft(rect, x);
            SetTop(rect, y);

            int col = c, row = r;
            rect.MouseDown += (_, _) => CellClicked?.Invoke(col, row);

            Children.Add(rect);
            _rects[r, c] = rect;
        }
    }

    public void Update(bool[,] current, bool[,] target)
    {
        _current = current;
        _target  = target;
        Refresh();
    }

    private void Refresh()
    {
        for (int r = 0; r < N; r++)
        for (int c = 0; c < N; c++)
        {
            bool cur = _current[c, r];
            bool tgt = _target[c, r];
            var rect  = _rects[r, c];

            rect.Fill             = (cur, tgt) switch
            {
                (true,  true)  => BrMatch,
                (true,  false) => BrExcess,
                (false, true)  => BrMissing,
                _              => BrEmpty,
            };
            rect.Stroke          = tgt ? BrTarget : null;
            rect.StrokeThickness = tgt ? 1.5 : 0;
        }
    }
}
