using System.Runtime.InteropServices;
using System.Windows.Interop;
using SandFall.Sim;

namespace SandFall;

public partial class MainWindow : Window
{
    // ── DWM 다크 타이틀바 ────────────────────────────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    // ── 시뮬레이션 ──────────────────────────────────────────────────
    private SimGrid         _grid       = new();
    private WriteableBitmap _bitmap     = null!;
    private bool            _isPaused   = false;
    private Material        _curMat     = Material.Sand;
    private bool            _isDrawing  = false;

    // ── 성능 측정 ────────────────────────────────────────────────────
    private int      _frameCount;
    private DateTime _fpsTimer = DateTime.UtcNow;

    // ── 물질 팔레트 정의 ─────────────────────────────────────────────
    private static readonly (Material Mat, string Emoji, string Name)[] Palette =
    [
        (Material.Sand,  "🟡", "모래"),
        (Material.Water, "💧", "물"),
        (Material.Fire,  "🔥", "불"),
        (Material.Oil,   "🟤", "기름"),
        (Material.Steam, "💨", "증기"),
        (Material.Stone, "🪨", "돌"),
        (Material.Wood,  "🪵", "나무"),
        (Material.Ice,   "🧊", "얼음"),
        (Material.Seed,  "🌱", "씨앗"),
        (Material.Plant, "🌿", "식물"),
        (Material.Acid,  "🧪", "산"),
        (Material.Ash,   "⬛", "재"),
    ];

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new WindowInteropHelper(this).Handle;
        int val = 1;
        DwmSetWindowAttribute(hwnd, 20, ref val, sizeof(int));

        // WriteableBitmap 생성
        _bitmap = new WriteableBitmap(SimGrid.W, SimGrid.H,
                                      96, 96, PixelFormats.Bgr32, null);
        SimImage.Source = _bitmap;

        // 물질 팔레트 버튼 생성
        BuildPalette();

        // 슬라이더 이벤트
        SldBrush.ValueChanged += (s, ev) =>
            TxtBrush.Text = ((int)SldBrush.Value).ToString();
        SldSpeed.ValueChanged += (s, ev) =>
            TxtSpeed.Text = $"×{(int)SldSpeed.Value}";

        // 렌더링 루프 (CompositionTarget.Rendering = 모니터 주사율)
        CompositionTarget.Rendering += OnRender;
    }

    // ── 렌더링 루프 ──────────────────────────────────────────────────
    private void OnRender(object? sender, EventArgs e)
    {
        if (!_isPaused)
        {
            int steps = (int)SldSpeed.Value;
            for (int i = 0; i < steps; i++)
                _grid.Step();
        }

        // WriteableBitmap 갱신
        _bitmap.Lock();
        unsafe
        {
            var pixels = _grid.Pixels;
            int stride = _bitmap.BackBufferStride / 4;

            for (int y = 0; y < SimGrid.H; y++)
            for (int x = 0; x < SimGrid.W; x++)
            {
                // Bgr32 순서로 쓰기
                int   dstIdx = y * stride + x;
                uint  color  = pixels[y * SimGrid.W + x];
                // BGRA → Bgr32: 알파 무시, B·G·R 채널 그대로
                *((uint*)_bitmap.BackBuffer + dstIdx) = color & 0x00FFFFFF;
            }
        }
        _bitmap.AddDirtyRect(new Int32Rect(0, 0, SimGrid.W, SimGrid.H));
        _bitmap.Unlock();

        // FPS 카운터
        _frameCount++;
        var elapsed = (DateTime.UtcNow - _fpsTimer).TotalSeconds;
        if (elapsed >= 1.0)
        {
            TxtFps.Text = $"FPS: {_frameCount / elapsed:F0}  |  ×{(int)SldSpeed.Value} 속도";
            _frameCount = 0;
            _fpsTimer = DateTime.UtcNow;
        }
    }

    // ── 마우스 입력 ──────────────────────────────────────────────────
    private void SimImage_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDrawing = true;
        DrawAt(e.GetPosition(SimImage));
    }

    private void SimImage_MouseUp(object sender, MouseButtonEventArgs e)
        => _isDrawing = false;

    private void SimImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDrawing && e.LeftButton == MouseButtonState.Pressed)
            DrawAt(e.GetPosition(SimImage));
    }

    private void SimImage_RightMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 우클릭: 지우기
        var pos = e.GetPosition(SimImage);
        var (gx, gy) = ToGridCoord(pos);
        int radius = (int)SldBrush.Value;
        _grid.SetBrush(gx, gy, radius, Material.Empty);
    }

    private void DrawAt(Point pos)
    {
        var (gx, gy) = ToGridCoord(pos);
        int radius = (int)SldBrush.Value;
        _grid.SetBrush(gx, gy, radius, _curMat);
    }

    private (int gx, int gy) ToGridCoord(Point imagePos)
    {
        // Image의 실제 렌더 크기 → 그리드 좌표 변환
        double scaleX = SimGrid.W / SimImage.ActualWidth;
        double scaleY = SimGrid.H / SimImage.ActualHeight;
        int gx = (int)(imagePos.X * scaleX);
        int gy = (int)(imagePos.Y * scaleY);
        return (Math.Clamp(gx, 0, SimGrid.W - 1),
                Math.Clamp(gy, 0, SimGrid.H - 1));
    }

    // ── 팔레트 버튼 생성 ─────────────────────────────────────────────
    private Border? _selectedBtn;

    private void BuildPalette()
    {
        MatPanel.Children.Clear();

        foreach (var (mat, emoji, name) in Palette)
        {
            var btn = new Border
            {
                Width   = 80, Height  = 36,
                Margin  = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D)),
                Background  = new SolidColorBrush(Color.FromRgb(0x21, 0x26, 0x2D)),
                Cursor      = Cursors.Hand,
                Tag         = mat,
            };

            var tb = new TextBlock
            {
                Text = $"{emoji} {name}",
                Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xED, 0xF3)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            };
            btn.Child = tb;

            btn.MouseLeftButtonDown += (s, e) =>
            {
                if (_selectedBtn is not null)
                    _selectedBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D));
                _curMat = (Material)btn.Tag;
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD2, 0x99, 0x22));
                _selectedBtn = btn;
            };

            MatPanel.Children.Add(btn);

            // 기본 선택: Sand
            if (mat == Material.Sand)
            {
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD2, 0x99, 0x22));
                _selectedBtn = btn;
            }
        }
    }

    // ── 제어 버튼 ────────────────────────────────────────────────────
    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        _isPaused = !_isPaused;
        BtnPause.Content = _isPaused ? "▶ 재개" : "⏸ 일시정지";
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
        => _grid.Clear();

    private void BtnRainSand_Click(object sender, RoutedEventArgs e)
    {
        // 상단에 모래 비 효과
        var rng = new Random();
        for (int x = 0; x < SimGrid.W; x += rng.Next(2, 6))
            _grid.Set(x, 0, Material.Sand);
    }

    private void BtnFillWater_Click(object sender, RoutedEventArgs e)
    {
        // 하단 1/4 물 채우기
        for (int y = SimGrid.H * 3 / 4; y < SimGrid.H; y++)
        for (int x = 0; x < SimGrid.W; x++)
        {
            if (_grid.GetType(x, y) == Material.Empty)
                _grid.Set(x, y, Material.Water);
        }
    }
}
