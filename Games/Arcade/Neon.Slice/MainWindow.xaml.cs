using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NeonSlice.Engine;
using NeonSlice.Models;
using NeonSlice.Services;

namespace NeonSlice;

public partial class MainWindow : Window
{
    // ── DWM 다크 타이틀바 ────────────────────────────────────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ── 게임 관련 ─────────────────────────────────────────────────────────────
    private readonly DrawingVisualHost _drawHost = new();
    private GameEngine? _engine;
    private readonly HighScoreService _scores = new();
    private GameMode _selectedMode = GameMode.Classic;

    public MainWindow()
    {
        InitializeComponent();
        _scores.Load();

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        SizeChanged += OnSizeChanged;
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        // DrawingVisualHost를 GameLayer(Grid)에 직접 추가 — Canvas 크기 0 문제 방지
        // GameLayer의 첫 번째 자식으로 삽입하여 HUD 아래 배경으로 렌더링
        GameLayer.Children.Insert(0, _drawHost);

        // 마우스 이벤트 (슬라이스 중 여부와 무관하게 항상 위치 추적)
        GameLayer.MouseMove  += OnGameMouseMove;
        GameLayer.MouseDown  += OnGameMouseDown;
        GameLayer.MouseUp    += OnGameMouseUp;

        UpdateBestScores();
        SelectMode(GameMode.Classic);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 엔진이 매 프레임 _host.ActualWidth/Height를 직접 읽으므로 별도 Resize 불필요
    }

    // ── 렌더링 루프 ────────────────────────────────────────────────────────────
    private void OnRendering(object? sender, EventArgs e)
    {
        _engine?.OnRender(sender, e);
    }

