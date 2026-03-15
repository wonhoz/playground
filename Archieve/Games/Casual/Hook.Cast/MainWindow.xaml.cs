using System.Windows.Media;
using Point = System.Windows.Point;

namespace HookCast;

public partial class MainWindow : Window
{
    private readonly GameEngine       _engine = new();
    private readonly DispatcherTimer  _timer  = new() { Interval = TimeSpan.FromMilliseconds(16) };

    // 그래픽 오브젝트 풀
    private readonly List<Line>    _ropeLines  = [];
    private readonly List<Ellipse> _fishShapes = [];
    private readonly List<Ellipse> _bubbles    = [];

    // 릴링 키 상태
    private bool _reeling;

    // 도감 창
    private CatchLogWindow? _logWin;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        _timer.Tick += OnTick;

        _engine.Bite       += OnBite;
        _engine.FishCaught += OnFishCaught;
        _engine.FishEscaped += () => BiteIndicator.Visibility = Visibility.Collapsed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _engine.Init(GameCanvas.ActualWidth, GameCanvas.ActualHeight);
        ResizeCanvasElements();
        _timer.Start();
    }

    private void ResizeCanvasElements()
    {
        var w = GameCanvas.ActualWidth;
        var h = GameCanvas.ActualHeight;
        if (w < 10 || h < 10) return;

        SkyRect.Width  = w;
        SkyRect.Height = _engine.WaterY;

        WaterRect.Width  = w;
        WaterRect.Height = h - _engine.WaterY;
        Canvas.SetTop(WaterRect, _engine.WaterY);

        WaterLine.X1 = 0; WaterLine.X2 = w;
        WaterLine.Y1 = _engine.WaterY; WaterLine.Y2 = _engine.WaterY;

        Canvas.SetLeft(TxtHint, 10);
        Canvas.SetTop(TxtHint, h - 24);
    }

    // ── 게임 루프 ────────────────────────────────────────────────
    private void OnTick(object? s, EventArgs e)
    {
        if (_reeling) _engine.Reel(0.016);
        _engine.Update();
        Render();
    }

    private void Render()
    {
        var nodes = _engine.Physics.Nodes;
        var fish  = _engine.Fish;

        // 낚싯줄 그리기
        EnsureLines(_ropeLines, PhysicsEngine.NodeCount - 1, GameCanvas,
                    stroke: (Brush)FindResource("AccentBrush"), thickness: 1.5);
        for (int i = 0; i < PhysicsEngine.NodeCount - 1; i++)
        {
            _ropeLines[i].X1 = nodes[i].Pos.X;
            _ropeLines[i].Y1 = nodes[i].Pos.Y;
            _ropeLines[i].X2 = nodes[i + 1].Pos.X;
            _ropeLines[i].Y2 = nodes[i + 1].Pos.Y;
        }

        // 후크 점
        DrawHook(nodes[^1].Pos);

        // 낚싯대 (대각선 라인, 화면 왼쪽)
        RodLine.X1 = 20;  RodLine.Y1 = _engine.WaterY - 40;
        RodLine.X2 = nodes[0].Pos.X; RodLine.Y2 = nodes[0].Pos.Y;

        // 조준 화살표
        if (_engine.IsDragging)
        {
            AimArrow.Visibility = Visibility.Visible;
            AimArrow.X1 = _engine.DragStart.X;   AimArrow.Y1 = _engine.DragStart.Y;
            AimArrow.X2 = _engine.DragCurrent.X; AimArrow.Y2 = _engine.DragCurrent.Y;
        }
        else AimArrow.Visibility = Visibility.Collapsed;

        // 물고기 그리기
        EnsureEllipses(_fishShapes, fish.Count, GameCanvas);
        for (int i = 0; i < fish.Count; i++)
        {
            var f = fish[i];
            double r = Math.Clamp(f.Size * 0.22, 6, 16);
            _fishShapes[i].Width  = r * 2;
            _fishShapes[i].Height = r;
            _fishShapes[i].Fill   = GetFishColor(f);
            _fishShapes[i].Opacity = f.Pos.Y > _engine.WaterY ? 0.75 : 0;
            Canvas.SetLeft(_fishShapes[i], f.Pos.X - r);
            Canvas.SetTop(_fishShapes[i],  f.Pos.Y - r / 2);
        }
        // 남는 물고기 셰이프 숨기기
        for (int i = fish.Count; i < _fishShapes.Count; i++)
            _fishShapes[i].Opacity = 0;

        // 릴링 진행바
        if (_engine.Phase == GamePhase.FightReel)
        {
            ReelPanel.Visibility  = Visibility.Visible;
            BiteIndicator.Visibility = Visibility.Collapsed;
            ReelBar.Width = Math.Max(0, Math.Min(200, _engine.ReelProgress * 200));
            Canvas.SetLeft(ReelPanel,  GameCanvas.ActualWidth / 2 - 120);
            Canvas.SetTop(ReelPanel,   _engine.WaterY - 80);
        }
        else if (_engine.Phase != GamePhase.FightReel)
            ReelPanel.Visibility = Visibility.Collapsed;

        // 힌트 텍스트 업데이트
        TxtHint.Text = _engine.Phase switch
        {
            GamePhase.Aiming    => "🎣 드래그로 낚싯줄을 던지세요",
            GamePhase.Flying    => "낚싯줄 날아가는 중...",
            GamePhase.Waiting   => "물고기를 기다리는 중... (입질 시 Space/클릭)",
            GamePhase.FightReel => "🐟 Space 홀드로 릴링!",
            GamePhase.Result    => "잠시 후 자동으로 재시작...",
            _ => ""
        };
    }

    // ── 이벤트 ───────────────────────────────────────────────────
    private void OnBite()
    {
        Dispatcher.Invoke(() =>
        {
            BiteIndicator.Visibility = Visibility.Visible;
            Canvas.SetLeft(BiteIndicator, GameCanvas.ActualWidth / 2 - 180);
            Canvas.SetTop(BiteIndicator,  _engine.WaterY - 50);
        });
    }

    private void OnFishCaught(CatchRecord rec)
    {
        Dispatcher.Invoke(() =>
        {
            BiteIndicator.Visibility = Visibility.Collapsed;
            ReelPanel.Visibility     = Visibility.Collapsed;

            TxtResult.Text      = $"🎉 낚시 성공!";
            TxtCatchDetail.Text = $"{rec.KorName}  {rec.Size:F1}cm  {rec.Weight:F2}kg";
            ResultPanel.Visibility = Visibility.Visible;
            Canvas.SetLeft(ResultPanel, GameCanvas.ActualWidth / 2 - 150);
            Canvas.SetTop(ResultPanel,  GameCanvas.ActualHeight / 2 - 60);

            TxtCatchCount.Text = $"{_engine.CatchLog.Count}마리";
            TxtLastCatch.Text  = $"| 최근: {rec.KorName} {rec.Size:F1}cm";

            _logWin?.RefreshLog(_engine.CatchLog);
        });
    }

    // ── 입력 처리 ────────────────────────────────────────────────
    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_engine.Phase == GamePhase.Aiming)
        {
            var p = e.GetPosition(GameCanvas);
            _engine.BeginDrag(new Vec2(p.X, p.Y));
            GameCanvas.CaptureMouse();
        }
        else if (_engine.Phase == GamePhase.Waiting)
        {
            _engine.TrySetHook();
            BiteIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(GameCanvas);
        _engine.UpdateDrag(new Vec2(p.X, p.Y));
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        GameCanvas.ReleaseMouseCapture();
        if (_engine.Phase == GamePhase.Aiming)
        {
            var p = e.GetPosition(GameCanvas);
            _engine.EndDrag(new Vec2(p.X, p.Y));
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            if (_engine.Phase == GamePhase.Waiting)
            {
                _engine.TrySetHook();
                BiteIndicator.Visibility = Visibility.Collapsed;
            }
            else if (_engine.Phase == GamePhase.FightReel)
            {
                _reeling = true;
            }
        }
        if (e.Key == Key.R) _engine.Reset();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space) _reeling = false;
    }

    // ── 버튼 ─────────────────────────────────────────────────────
    private void CatchLogBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_logWin == null || !_logWin.IsVisible)
        {
            _logWin = new CatchLogWindow(_engine.CatchLog);
            _logWin.Show();
        }
        else _logWin.Activate();
    }

    private void RestartBtn_Click(object sender, RoutedEventArgs e)
    {
        _engine.Init(GameCanvas.ActualWidth, GameCanvas.ActualHeight);
        ResultPanel.Visibility   = Visibility.Collapsed;
        BiteIndicator.Visibility = Visibility.Collapsed;
        ReelPanel.Visibility     = Visibility.Collapsed;
        TxtCatchCount.Text       = "0마리";
        TxtLastCatch.Text        = "";
        UpdateWeatherLabel();
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    // ── 헬퍼 ─────────────────────────────────────────────────────
    private void UpdateWeatherLabel()
    {
        TxtWeather.Text = _engine.Weather switch
        {
            Weather.Sunny  => "  ☀️ 맑음",
            Weather.Cloudy => "  ⛅ 흐림",
            Weather.Rainy  => "  🌧️ 비",
            _ => ""
        };
    }

    private static Brush GetFishColor(Fish f) => f.Species switch
    {
        FishSpecies.Crucian   => new SolidColorBrush(Color.FromRgb(200, 160, 80)),
        FishSpecies.Bass      => new SolidColorBrush(Color.FromRgb(60, 200, 120)),
        FishSpecies.Trout     => new SolidColorBrush(Color.FromRgb(100, 180, 255)),
        FishSpecies.Salmon    => new SolidColorBrush(Color.FromRgb(255, 120, 80)),
        FishSpecies.Snakehead => new SolidColorBrush(Color.FromRgb(180, 80, 200)),
        _ => Brushes.Gray
    };

    private void DrawHook(Vec2 pos)
    {
        // 후크 표시 (작은 원)
        var existing = GameCanvas.Children.OfType<Ellipse>()
                                 .FirstOrDefault(el => el.Tag as string == "hook");
        if (existing == null)
        {
            existing = new Ellipse { Width = 8, Height = 8, Fill = Brushes.White, Tag = "hook" };
            GameCanvas.Children.Add(existing);
        }
        Canvas.SetLeft(existing, pos.X - 4);
        Canvas.SetTop(existing,  pos.Y - 4);
    }

    private static void EnsureLines(List<Line> lines, int count, Canvas canvas, Brush stroke, double thickness)
    {
        while (lines.Count < count)
        {
            var l = new Line { Stroke = stroke, StrokeThickness = thickness,
                              StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            canvas.Children.Add(l);
            lines.Add(l);
        }
    }

    private static void EnsureEllipses(List<Ellipse> ellipses, int count, Canvas canvas)
    {
        while (ellipses.Count < count)
        {
            var el = new Ellipse();
            canvas.Children.Add(el);
            ellipses.Add(el);
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        if (IsLoaded) ResizeCanvasElements();
    }
}
