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
    private GameEngine? _engine;
    private readonly HighScoreService _scores = new();
    private readonly SoundService     _sound  = new();
    private GameMode   _selectedMode       = GameMode.Classic;
    private Difficulty _selectedDifficulty = Difficulty.Normal;

    private static readonly SolidColorBrush ZenLivesBrush = new(Color.FromRgb(74, 222, 128));

    public MainWindow()
    {
        InitializeComponent();
        _scores.Load();

        // 오디오 설정 복원
        _sound.BgmVolume = _scores.Data.BgmVolume;
        _sound.SfxVolume = _scores.Data.SfxVolume;
        _sound.Muted     = _scores.Data.Muted;

        Loaded      += OnLoaded;
        KeyDown     += OnKeyDown;
        SizeChanged += OnSizeChanged;
        CompositionTarget.Rendering += OnRendering;
        Closed += (_, _) => _sound.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        // 마우스 이벤트
        GameLayer.MouseMove  += OnGameMouseMove;
        GameLayer.MouseDown  += OnGameMouseDown;
        GameLayer.MouseUp    += OnGameMouseUp;

        // 창 크기 복원
        var d = _scores.Data;
        if (d.WindowWidth >= 700)  Width  = d.WindowWidth;
        if (d.WindowHeight >= 520) Height = d.WindowHeight;

        // 마지막 선택 모드·난이도 복원
        var lastDiff = Enum.TryParse<Difficulty>(d.LastDifficulty, out var diff) ? diff : Difficulty.Normal;
        var lastMode = Enum.TryParse<GameMode>(d.LastMode, out var mode)         ? mode : GameMode.Classic;

        SelectDifficulty(lastDiff);
        SelectMode(lastMode);
        UpdateBestScores();
        UpdateMuteIndicator();
        UpdateHelpAudioLabels();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 창 크기 자동 저장 (최소화 제외 — 게임 중에도 저장)
        if (WindowState == WindowState.Normal && IsLoaded)
            _scores.SaveSettings(_selectedMode.ToString(), _selectedDifficulty.ToString(), ActualWidth, ActualHeight);
    }

    // ── 렌더링 루프 ────────────────────────────────────────────────────────────
    private void OnRendering(object? sender, EventArgs e)
    {
        _engine?.OnRender(sender, e);
    }

    // ── 키보드 ────────────────────────────────────────────────────────────────
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // F1 — 도움말 토글
        if (e.Key == Key.F1)
        {
            ToggleHelp();
            return;
        }

        // M — 사운드 음소거 토글
        if (e.Key == Key.M)
        {
            _sound.Muted = !_sound.Muted;
            _scores.SaveAudioSettings(_sound.BgmVolume, _sound.SfxVolume, _sound.Muted);
            UpdateMuteIndicator();
            UpdateHelpAudioLabels();
            return;
        }

        // [ / ] — BGM 볼륨 조절
        if (e.Key == Key.OemOpenBrackets || e.Key == Key.OemCloseBrackets)
        {
            var delta = e.Key == Key.OemCloseBrackets ? 0.1 : -0.1;
            _sound.BgmVolume = Math.Clamp(_sound.BgmVolume + delta, 0, 1);
            _scores.SaveAudioSettings(_sound.BgmVolume, _sound.SfxVolume, _sound.Muted);
            UpdateHelpAudioLabels();
            return;
        }

        // - / + — SFX 볼륨 조절
        if (e.Key == Key.OemMinus || e.Key == Key.Subtract ||
            e.Key == Key.OemPlus  || e.Key == Key.Add)
        {
            var isUp = e.Key == Key.OemPlus || e.Key == Key.Add;
            var delta = isUp ? 0.1 : -0.1;
            _sound.SfxVolume = Math.Clamp(_sound.SfxVolume + delta, 0, 1);
            _scores.SaveAudioSettings(_sound.BgmVolume, _sound.SfxVolume, _sound.Muted);
            UpdateHelpAudioLabels();
            return;
        }

        if (e.Key != Key.Escape) return;

        // ESC — 도움말 닫기 우선
        if (HelpLayer.Visibility == Visibility.Visible)
        {
            HelpLayer.Visibility = Visibility.Collapsed;
            return;
        }

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

    private void ToggleHelp()
    {
        if (HelpLayer.Visibility == Visibility.Visible)
        {
            HelpLayer.Visibility = Visibility.Collapsed;
        }
        else
        {
            // 게임 중이면 일시정지
            if (GameLayer.Visibility == Visibility.Visible &&
                _engine is { IsRunning: true, IsPaused: false })
            {
                _engine.Pause();
                PauseLayer.Visibility = Visibility.Visible;
            }
            HelpLayer.Visibility = Visibility.Visible;
        }
    }

    // ── 마우스 (게임 레이어) ─────────────────────────────────────────────────
    private void OnGameMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(GameCanvas);
        _engine?.OnMouseMove(pos);
    }

    private void OnGameMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_engine is not { IsRunning: true } || _engine.IsPaused) return;
        _engine.OnMouseDown(e.GetPosition(GameCanvas));
    }

    private void OnGameMouseUp(object sender, MouseButtonEventArgs e)
    {
        _engine?.OnMouseUp();
    }

    // ── 난이도 선택 ───────────────────────────────────────────────────────────
    private void BtnDifficulty_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn &&
            Enum.TryParse<Difficulty>(btn.Tag?.ToString(), out var diff))
        {
            SelectDifficulty(diff);
            _scores.SaveSettings(_selectedMode.ToString(), diff.ToString(), ActualWidth, ActualHeight);
            UpdateBestScores();
        }
    }

    private void SelectDifficulty(Difficulty diff)
    {
        _selectedDifficulty = diff;
        BtnEasy.Style   = Application.Current.Resources[diff == Difficulty.Easy   ? "ModeBtnActive" : "ModeBtn"] as System.Windows.Style;
        BtnNormal.Style = Application.Current.Resources[diff == Difficulty.Normal ? "ModeBtnActive" : "ModeBtn"] as System.Windows.Style;
        BtnHard.Style   = Application.Current.Resources[diff == Difficulty.Hard   ? "ModeBtnActive" : "ModeBtn"] as System.Windows.Style;
    }

    // ── 모드 선택 ─────────────────────────────────────────────────────────────
    private void BtnMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn &&
            Enum.TryParse<GameMode>(btn.Tag?.ToString(), out var mode))
        {
            SelectMode(mode);
            _scores.SaveSettings(mode.ToString(), _selectedDifficulty.ToString(), ActualWidth, ActualHeight);
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
            GameMode.Zen        => "30개 도형을 슬라이스하라 — 최대 콤보를 유지하라!",
            _                   => ""
        };
    }

    // ── 게임 시작 ─────────────────────────────────────────────────────────────
    private void BtnStart_Click(object sender, RoutedEventArgs e) => StartGame(_selectedMode);

    private void StartGame(GameMode mode)
    {
        _engine = new GameEngine(GameCanvas);
        _engine.StateChanged += UpdateHud;
        _engine.GameOver     += OnGameOver;
        _engine.PlaySound     = _sound.Play;

        UpdateBestForMode(mode, _selectedDifficulty);
        UpdateLivesDisplay(mode);
        TxtFever.Visibility = Visibility.Collapsed;

        HelpLayer.Visibility     = Visibility.Collapsed;
        MenuLayer.Visibility     = Visibility.Collapsed;
        GameOverLayer.Visibility = Visibility.Collapsed;
        PauseLayer.Visibility    = Visibility.Collapsed;
        GameLayer.Visibility     = Visibility.Visible;

        _sound.StartBgm();
        _engine.StartGame(mode, _selectedDifficulty);
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
                TxtLives.Foreground = ZenLivesBrush;
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

            // 피버 표시
            TxtFever.Visibility = _engine.IsFever ? Visibility.Visible : Visibility.Collapsed;

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

    private void UpdateBestForMode(GameMode mode, Difficulty diff)
    {
        TxtBest.Text = _scores.GetBest(mode, diff).ToString("N0");
    }

    private void UpdateBestScores()
    {
        var diff = _selectedDifficulty;
        TxtBestClassic.Text = _scores.GetBest(GameMode.Classic,    diff).ToString("N0");
        TxtBestTime.Text    = _scores.GetBest(GameMode.TimeAttack, diff).ToString("N0");
        TxtBestZen.Text     = _scores.GetBest(GameMode.Zen,        diff).ToString("N0");

        UpdateTop3Display(GameMode.Classic,    diff, TxtTop2Classic, TxtTop3Classic);
        UpdateTop3Display(GameMode.TimeAttack, diff, TxtTop2Time,    TxtTop3Time);
        UpdateTop3Display(GameMode.Zen,        diff, TxtTop2Zen,     TxtTop3Zen);
    }

    private void UpdateTop3Display(GameMode mode, Difficulty diff,
        System.Windows.Controls.TextBlock txt2, System.Windows.Controls.TextBlock txt3)
    {
        var top3  = _scores.GetTop3(mode, diff);
        var dates = _scores.GetTop3Dates(mode, diff);

        txt2.Text = top3.Count >= 2
            ? $"2  {top3[1]:N0}{(dates.Count >= 2 && !string.IsNullOrEmpty(dates[1]) ? $"  {dates[1]}" : "")}"
            : "";
        txt3.Text = top3.Count >= 3
            ? $"3  {top3[2]:N0}{(dates.Count >= 3 && !string.IsNullOrEmpty(dates[2]) ? $"  {dates[2]}" : "")}"
            : "";
    }

    // ── 뮤트 인디케이터 / 도움말 볼륨 라벨 ────────────────────────────────
    private void UpdateMuteIndicator()
    {
        TxtMuteIndicator.Visibility = _sound.Muted ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateHelpAudioLabels()
    {
        TxtHelpBgmVol.Text = $"{(int)Math.Round(_sound.BgmVolume * 100)}%";
        TxtHelpSfxVol.Text = $"{(int)Math.Round(_sound.SfxVolume * 100)}%";
        TxtHelpMute.Text   = _sound.Muted ? "ON" : "OFF";
        TxtHelpMute.Foreground = _sound.Muted
            ? new SolidColorBrush(Color.FromRgb(255, 107, 0))
            : new SolidColorBrush(Color.FromRgb(106, 112, 128));
    }

    // ── 게임오버 ─────────────────────────────────────────────────────────────
    private void OnGameOver(GameResult result)
    {
        var isNewBest = _scores.TryUpdate(result.Mode, result.Difficulty, result.Score);

        Dispatcher.BeginInvoke(() =>
        {
            TxtGoScore.Text  = result.Score.ToString("N0");
            TxtGoCombo.Text  = $"×{result.MaxCombo}";
            TxtGoSliced.Text = result.Sliced.ToString();
            TxtGoMissed.Text = result.Missed.ToString();
            TxtFever.Visibility = Visibility.Collapsed;

            var total = result.Sliced + result.Missed;
            TxtGoAccuracy.Text = total > 0
                ? $"{result.Sliced * 100.0 / total:F1}%"
                : "—";

            TxtGoMode.Text       = result.Mode.ToString().ToUpper();
            TxtGoDifficulty.Text = result.Difficulty.ToString().ToUpper();

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
        _sound.StopBgm();
        HelpLayer.Visibility     = Visibility.Collapsed;
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

    private void BtnHowToPlay_Click(object sender, RoutedEventArgs e) => ToggleHelp();
    private void BtnHelpClose_Click(object sender, RoutedEventArgs e)  => HelpLayer.Visibility = Visibility.Collapsed;
}