    // ── 키보드 ────────────────────────────────────────────────────────────────
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        if (GameLayer.Visibility == Visibility.Visible &&
            _engine is { IsRunning: true })
        {
            if (_engine.IsPaused)
            {
                _engine.Resume();
                PauseLayer.Visibility = Visibility.Collapsed;
            }
            else
            {
                _engine.Pause();
                PauseLayer.Visibility = Visibility.Visible;
            }
        }
    }

    // ── 마우스 (게임 레이어) ─────────────────────────────────────────────────
    private void OnGameMouseMove(object sender, MouseEventArgs e)
    {
        // 일시정지/게임오버 중에도 커서 위치는 항상 엔진에 전달 (커서 dot 렌더용)
        var pos = e.GetPosition(_drawHost);
        _engine?.OnMouseMove(pos);
    }

    private void OnGameMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_engine is not { IsRunning: true } || _engine.IsPaused) return;
        _engine.OnMouseDown(e.GetPosition(_drawHost));
    }

    private void OnGameMouseUp(object sender, MouseButtonEventArgs e)
    {
        _engine?.OnMouseUp();
    }

    // ── 모드 선택 ─────────────────────────────────────────────────────────────
    private void BtnMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn &&
            Enum.TryParse<GameMode>(btn.Tag?.ToString(), out var mode))
        {
            SelectMode(mode);
        }
    }

    private void SelectMode(GameMode mode)
    {
        _selectedMode = mode;

        BtnClassic.Style    = Application.Current.Resources[mode == GameMode.Classic    ? "ModeBtnActive" : "ModeBtn"] as System.Windows.Style;
        BtnTimeAttack.Style = Application.Current.Resources[mode == GameMode.TimeAttack ? "ModeBtnActive" : "ModeBtn"] as System.Windows.Style;
        BtnZen.Style        = Application.Current.Resources[mode == GameMode.Zen        ? "ModeBtnActive" : "ModeBtn"] as System.Windows.Style;

        TxtModeDesc.Text = mode switch
        {
            GameMode.Classic    => "목숨 3개 — 도형을 놓칠 때마다 감소, 폭탄 주의!",
            GameMode.TimeAttack => "60초 제한 — 제한 시간 내 최대 점수 획득!",
            GameMode.Zen        => "30개 슬라이스 — 최대 콤보를 유지하라!",
            _                   => ""
        };
    }

    // ── 게임 시작 ─────────────────────────────────────────────────────────────
    private void BtnStart_Click(object sender, RoutedEventArgs e) => StartGame(_selectedMode);

    private void StartGame(GameMode mode)
    {
        _engine = new GameEngine(_drawHost);
        _engine.StateChanged += UpdateHud;
        _engine.GameOver     += OnGameOver;

        UpdateBestForMode(mode);
        UpdateLivesDisplay(mode);

        MenuLayer.Visibility     = Visibility.Collapsed;
        GameOverLayer.Visibility = Visibility.Collapsed;
        PauseLayer.Visibility    = Visibility.Collapsed;
        GameLayer.Visibility     = Visibility.Visible;

        // 크기 동기화는 OnRender에서 매 프레임 처리하므로 UpdateLayout/Resize 불필요
        _engine.StartGame(mode);
    }

    private void UpdateLivesDisplay(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Classic:
                TxtLivesLabel.Text = "LIVES";
                TxtLives.Text = "♥♥♥";
                TxtLives.Foreground = new SolidColorBrush(Color.FromRgb(255, 45, 120));
                PanelLives.Visibility = Visibility.Visible;
                break;
            case GameMode.TimeAttack:
                TxtLivesLabel.Text = "TIME";
                TxtLives.Text = "60";
                TxtLives.Foreground = new SolidColorBrush(Color.FromRgb(255, 230, 0));
                PanelLives.Visibility = Visibility.Visible;
                break;
            case GameMode.Zen:
                TxtLivesLabel.Text = "LEFT";
                TxtLives.Text = "30";
                TxtLives.Foreground = new SolidColorBrush(Color.FromRgb(57, 255, 20));
                PanelLives.Visibility = Visibility.Visible;
                break;
        }
    }

    // ── HUD 갱신 ─────────────────────────────────────────────────────────────
    private void UpdateHud()
    {
        if (_engine is null) return;
        Dispatcher.BeginInvoke(() =>
        {
            TxtScore.Text  = _engine.Score.ToString("N0");
            TxtCombo.Text  = $"×{_engine.Combo}";
            TxtSliced.Text = _engine.Sliced.ToString();

            switch (_engine.Mode)
            {
                case GameMode.Classic:
                    TxtLives.Text = new string('♥', Math.Max(0, _engine.Lives))
                                  + new string('♡', Math.Max(0, 3 - _engine.Lives));
                    break;
                case GameMode.TimeAttack:
                    TxtLives.Text = ((int)Math.Ceiling(_engine.TimeLeft)).ToString();
                    break;
                case GameMode.Zen:
                    TxtLives.Text = _engine.ZenSlicesLeft.ToString();
                    break;
            }
        });
    }

    private void UpdateBestForMode(GameMode mode)
    {
        TxtBest.Text = _scores.GetBest(mode).ToString("N0");
    }

    private void UpdateBestScores()
    {
        TxtBestClassic.Text = _scores.GetBest(GameMode.Classic).ToString("N0");
        TxtBestTime.Text    = _scores.GetBest(GameMode.TimeAttack).ToString("N0");
        TxtBestZen.Text     = _scores.GetBest(GameMode.Zen).ToString("N0");
    }

    // ── 게임오버 ─────────────────────────────────────────────────────────────
    private void OnGameOver(GameResult result)
    {
        var isNewBest = _scores.TryUpdate(result.Mode, result.Score);

        Dispatcher.BeginInvoke(() =>
        {
            TxtGoScore.Text  = result.Score.ToString("N0");
            TxtGoCombo.Text  = $"×{result.MaxCombo}";
            TxtGoSliced.Text = result.Sliced.ToString();
            TxtGoMissed.Text = result.Missed.ToString();
            TxtGoNewBest.Visibility = isNewBest ? Visibility.Visible : Visibility.Collapsed;
            TxtGameOverTitle.Text = result.Mode == GameMode.Zen ? "ZEN COMPLETE" : "GAME OVER";

            GameOverLayer.Visibility = Visibility.Visible;
            UpdateBestScores();
        });
    }

    // ── 버튼 이벤트 ───────────────────────────────────────────────────────────
    private void BtnPlayAgain_Click(object sender, RoutedEventArgs e)
    {
        GameOverLayer.Visibility = Visibility.Collapsed;
        StartGame(_engine?.Mode ?? _selectedMode);
    }

    private void BtnMenu_Click(object sender, RoutedEventArgs e)
    {
        _engine?.Pause();
        PauseLayer.Visibility    = Visibility.Collapsed;
        GameOverLayer.Visibility = Visibility.Collapsed;
        GameLayer.Visibility     = Visibility.Collapsed;
        MenuLayer.Visibility     = Visibility.Visible;
        UpdateBestScores();
    }

    private void BtnResume_Click(object sender, RoutedEventArgs e)
    {
        _engine?.Resume();
        PauseLayer.Visibility = Visibility.Collapsed;
    }
}
