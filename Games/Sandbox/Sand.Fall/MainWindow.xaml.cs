using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
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

    // ── 드래그 보간용 이전 위치 ──────────────────────────────────────
    private Point _lastDrawPos  = new(-1, -1);
    private Point _lastErasePos = new(-1, -1);

    // ── 성능 측정 ────────────────────────────────────────────────────
    private int      _frameCount;
    private DateTime _fpsTimer = DateTime.UtcNow;

    // ── 물질 팔레트 정의 ─────────────────────────────────────────────
    private static readonly (Material Mat, string Emoji, string Name, string Hint)[] Palette =
    [
        (Material.Sand,      "🟡", "모래",   "분말 — 아래로 낙하, 액체 위에 쌓임"),
        (Material.Water,     "💧", "물",     "액체 — 아래·수평으로 흐름\n불에 닿으면 증기로"),
        (Material.Fire,      "🔥", "불",     "불꽃 — 가연성 물질 점화\n물→증기, 얼음→물"),
        (Material.Oil,       "🟤", "기름",   "액체 — 물 위에 뜸, 불에 잘 탐"),
        (Material.Steam,     "💨", "증기",   "기체 — 위로 상승, 시간이 지나면 소멸·물로"),
        (Material.Stone,     "🪨", "돌",     "고체 — 이동 없음, 불·산에 강함"),
        (Material.Wood,      "🪵", "나무",   "고체 — 이동 없음, 불에 탐"),
        (Material.Ice,       "🧊", "얼음",   "고체 — 인근 물을 얼림\n불에 녹아 물이 됨"),
        (Material.Seed,      "🌱", "씨앗",   "분말 — 물에 닿으면 식물로 성장"),
        (Material.Plant,     "🌿", "식물",   "고체 — 물을 흡수해 성장, 불에 탐"),
        (Material.Acid,      "🧪", "산",     "액체 — 대부분의 물질 용해\n물·산 제외"),
        (Material.Ash,       "⬛", "재",     "분말 — 불이 소멸 후 남음"),
        (Material.Gunpowder, "💣", "화약",   "분말 — 불에 닿으면 폭발!\n반경 10 내 불 생성"),
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

        // FPS + 파티클 카운터
        _frameCount++;
        var elapsed = (DateTime.UtcNow - _fpsTimer).TotalSeconds;
        if (elapsed >= 1.0)
        {
            TxtFps.Text = $"FPS: {_frameCount / elapsed:F0}  |  ×{(int)SldSpeed.Value} 속도";
            TxtParticles.Text = $"파티클: {_grid.ParticleCount:N0}";
            _frameCount = 0;
            _fpsTimer = DateTime.UtcNow;
        }
    }

    // ── 마우스 입력 ──────────────────────────────────────────────────
    private void SimImage_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDrawing = true;
        _lastDrawPos = new(-1, -1);
        DrawAt(e.GetPosition(SimImage));
    }

    private void SimImage_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDrawing = false;
        _lastDrawPos = new(-1, -1);
    }

    private void SimImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDrawing && e.LeftButton == MouseButtonState.Pressed)
            DrawAt(e.GetPosition(SimImage));
        else if (e.RightButton == MouseButtonState.Pressed)
            EraseAt(e.GetPosition(SimImage));
    }

    private void SimImage_RightMouseDown(object sender, MouseButtonEventArgs e)
    {
        Mouse.OverrideCursor = Cursors.No; // 지우기 커서
        _lastErasePos = new(-1, -1);
        EraseAt(e.GetPosition(SimImage));
    }

    private void DrawAt(Point pos)
    {
        var (gx, gy) = ToGridCoord(pos);
        if (gx < 0) return;
        int radius = (int)SldBrush.Value;

        if (_lastDrawPos.X >= 0)
        {
            var (lx, ly) = ToGridCoord(_lastDrawPos);
            BrushLine(lx, ly, gx, gy, radius, _curMat);
        }
        else
        {
            _grid.SetBrush(gx, gy, radius, _curMat);
        }
        _lastDrawPos = pos;
    }

    private void EraseAt(Point pos)
    {
        var (gx, gy) = ToGridCoord(pos);
        if (gx < 0) return;
        int radius = (int)SldBrush.Value;

        if (_lastErasePos.X >= 0)
        {
            var (lx, ly) = ToGridCoord(_lastErasePos);
            BrushLine(lx, ly, gx, gy, radius, Material.Empty);
        }
        else
        {
            _grid.SetBrush(gx, gy, radius, Material.Empty);
        }
        _lastErasePos = pos;
    }

    // ── Bresenham 직선 보간 ──────────────────────────────────────────
    private void BrushLine(int x0, int y0, int x1, int y1, int radius, Material mat)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            _grid.SetBrush(x0, y0, radius, mat);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    private (int gx, int gy) ToGridCoord(Point imagePos)
    {
        // Image의 실제 렌더 크기 → 그리드 좌표 변환
        if (SimImage.ActualWidth <= 0 || SimImage.ActualHeight <= 0) return (-1, -1);
        double scaleX = SimGrid.W / SimImage.ActualWidth;
        double scaleY = SimGrid.H / SimImage.ActualHeight;
        int gx = (int)(imagePos.X * scaleX);
        int gy = (int)(imagePos.Y * scaleY);
        return (Math.Clamp(gx, 0, SimGrid.W - 1),
                Math.Clamp(gy, 0, SimGrid.H - 1));
    }

    // ── 팔레트 버튼 생성 ─────────────────────────────────────────────
    private Border? _selectedBtn;
    private readonly List<Border> _paletteButtons = new();

    private void BuildPalette()
    {
        MatPanel.Children.Clear();
        _paletteButtons.Clear();

        foreach (var (mat, emoji, name, hint) in Palette)
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
                ToolTip     = hint,
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

            btn.MouseLeftButtonDown += (s, e) => SelectPaletteButton(btn);

            MatPanel.Children.Add(btn);
            _paletteButtons.Add(btn);

            // 기본 선택: Sand
            if (mat == Material.Sand)
                SelectPaletteButton(btn);
        }
    }

    private void SelectPaletteButton(Border btn)
    {
        if (_selectedBtn is not null)
            _selectedBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D));
        _curMat = (Material)btn.Tag;
        btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
        _selectedBtn = btn;
    }

    // ── 키보드 단축키 ────────────────────────────────────────────────
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Space: 일시정지/재개
        if (e.Key == Key.Space)
        {
            _isPaused = !_isPaused;
            BtnPause.Content = _isPaused ? "▶ 재개" : "⏸ 일시정지";
            e.Handled = true;
            return;
        }

        // Ctrl+S: 상태 저장
        if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            SaveState();
            e.Handled = true;
            return;
        }

        // Ctrl+O: 상태 불러오기
        if (e.Key == Key.O && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            LoadState();
            e.Handled = true;
            return;
        }

        // F1: 도움말
        if (e.Key == Key.F1)
        {
            ShowHelp();
            e.Handled = true;
            return;
        }

        // 숫자키 1-9, 0 → 팔레트 선택 (Palette 배열 순서)
        int idx = e.Key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            Key.D6 or Key.NumPad6 => 5,
            Key.D7 or Key.NumPad7 => 6,
            Key.D8 or Key.NumPad8 => 7,
            Key.D9 or Key.NumPad9 => 8,
            Key.D0 or Key.NumPad0 => 9,
            _ => -1
        };
        if (idx >= 0 && idx < _paletteButtons.Count)
        {
            SelectPaletteButton(_paletteButtons[idx]);
            e.Handled = true;
        }
    }

    // ── 상태 저장 / 불러오기 ─────────────────────────────────────────
    private void SaveState()
    {
        var dlg = new SaveFileDialog
        {
            Filter   = "Sand.Fall 저장|*.sfall",
            FileName = $"SandFall_{DateTime.Now:yyyyMMdd_HHmmss}.sfall",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            using var stream = File.OpenWrite(dlg.FileName);
            _grid.Save(stream);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadState()
    {
        var dlg = new OpenFileDialog { Filter = "Sand.Fall 저장|*.sfall" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            using var stream = File.OpenRead(dlg.FileName);
            if (!_grid.Load(stream))
                MessageBox.Show("파일 형식이 맞지 않거나 해상도가 다릅니다.", "불러오기 실패",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"불러오기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)  => SaveState();
    private void BtnLoad_Click(object sender, RoutedEventArgs e)  => LoadState();
    private void BtnHelp_Click(object sender, RoutedEventArgs e)  => ShowHelp();

    // ── 도움말 ───────────────────────────────────────────────────────
    private void ShowHelp()
    {
        const string msg =
            "[ 마우스 ]\n" +
            "  왼쪽 클릭·드래그  — 물질 배치\n" +
            "  오른쪽 클릭·드래그 — 지우기\n" +
            "  마우스 휠 ↑↓     — 브러시 크기 조절\n\n" +
            "[ 키보드 ]\n" +
            "  Space        — 일시정지 / 재개\n" +
            "  1~9, 0       — 팔레트 1~10번 선택\n" +
            "  Ctrl + S     — 현재 상태 저장 (.sfall)\n" +
            "  Ctrl + O     — 저장 파일 불러오기\n" +
            "  F1           — 이 도움말\n\n" +
            "[ 반응 관계 ]\n" +
            "  불 + 나무/기름/씨앗/식물/화약 → 불\n" +
            "  불 + 물  → 증기\n" +
            "  불 + 얼음 → 물\n" +
            "  화약 + 불 → 폭발 (반경 10)\n" +
            "  씨앗 + 물 → 식물\n" +
            "  얼음 → 인근 물 동결\n" +
            "  산   → 대부분의 물질 용해";

        MessageBox.Show(msg, "Sand.Fall — 도움말 (F1)",
                        MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── 마우스 휠 브러시 크기 조절 ───────────────────────────────────
    private void SimImage_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double delta = e.Delta > 0 ? 1 : -1;
        SldBrush.Value = Math.Clamp(SldBrush.Value + delta, SldBrush.Minimum, SldBrush.Maximum);
    }

    // ── 스크린샷 저장 ────────────────────────────────────────────────
    private void SaveScreenshot()
    {
        var dlg = new SaveFileDialog
        {
            Filter   = "PNG 이미지|*.png",
            FileName = $"SandFall_{DateTime.Now:yyyyMMdd_HHmmss}.png",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_bitmap));
            using var stream = File.OpenWrite(dlg.FileName);
            encoder.Save(stream);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 해상도 변경 ──────────────────────────────────────────────────
    private void CmbRes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CmbRes.SelectedItem is not ComboBoxItem item) return;

        var parts = ((string)item.Tag).Split(',');
        int w = int.Parse(parts[0]), h = int.Parse(parts[1]);

        _grid   = new SimGrid(w, h);
        _bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
        SimImage.Source = _bitmap;
    }

    // ── 우클릭 커서 복원 ─────────────────────────────────────────────
    private void SimImage_RightMouseUp(object sender, MouseButtonEventArgs e)
        => Mouse.OverrideCursor = null;

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
            if (_grid.GetMaterial(x, y) == Material.Empty)
                _grid.Set(x, y, Material.Water);
        }
    }
}
