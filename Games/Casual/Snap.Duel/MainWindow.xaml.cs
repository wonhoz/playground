using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SnapDuel;

// ─── 게임 상태 ───────────────────────────────────────────────────────────
enum GameState { Idle, Waiting, Ready, Go, Trap, RoundEnd, GameOver }

// ─── 신호 유형 ───────────────────────────────────────────────────────────
enum SignalType { Simple, ColorCondition, ShapeCondition }

// ─── 라운드 결과 ─────────────────────────────────────────────────────────
record RoundResult(int Winner, double P1Ms, double P2Ms, bool P1Trap, bool P2Trap);

public partial class MainWindow : Window
{
    // Win32 다크 타이틀바
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    // ─── 게임 설정 ────────────────────────────────────────────────────────
    int _totalRounds = 5;
    bool _isTournamentMode = false;
    SignalType _signalType = SignalType.Simple;
    bool _trapEnabled = true;

    // ─── 게임 상태 ────────────────────────────────────────────────────────
    GameState _state = GameState.Idle;
    int _currentRound = 0;
    int _p1RoundWins = 0;
    int _p2RoundWins = 0;
    int _p1TotalWins = 0;
    int _p2TotalWins = 0;

    // ─── 라운드 내 반응 ───────────────────────────────────────────────────
    bool _p1Reacted = false;
    bool _p2Reacted = false;
    double _p1Ms = 0;
    double _p2Ms = 0;
    bool _p1Trap = false;
    bool _p2Trap = false;
    string _currentCondition = "";

    // ─── 타이머 / Stopwatch ───────────────────────────────────────────────
    readonly Stopwatch _sw = new();
    DispatcherTimer? _waitTimer;
    readonly Random _rng = new();

    // ─── 색상 신호 조건 ───────────────────────────────────────────────────
    readonly (string Name, Color Color)[] _colors =
    [
        ("빨강", Colors.Red),
        ("초록", Color.FromRgb(0x66, 0xBB, 0x6A)),
        ("파랑", Color.FromRgb(0x42, 0xA5, 0xF5)),
        ("노랑", Colors.Yellow),
    ];

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        try
        {
            var sri = Application.GetResourceStream(new Uri("Resources/app.ico", UriKind.Relative));
            if (sri?.Stream != null)
                Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                    sri.Stream,
                    System.Windows.Media.Imaging.BitmapCreateOptions.None,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        }
        catch { }

