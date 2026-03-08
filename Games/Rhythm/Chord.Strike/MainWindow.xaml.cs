using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ChordStrike.Models;
using ChordStrike.Services;

namespace ChordStrike;

public partial class MainWindow : Window
{
    // ── 엔진 / 타이머 ───────────────────────────────────────────────
    private readonly GameEngine     _engine = new();
    private readonly DispatcherTimer _timer  = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private DateTime                _lastTick;

    // ── 채보 목록 ────────────────────────────────────────────────────
    private List<Chart> _charts = [];
    private Chart?      _selectedChart;

    // ── 레인 시각 요소 풀 ────────────────────────────────────────────
    private readonly List<Rectangle> _notePool     = [];
    private readonly List<TextBlock> _judgePool    = [];

    // 레인 구분선 + 키 레이블
    private readonly List<Line>      _laneLines    = [];
    private readonly List<Rectangle> _laneBgs      = [];
    private readonly List<Border>    _keyLabels    = [];
    private Rectangle?              _hitLineRect;

    // 판정 애니메이션 (레인별 마지막 판정 표시)
    private readonly TextBlock[] _laneJudge = new TextBlock[Lanes.Count];
    private readonly double[]    _judgeAlpha = new double[Lanes.Count];

    // 레인 폭 계산
    private double LaneW  => LaneCanvas.ActualWidth  / Lanes.Count;
    private double LaneH  => LaneCanvas.ActualHeight;

