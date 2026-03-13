using System.Runtime.InteropServices;
using System.Windows.Interop;
using GolfCast.Models;
using GolfCast.Services;

namespace GolfCast.Views;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int val, int sz);

    private readonly GameService _game = new();
    private bool  _dragging;
    private Point _dragStart;

    public MainWindow()
    {
        InitializeComponent();
        Canvas.GameService = _game;

        _game.Updated         += () => Canvas.InvalidateVisual();
        _game.HoleCompleted   += OnHoleCompleted;
        _game.CourseCompleted += OnCourseCompleted;
        _game.BallInWater     += OnBallInWater;

        Loaded  += (_, _) => { ApplyDarkTitleBar(); ShowMenu(); };
        Closing += (_, _) => _game.Stop();
    }

    // ── 마우스 이벤트 ─────────────────────────────────────────────────────────

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_game.AimMode) return;
        var pos  = e.GetPosition(Canvas);
        var ball = _game.Ball.Pos;
        var dir  = new Vec2(pos.X - ball.X, pos.Y - ball.Y);
        if (dir.Length < 1) return;

        if (_dragging)
        {
            var drag  = e.GetPosition(Canvas);
            var delta = new Vec2(_dragStart.X - drag.X, _dragStart.Y - drag.Y);
            _game.AimDir   = delta.Normalized;
            _game.AimPower = Math.Clamp(delta.Length / 160.0, 0, 1);
        }
        else
        {
            _game.AimDir   = (-dir).Normalized;
            _game.AimPower = 0.5;
        }
        Canvas.InvalidateVisual();
        UpdateHud();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_game.AimMode) return;
        _dragging  = true;
        _dragStart = e.GetPosition(Canvas);
        Canvas.CaptureMouse();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        Canvas.ReleaseMouseCapture();
        _dragging = false;

        if (_game.AimDir.Length > 0.01 && _game.AimPower > 0.02)
            _game.Fire(_game.AimDir, _game.AimPower);
    }

    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        // 우클릭: 조준 취소
        _dragging      = false;
        _game.AimDir   = Vec2.Zero;
        _game.AimPower = 0;
        Canvas.ReleaseMouseCapture();
        Canvas.InvalidateVisual();
    }

    // ── 게임 이벤트 ───────────────────────────────────────────────────────────

    private void OnHoleCompleted()
    {
        UpdateHud();
        var h = _game.ScoreCard.Scores.LastOrDefault();
        var par = _game.CurrentHole?.Par ?? 3;
        var msg = h switch
        {
            1       => "⭐ 홀인원!",
            _ when h < par => "🎉 버디!",
            _ when h == par => "✅ 파",
            _ when h == par + 1 => "보기",
            _ => $"+{h - par} 오버",
        };
        TxtStatus.Text = msg;
    }

    private void OnCourseCompleted()
    {
        Dispatcher.Invoke(() =>
        {
            var sc   = new ScoreCardWindow(_game.ScoreCard) { Owner = this };
            sc.ShowDialog();
            if (sc.Replay) ShowMenu();
        });
    }

    private void OnBallInWater()
    {
        Dispatcher.Invoke(() =>
        {
            TxtStatus.Text = "⚠ 워터 해저드! 티에서 +1 벌타 재시작";
            _game.RetryFromWater();
        });
    }

    // ── HUD 업데이트 ─────────────────────────────────────────────────────────

    private void UpdateHud()
    {
        var hole = _game.CurrentHole;
        if (hole is null) return;
        TxtHole.Text     = $"홀 {hole.Number} / {_game.ScoreCard.Course.Holes.Count}";
        TxtHoleName.Text = hole.Name;
        TxtPar.Text      = $"파 {hole.Par}";
        TxtStrokes.Text  = $"{_game.Ball.Strokes}타";
        TxtTotalScore.Text = _game.ScoreCard.Scores.Count > 0
            ? $"누계: {_game.ScoreCard.Total}타"
            : "";
    }

    // ── 메뉴 ─────────────────────────────────────────────────────────────────

    private void OnMenu(object sender, RoutedEventArgs e) => ShowMenu();

    private void ShowMenu()
    {
        var dlg = new CourseSelectWindow { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedCourse is not null)
        {
            _game.StartCourse(dlg.SelectedCourse);
            UpdateHud();
            TxtStatus.Text = "마우스 드래그로 조준, 버튼 놓으면 발사!";
        }
        else if (!_game.IsFinished && _game.CurrentHole is null)
            Close();
    }

    private void ApplyDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));
    }
}