        UpdateUI();
    }

    // ─── 키보드 입력 ──────────────────────────────────────────────────────
    void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Q: React(1); break;
            case Key.P: React(2); break;
            case Key.Space: StartOrNext(); break;
            case Key.Escape: AbortGame(); break;
        }
    }

    void BtnP1_Click(object sender, RoutedEventArgs e) => React(1);
    void BtnP2_Click(object sender, RoutedEventArgs e) => React(2);
    void BtnStart_Click(object sender, RoutedEventArgs e) => StartOrNext();

    // ─── 게임 흐름 ────────────────────────────────────────────────────────
    void StartOrNext()
    {
        if (_state == GameState.Idle || _state == GameState.GameOver)
        {
            ResetGame();
            StartRound();
        }
        else if (_state == GameState.RoundEnd)
        {
            _currentRound++;
            if (_currentRound > _totalRounds || IsGameOver())
                EndGame();
            else
                StartRound();
        }
    }

    void ResetGame()
    {
        _currentRound = 1;
        _p1RoundWins = 0;
        _p2RoundWins = 0;
        _p1Ms = 0;
        _p2Ms = 0;
    }

    bool IsGameOver()
    {
        if (_isTournamentMode)
        {
            int needed = _totalRounds / 2 + 1;
            return _p1RoundWins >= needed || _p2RoundWins >= needed;
        }
        return _currentRound > _totalRounds;
    }

    void StartRound()
    {
        _state = GameState.Waiting;
        _p1Reacted = false;
        _p2Reacted = false;
        _p1Ms = -1;
        _p2Ms = -1;
        _p1Trap = false;
        _p2Trap = false;

        // 신호 유형 결정
        _signalType = _rng.Next(3) switch
        {
            0 => SignalType.Simple,
            1 => SignalType.ColorCondition,
            _ => SignalType.ShapeCondition,
        };

        // 신호 조건 설정
        if (_signalType == SignalType.ColorCondition)
        {
            var target = _colors[_rng.Next(_colors.Length)];
            _currentCondition = $"신호가 [{target.Name}]일 때만 누르세요!";
        }
        else if (_signalType == SignalType.ShapeCondition)
        {
            _currentCondition = "신호가 원(●)일 때만 누르세요!";
        }
        else
        {
            _currentCondition = "";
        }

        UpdateUI();
        ScheduleSignal();
    }

    void ScheduleSignal()
    {
        double delay = 1500 + _rng.NextDouble() * 3500;  // 1.5~5초 랜덤 대기
        bool willTrap = _trapEnabled && _rng.Next(4) == 0;  // 25% 확률 함정

        _waitTimer?.Stop();
        _waitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
        _waitTimer.Tick += (_, _) =>
        {
            _waitTimer.Stop();
            if (willTrap)
                ShowTrap();
            else
                ShowGo();
        };
        _waitTimer.Start();

        _state = GameState.Waiting;
        UpdateUI();
    }

    void ShowGo()
    {
        _state = GameState.Go;
        _sw.Restart();
        UpdateSignalGo();
    }

    void ShowTrap()
    {
        _state = GameState.Trap;
        UpdateSignalTrap();

        // 함정은 1.5초 후 사라지고 실제 신호 재스케줄
        _waitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _waitTimer.Tick += (_, _) =>
        {
            _waitTimer.Stop();
            _state = GameState.Waiting;
            UpdateUI();
            ScheduleSignal();
        };
        _waitTimer.Start();
    }

    void React(int player)
    {
        if (_state == GameState.Trap)
        {
            // 함정에 눌렀다!
            if (player == 1 && !_p1Reacted) { _p1Reacted = true; _p1Trap = true; _p1Ms = 0; }
            if (player == 2 && !_p2Reacted) { _p2Reacted = true; _p2Trap = true; _p2Ms = 0; }
            CheckBothReacted();
            return;
        }

        if (_state != GameState.Go) return;

        double ms = _sw.Elapsed.TotalMilliseconds;
        if (player == 1 && !_p1Reacted) { _p1Reacted = true; _p1Ms = ms; }
        if (player == 2 && !_p2Reacted) { _p2Reacted = true; _p2Ms = ms; }

        UpdateReactionDisplay(player, ms);
        CheckBothReacted();
    }

    void CheckBothReacted()
    {
        if (!_p1Reacted || !_p2Reacted) return;

        _waitTimer?.Stop();
        DetermineRoundWinner();
    }

    void DetermineRoundWinner()
    {
        _state = GameState.RoundEnd;

        // 함정 패널티: 함정 누른 쪽은 패배
        if (_p1Trap && !_p2Trap) { _p2RoundWins++; }
        else if (_p2Trap && !_p1Trap) { _p1RoundWins++; }
        else if (_p1Trap && _p2Trap) { /* 둘 다 함정 — 무승부 */ }
        else
        {
            // 정상 반응: 빠른 쪽 승리
            if (_p1Ms <= _p2Ms) _p1RoundWins++;
            else _p2RoundWins++;
        }

        UpdateUI();

        // 자동으로 1.5초 후 다음 라운드 예약 (마지막 라운드 제외)
        if (!IsGameOver() && _currentRound < _totalRounds)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            t.Tick += (_, _) => { t.Stop(); _currentRound++; StartRound(); };
            t.Start();
        }
    }

    void EndGame()
    {
        _state = GameState.GameOver;
        if (_p1RoundWins > _p2RoundWins) _p1TotalWins++;
        else if (_p2RoundWins > _p1RoundWins) _p2TotalWins++;
        UpdateUI();
    }

    void AbortGame()
    {
        _waitTimer?.Stop();
        _state = GameState.Idle;
        _currentRound = 0;
        UpdateUI();
    }

    // ─── UI 업데이트 ──────────────────────────────────────────────────────
    void UpdateUI()
    {
        P1Score.Text = _p1RoundWins.ToString();
        P2Score.Text = _p2RoundWins.ToString();
        P1Wins.Text = $"총 승: {_p1TotalWins}";
        P2Wins.Text = $"총 승: {_p2TotalWins}";
        RoundText.Text = _currentRound == 0 ? "준비" : $"라운드 {_currentRound} / {_totalRounds}";

        bool showCondition = _signalType != SignalType.Simple && _state != GameState.Idle && _state != GameState.GameOver;
        ConditionBorder.Visibility = showCondition ? Visibility.Visible : Visibility.Collapsed;
        ConditionText.Text = _currentCondition;

        switch (_state)
        {
            case GameState.Idle:
                SetSignal("#2A2A2A", "#444", "⏸");
                StatusText.Text = "Space 또는 시작 버튼으로 게임 시작";
                BtnStart.Content = "▶ 시작 (Space)";
                BtnStart.IsEnabled = true;
                break;

            case GameState.Waiting:
                SetSignal("#2A2A2A", "#444", "⌛");
                StatusText.Text = "신호 대기 중...";
                BtnStart.IsEnabled = false;
                break;

            case GameState.Go:
                // UpdateSignalGo에서 처리
                StatusText.Text = "누르세요!";
                BtnStart.IsEnabled = false;
                break;

            case GameState.Trap:
                // UpdateSignalTrap에서 처리
                StatusText.Text = "⚠️ 함정! 누르면 패배!";
                BtnStart.IsEnabled = false;
                break;

            case GameState.RoundEnd:
                StatusText.Text = GetRoundResultText();
                BtnStart.Content = IsGameOver() ? "🏁 결과 보기" : "▶ 다음 라운드 (Space)";
                BtnStart.IsEnabled = true;
                break;

            case GameState.GameOver:
                SetSignal("#1A2A1A", "#66BB6A", "🏆");
                StatusText.Text = GetGameOverText();
                BtnStart.Content = "↺ 다시 시작 (Space)";
                BtnStart.IsEnabled = true;
                break;
        }
    }

    void UpdateSignalGo()
    {
        SetSignal("#1A2A1A", "#66BB6A", "GO!");
    }

    void UpdateSignalTrap()
    {
        SetSignal("#2A1A1A", "#EF5350", "✋");
    }

    void SetSignal(string bg, string stroke, string emoji)
    {
        SignalCircle.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
        SignalCircle.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(stroke));
        SignalEmoji.Text = emoji;
    }

    void UpdateReactionDisplay(int player, double ms)
    {
        string display = $"{ms:F1} ms";
        if (player == 1) P1LastTime.Text = display;
        else P2LastTime.Text = display;
    }

    string GetRoundResultText()
    {
        if (_p1Trap && _p2Trap) return "⚠️ 둘 다 함정 — 무승부";
        if (_p1Trap) return $"P1 함정! P2 승 ({_p2Ms:F1}ms)";
        if (_p2Trap) return $"P2 함정! P1 승 ({_p1Ms:F1}ms)";
        if (_p1Ms <= _p2Ms)
            return $"P1 승! ({_p1Ms:F1}ms vs {_p2Ms:F1}ms, 차이: {_p2Ms - _p1Ms:F1}ms)";
        return $"P2 승! ({_p2Ms:F1}ms vs {_p1Ms:F1}ms, 차이: {_p1Ms - _p2Ms:F1}ms)";
    }

    string GetGameOverText()
    {
        if (_p1RoundWins > _p2RoundWins) return $"🎉 Player 1 최종 승리! ({_p1RoundWins} vs {_p2RoundWins})";
        if (_p2RoundWins > _p1RoundWins) return $"🎉 Player 2 최종 승리! ({_p2RoundWins} vs {_p1RoundWins})";
        return $"🤝 무승부! ({_p1RoundWins} vs {_p2RoundWins})";
    }

    // ─── 버튼 이벤트 ──────────────────────────────────────────────────────
    void BtnTournament_Click(object sender, RoutedEventArgs e)
    {
        _isTournamentMode = !_isTournamentMode;
        BtnTournament.Content = _isTournamentMode ? "🏆 토너먼트 ON" : "🏆 토너먼트";
        GameModeText.Text = _isTournamentMode ? "● 토너먼트 모드 (선3승)" : "● 일반 모드 (5라운드)";
        if (_isTournamentMode) _totalRounds = 5;
        AbortGame();
    }

    void BtnResetScore_Click(object sender, RoutedEventArgs e)
    {
        _p1TotalWins = 0;
        _p2TotalWins = 0;
        AbortGame();
    }

    void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsDialog(_totalRounds, _trapEnabled)
        {
            Owner = this
        };
        if (dlg.ShowDialog() == true)
        {
            _totalRounds = dlg.Rounds;
            _trapEnabled = dlg.TrapEnabled;
            GameModeText.Text = $"● {(_isTournamentMode ? "토너먼트" : "일반")} 모드 ({_totalRounds}라운드{(_trapEnabled ? ", 함정" : "")})";
        }
    }
}