    // 브러시 캐시
    private static readonly Brush[] LaneBrushes = BuildLaneBrushes();
    private static readonly Brush   PerfectBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x40));
    private static readonly Brush   GoodBrush    = new SolidColorBrush(Color.FromRgb(0x40, 0xFF, 0x80));
    private static readonly Brush   MissBrush    = new SolidColorBrush(Color.FromRgb(0xFF, 0x40, 0x40));
    private static readonly Brush   HitLineBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x2A, 0x2A, 0x60));

    private static Brush[] BuildLaneBrushes()
    {
        Color[] colors =
        [
            Color.FromRgb(0x30, 0x60, 0xC0), Color.FromRgb(0x28, 0x50, 0xA0),
            Color.FromRgb(0x30, 0x60, 0xC0), Color.FromRgb(0x28, 0x50, 0xA0),
            Color.FromRgb(0x80, 0x40, 0xC0), Color.FromRgb(0x60, 0x30, 0xA0),
            Color.FromRgb(0x80, 0x40, 0xC0), Color.FromRgb(0x60, 0x30, 0xA0),
        ];
        return colors.Select(c => (Brush)new SolidColorBrush(c)).ToArray();
    }

    // ── 생성자 ───────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();

        _charts = ChartBuilder.BuiltInCharts();
        ChartList.ItemsSource = _charts.Select(c => $"{c.Title}  [{c.Artist}  {c.BPM} BPM]").ToList();
        ChartList.SelectedIndex = 0;
        _selectedChart = _charts[0];

        _engine.NoteJudged    += OnNoteJudged;
        _engine.ChartFinished += OnChartFinished;

        _timer.Tick += GameTick;
        Loaded       += (_, _) => InitLanes();
        SizeChanged  += (_, _) => RebuildLanes();
    }

    // ── 레인 초기화 ──────────────────────────────────────────────────
    private void InitLanes()
    {
        // 판정 텍스트 블록 (레인별)
        for (int i = 0; i < Lanes.Count; i++)
        {
            var tb = new TextBlock
            {
                FontSize   = 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity    = 0,
            };
            Canvas.SetZIndex(tb, 5);
            LaneCanvas.Children.Add(tb);
            _laneJudge[i] = tb;
        }

        RebuildLanes();
    }

    private void RebuildLanes()
    {
        if (!IsLoaded) return;

        // 기존 레인 요소 제거
        foreach (var l in _laneLines)  LaneCanvas.Children.Remove(l);
        foreach (var bg in _laneBgs)   LaneCanvas.Children.Remove(bg);
        foreach (var b in _keyLabels)  LaneCanvas.Children.Remove(b);
        if (_hitLineRect != null) LaneCanvas.Children.Remove(_hitLineRect);
        _laneLines.Clear();
        _laneBgs.Clear();
        _keyLabels.Clear();

        double lw = LaneW;
        double lh = LaneH;
        double hitY = lh * (GameEngine.HitY / GameEngine.LaneHeight);

        // 레인 배경 + 구분선
        for (int i = 0; i < Lanes.Count; i++)
        {
            // 레인 배경 (짝/홀 구분)
            var bg = new Rectangle
            {
                Width  = lw - 1,
                Height = lh,
                Fill   = new SolidColorBrush(Color.FromArgb(0x18, 0x10, 0x10, 0x30)),
            };
            Canvas.SetLeft(bg, i * lw);
            Canvas.SetTop(bg, 0);
            Canvas.SetZIndex(bg, 0);
            LaneCanvas.Children.Insert(0, bg);
            _laneBgs.Add(bg);

            // 수직 구분선
            var line = new Line
            {
                X1 = i * lw, Y1 = 0, X2 = i * lw, Y2 = lh,
                Stroke          = new SolidColorBrush(Color.FromArgb(0x40, 0x1A, 0x1A, 0x40)),
                StrokeThickness = 1,
            };
            Canvas.SetZIndex(line, 1);
            LaneCanvas.Children.Add(line);
            _laneLines.Add(line);

            // 키 레이블 버튼 (히트라인 위)
            var label = new Border
            {
                Width        = lw - 4,
                Height       = 28,
                Background   = new SolidColorBrush(Color.FromArgb(0xCC, 0x0A, 0x0A, 0x1E)),
                BorderBrush  = LaneBrushes[i],
                BorderThickness = new Thickness(0, 2, 0, 0),
                CornerRadius = new CornerRadius(3),
                Child        = new TextBlock
                {
                    Text                = Lanes.Labels[i],
                    Foreground          = LaneBrushes[i],
                    FontSize            = 13,
                    FontWeight          = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                },
            };
            Canvas.SetLeft(label, i * lw + 2);
            Canvas.SetTop(label, hitY - 30);
            Canvas.SetZIndex(label, 3);
            LaneCanvas.Children.Add(label);
            _keyLabels.Add(label);
        }

        // 히트라인
        var hitLine = new Rectangle
        {
            Width  = LaneCanvas.ActualWidth,
            Height = 3,
            Fill   = HitLineBrush,
        };
        Canvas.SetLeft(hitLine, 0);
        Canvas.SetTop(hitLine, hitY - 2);
        Canvas.SetZIndex(hitLine, 2);
        LaneCanvas.Children.Add(hitLine);
        _hitLineRect = hitLine;

        // 판정 텍스트 위치 재배치
        for (int i = 0; i < Lanes.Count; i++)
        {
            if (_laneJudge[i] == null) continue;
            Canvas.SetLeft(_laneJudge[i], i * lw + 2);
            Canvas.SetTop(_laneJudge[i],  hitY - 60);
            _laneJudge[i].Width = lw - 4;
            _laneJudge[i].TextAlignment = TextAlignment.Center;
        }
    }

    // ── 게임 틱 ──────────────────────────────────────────────────────
    private void GameTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        double dt = (now - _lastTick).TotalSeconds;
        _lastTick = now;
        if (dt > 0.1) dt = 0.1; // 스파이크 방지

        _engine.Update(dt);
        RenderFrame(dt);
    }

    // ── 렌더링 ───────────────────────────────────────────────────────
    private void RenderFrame(double dt)
    {
        double lw  = LaneW;
        double lh  = LaneH;
        double scaleY = lh / GameEngine.LaneHeight;

        // 노트 렌더
        var noteIdx = 0;
        foreach (var note in _engine.Active)
        {
            if (note.Judged && note.Y > GameEngine.HitY + 10) continue;

            var rect = GetNoteRect(noteIdx++);
            double x  = note.Lane * lw + 2;
            double y  = note.Y * scaleY - 14;
            double w  = lw - 4;
            double h  = note.Type == NoteType.Hold
                        ? note.Duration * _engine.BeatTimeSec * GameEngine.SpeedPx * scaleY
                        : 14;

            rect.Width  = w;
            rect.Height = Math.Max(h, 8);
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y - rect.Height + 14);

            // 레인 색상 + 판정 플래시
            var brush = LaneBrushes[note.Lane];
            if (note.Judged)
            {
                brush = note.LastJudge == Judgment.Perfect ? PerfectBrush
                      : note.LastJudge == Judgment.Good    ? GoodBrush
                                                           : MissBrush;
            }
            rect.Fill    = brush;
            rect.Opacity = note.Judged ? 0.5 : 1.0;
            rect.Visibility = Visibility.Visible;
        }
        // 남은 노트 rect 숨기기
        for (int i = noteIdx; i < _notePool.Count; i++)
            _notePool[i].Visibility = Visibility.Collapsed;

        // 레인 활성화 글로우
        for (int i = 0; i < Lanes.Count; i++)
        {
            if (i >= _keyLabels.Count) break;
            var border = _keyLabels[i];
            border.Background = _engine.LaneActive[i]
                ? new SolidColorBrush(Color.FromArgb(0xFF, 0x14, 0x14, 0x40))
                : new SolidColorBrush(Color.FromArgb(0xCC, 0x0A, 0x0A, 0x1E));
        }

        // 판정 텍스트 페이드
        for (int i = 0; i < Lanes.Count; i++)
        {
            if (_judgeAlpha[i] > 0)
            {
                _judgeAlpha[i] = Math.Max(0, _judgeAlpha[i] - dt * 2.5);
                _laneJudge[i].Opacity = _judgeAlpha[i];
            }
        }

        // HUD 업데이트
        TxtCombo.Text  = _engine.Score.Combo.ToString();
        TxtScore.Text  = _engine.Score.TotalScore.ToString("N0");

        double prog = _engine.Progress;
        ProgressBar.Width = prog * 200;
    }

    private Rectangle GetNoteRect(int idx)
    {
        while (_notePool.Count <= idx)
        {
            var r = new Rectangle
            {
                RadiusX = 3, RadiusY = 3,
                Visibility = Visibility.Collapsed,
            };
            Canvas.SetZIndex(r, 4);
            LaneCanvas.Children.Add(r);
            _notePool.Add(r);
        }
        return _notePool[idx];
    }

    // ── 판정 이벤트 ──────────────────────────────────────────────────
    private void OnNoteJudged(Note note, Judgment j)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var tb = _laneJudge[note.Lane];
            tb.Text       = j == Judgment.Perfect ? "PERFECT" : j == Judgment.Good ? "GOOD" : "MISS";
            tb.Foreground = j == Judgment.Perfect ? PerfectBrush : j == Judgment.Good ? GoodBrush : MissBrush;
            _judgeAlpha[note.Lane] = 1.0;
            tb.Opacity = 1.0;
        });
    }

    private void OnChartFinished()
    {
        Dispatcher.InvokeAsync(() =>
        {
            _timer.Stop();
            ShowResult();
        });
    }

    private void ShowResult()
    {
        var s = _engine.Score;
        TxtGrade.Text    = s.Grade;
        TxtAccuracy.Text = $"{s.Acc:F1}%";
        TxtPerfect.Text  = s.Perfect.ToString();
        TxtGood.Text     = s.Good.ToString();
        TxtMiss.Text     = s.Miss.ToString();
        TxtMaxCombo.Text = $"Max Combo: {s.MaxCombo}";

        TxtGrade.Foreground = s.Grade switch
        {
            "S" => PerfectBrush,
            "A" => GoodBrush,
            _   => new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xFF)),
        };

        ResultPanel.Visibility = Visibility.Visible;
    }

    // ── UI 이벤트 ────────────────────────────────────────────────────
    private void ChartList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        int idx = ChartList.SelectedIndex;
        if (idx >= 0 && idx < _charts.Count)
            _selectedChart = _charts[idx];
    }

    private void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedChart == null) return;

        MenuPanel.Visibility   = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Collapsed;

        // 노트 풀 초기화
        foreach (var r in _notePool) r.Visibility = Visibility.Collapsed;
        for (int i = 0; i < Lanes.Count; i++)
        {
            _judgeAlpha[i]    = 0;
            _laneJudge[i].Opacity = 0;
        }

        TxtSongInfo.Text = $" — {_selectedChart.Title}  {_selectedChart.BPM} BPM";

        _engine.StartChart(_selectedChart);
        _lastTick = DateTime.UtcNow;
        _timer.Start();
    }

    private void RetryBtn_Click(object sender, RoutedEventArgs e)
    {
        ResultPanel.Visibility = Visibility.Collapsed;
        if (_selectedChart != null) StartBtn_Click(sender, e);
    }

    private void MenuBtn_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        ResultPanel.Visibility = Visibility.Collapsed;
        MenuPanel.Visibility   = Visibility.Visible;
        TxtSongInfo.Text       = "";
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.IsRepeat) return;
        int lane = Array.IndexOf(Lanes.KeyBindings, e.Key);
        if (lane >= 0) _engine.KeyDown(lane);
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        int lane = Array.IndexOf(Lanes.KeyBindings, e.Key);
        if (lane >= 0) _engine.KeyUp(lane);
    }

    private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
        => Close();
}
