using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MorseRun;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // ── 모스 코드 테이블 ──────────────────────────────────────
    private static readonly Dictionary<string, char> MorseToChar = new()
    {
        [".-"] = 'A', ["-..."] = 'B', ["-.-."] = 'C', ["-.."] = 'D',
        ["."] = 'E', ["..-."] = 'F', ["--."] = 'G', ["...."] = 'H',
        [".."] = 'I', [".---"] = 'J', ["-.-"] = 'K', [".-.."] = 'L',
        ["--"] = 'M', ["-."] = 'N', ["---"] = 'O', [".--."] = 'P',
        ["--.-"] = 'Q', [".-."] = 'R', ["..."] = 'S', ["-"] = 'T',
        ["..-"] = 'U', ["...-"] = 'V', [".--"] = 'W', ["-..-"] = 'X',
        ["-.--"] = 'Y', ["--.."] = 'Z',
        [".----"] = '1', ["..---"] = '2', ["...--"] = '3', ["....-"] = '4',
        ["....."] = '5', ["-...."] = '6', ["--..."] = '7', ["---.."] = '8',
        ["----."] = '9', ["-----"] = '0'
    };
    private static readonly Dictionary<char, string> CharToMorse =
        MorseToChar.ToDictionary(kv => kv.Value, kv => kv.Key);

    // ── 액션 매핑 ─────────────────────────────────────────────
    // A=Jump, J=DoubleJump, S=Slide, E=none (모스 특성상 자유 매핑)
    private static readonly Dictionary<char, GameAction> ActionMap = new()
    {
        ['A'] = GameAction.Jump,     // .-
        ['J'] = GameAction.DJump,    // .---
        ['S'] = GameAction.Slide,    // ...
        ['T'] = GameAction.Jump,     // -
        ['E'] = GameAction.Jump,     // .
        ['I'] = GameAction.Jump,     // ..
        ['N'] = GameAction.Jump,     // -.
    };

    // ── 게임 상태 ─────────────────────────────────────────────
    private enum GameState { Idle, Playing, Paused, GameOver }
    private enum GameAction { None, Jump, DJump, Slide }

    private GameState _state = GameState.Idle;
    private double _playerX = 80;
    private double _playerY = 0;      // 지면 위로 얼마나 떠있는지
    private double _velocityY = 0;
    private bool _isSliding = false;
    private bool _canDoubleJump = false;
    private int _score = 0;
    private int _lives = 3;
    private int _level = 1;
    private double _speed = 3.0;      // 장애물 이동 속도
    private double _groundY = 0;      // 실제 지면 Y좌표 (캔버스 기준)
    private double _canvasH = 400;
    private double _canvasW = 900;
    private const double PlayerH = 40;
    private const double PlayerW = 30;
    private const double Gravity = 0.6;
    private const double JumpForce = -12.0;

    private readonly DispatcherTimer _gameTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly List<ObstacleObj> _obstacles = [];
    private readonly Random _rng = new();

    // 모스 입력
    private string _currentMorse = "";
    private DateTime _keyDownTime;
    private const int DashThresholdMs = 300;
    private bool _spaceHeld = false;

    // 문제 출제
    private char _questionChar = 'A';
    private int _frameCount = 0;
    private int _nextObstacleFrame = 80;
    private int _correctStreak = 0;

    public MainWindow()
    {
        InitializeComponent();
        var handle = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
        int v = 1;
        DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 모스 코드 표 생성
        MorseTable.ItemsSource = CharToMorse
            .Where(kv => char.IsLetter(kv.Key))
            .OrderBy(kv => kv.Key)
            .Select(kv => new MorseTableItem(kv.Key.ToString(), kv.Value))
            .ToList();

        _gameTimer.Tick += GameLoop;
        RefreshHUD();
        SetStatus("Space 짧게=점, Space 길게=선, Enter=확정");
    }

    private void GameCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _canvasH = e.NewSize.Height;
        _canvasW = e.NewSize.Width;
        _groundY = _canvasH - 20 - PlayerH;

        Canvas.SetTop(Ground, _canvasH - 20);
        Ground.Width = _canvasW;

        Canvas.SetLeft(Overlay, 0);
        Canvas.SetTop(Overlay, 0);
        (Overlay as FrameworkElement)!.Width = _canvasW;
        (Overlay as FrameworkElement)!.Height = _canvasH;

        Canvas.SetLeft(MorseIndicator, 10);
        Canvas.SetTop(MorseIndicator, _canvasH - 80);
        Canvas.SetLeft(QuestionBox, _canvasW - 170);
        Canvas.SetTop(QuestionBox, 10);
    }

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (_state == GameState.GameOver || _state == GameState.Idle)
            StartGame();
        else if (_state == GameState.Paused)
            ResumeGame();
    }

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        if (_state == GameState.Playing) PauseGame();
        else if (_state == GameState.Paused) ResumeGame();
    }

    private void StartGame()
    {
        _score = 0; _lives = 3; _level = 1; _speed = 3.0;
        _currentMorse = ""; _correctStreak = 0;
        _playerY = 0; _velocityY = 0; _isSliding = false; _canDoubleJump = false;
        _obstacles.Clear();
        foreach (var c in GameCanvas.Children.OfType<FrameworkElement>().Where(x => x.Tag is ObstacleObj).ToList())
            GameCanvas.Children.Remove(c);

        _frameCount = 0; _nextObstacleFrame = 80;
        _state = GameState.Playing;
        Overlay.Visibility = Visibility.Collapsed;
        BtnStart.Content = "재시작";
        BtnPause.IsEnabled = true;

        PickNewQuestion();
        RefreshHUD();
        _gameTimer.Start();
        this.Focus();
    }

    private void PauseGame()
    {
        _state = GameState.Paused;
        _gameTimer.Stop();
        OverlayTitle.Text = "⏸ 일시정지";
        OverlayMsg.Text = "계속하려면 [Esc] 또는 [시작] 버튼";
        OverlayScore.Text = $"현재 점수: {_score}";
        Overlay.Visibility = Visibility.Visible;
    }

    private void ResumeGame()
    {
        _state = GameState.Playing;
        Overlay.Visibility = Visibility.Collapsed;
        _gameTimer.Start();
        this.Focus();
    }

    private void GameOver()
    {
        _state = GameState.GameOver;
        _gameTimer.Stop();
        OverlayTitle.Text = "💀 게임 오버";
        OverlayMsg.Text = $"레벨 {_level}까지 생존!\n\n다시 도전하려면 [시작]을 눌러주세요.";
        OverlayScore.Text = $"최종 점수: {_score}";
        BtnStart.Content = "▶ 다시 시작";
        Overlay.Visibility = Visibility.Visible;
        BtnPause.IsEnabled = false;
    }

    // ── 게임 루프 ─────────────────────────────────────────────
    private void GameLoop(object? sender, EventArgs e)
    {
        _frameCount++;
        _score += 1;

        // 레벨 업
        if (_score > 0 && _score % 1000 == 0)
        {
            _level++;
            _speed = Math.Min(12.0, _speed + 0.5);
            LblLevel.Text = _level.ToString();
        }

        // 물리
        UpdatePhysics();

        // 플레이어 렌더
        RenderPlayer();

        // 다리 애니메이션
        AnimateLegs();

        // 장애물 생성
        if (_frameCount >= _nextObstacleFrame)
        {
            SpawnObstacle();
            _nextObstacleFrame = _frameCount + _rng.Next(60, 120);
        }

        // 장애물 이동 & 충돌
        UpdateObstacles();

        // 스코어 HUD
        LblScore.Text = _score.ToString();
    }

    private void UpdatePhysics()
    {
        if (_isSliding)
        {
            _playerY = 0;
            return;
        }
        _velocityY += Gravity;
        _playerY -= _velocityY;
        if (_playerY <= 0)
        {
            _playerY = 0;
            _velocityY = 0;
            _canDoubleJump = true;
        }
    }

    private void RenderPlayer()
    {
        double y = _groundY - _playerY;
        Canvas.SetLeft(PlayerCanvas, _playerX);
        Canvas.SetTop(PlayerCanvas, y);

        if (_isSliding)
        {
            PlayerCanvas.Width = 40;
            PlayerCanvas.Height = 20;
            // 슬라이딩 시 낮아짐
        }
        else
        {
            PlayerCanvas.Width = 30;
            PlayerCanvas.Height = 40;
        }
    }

    private double _legPhase = 0;
    private void AnimateLegs()
    {
        if (_playerY > 0 || _isSliding) return;
        _legPhase += 0.25;
        var angle1 = Math.Sin(_legPhase) * 25;
        var angle2 = Math.Sin(_legPhase + Math.PI) * 25;
        Leg1.RenderTransform = new RotateTransform(angle1, 3, 0);
        Leg2.RenderTransform = new RotateTransform(angle2, 3, 0);
    }

    private void SpawnObstacle()
    {
        bool isHigh = _rng.NextDouble() > 0.6; // 30% 확률로 공중 장애물
        double obstW = _rng.Next(20, 40);
        double obstH = isHigh ? 25 : _rng.Next(30, 60);
        double obstY = isHigh ? _groundY - 60 : _groundY + PlayerH - obstH;

        var rect = new Rectangle
        {
            Width = obstW,
            Height = obstH,
            Fill = new SolidColorBrush(isHigh
                ? Color.FromRgb(100, 60, 180)
                : Color.FromRgb(180, 80, 60)),
            RadiusX = 4, RadiusY = 4,
            Tag = null
        };

        double x = _canvasW + 20;
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, obstY);
        GameCanvas.Children.Add(rect);

        var obj = new ObstacleObj { Shape = rect, X = x, Y = obstY, W = obstW, H = obstH, IsHigh = isHigh };
        rect.Tag = obj;
        _obstacles.Add(obj);
    }

    private void UpdateObstacles()
    {
        for (int i = _obstacles.Count - 1; i >= 0; i--)
        {
            var obs = _obstacles[i];
            obs.X -= _speed;
            Canvas.SetLeft(obs.Shape, obs.X);

            if (obs.X + obs.W < 0)
            {
                GameCanvas.Children.Remove(obs.Shape);
                _obstacles.RemoveAt(i);
                continue;
            }

            // 충돌 검사
            if (CheckCollision(obs))
            {
                _lives--;
                RefreshHUD();
                GameCanvas.Children.Remove(obs.Shape);
                _obstacles.RemoveAt(i);

                // 히트 플래시
                FlashPlayer();
                if (_lives <= 0) { GameOver(); return; }
            }
        }
    }

    private bool CheckCollision(ObstacleObj obs)
    {
        double px = _playerX;
        double pw = _isSliding ? 40 : PlayerW;
        double ph = _isSliding ? 20 : PlayerH;
        double py = _groundY - _playerY;

        double margin = 6;
        bool xOverlap = px + pw - margin > obs.X + margin && px + margin < obs.X + obs.W - margin;
        bool yOverlap = py + ph - margin > obs.Y + margin && py + margin < obs.Y + obs.H - margin;
        return xOverlap && yOverlap;
    }

    private void FlashPlayer()
    {
        var anim = new ColorAnimation(Colors.Red, Colors.Cyan, TimeSpan.FromMilliseconds(300));
        // 간단 구현: 그냥 색만 바꿈 (복잡한 애니메이션 생략)
    }

    // ── 키보드 입력 ─────────────────────────────────────────────
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_state != GameState.Playing) return;
        if (e.IsRepeat) return;

        if (e.Key == Key.Space)
        {
            if (!_spaceHeld)
            {
                _spaceHeld = true;
                _keyDownTime = DateTime.UtcNow;
                HighlightDot(true);
            }
        }
        else if (e.Key == Key.Return || e.Key == Key.Enter)
        {
            ConfirmMorse();
        }
        else if (e.Key == Key.Back)
        {
            if (_currentMorse.Length > 0)
            {
                _currentMorse = _currentMorse[..^1];
                UpdateMorseDisplay();
            }
        }
        else if (e.Key == Key.Escape)
        {
            PauseGame();
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (_state != GameState.Playing) return;
        if (e.Key == Key.Space && _spaceHeld)
        {
            _spaceHeld = false;
            HighlightDot(false);
            var held = (DateTime.UtcNow - _keyDownTime).TotalMilliseconds;
            if (held >= DashThresholdMs)
            {
                _currentMorse += "-";
                HighlightDash(true);
                DispatcherTimerExt.RunOnce(TimeSpan.FromMilliseconds(150), () => HighlightDash(false));
            }
            else
            {
                _currentMorse += ".";
            }
            UpdateMorseDisplay();
        }
    }

    private void ConfirmMorse()
    {
        if (string.IsNullOrEmpty(_currentMorse)) return;

        if (MorseToChar.TryGetValue(_currentMorse, out char decoded))
        {
            ExecuteAction(decoded);
            // 문제 정답 체크
            if (decoded == _questionChar)
            {
                _correctStreak++;
                _score += 50 + _correctStreak * 10;
                PickNewQuestion();
                SetStatus($"정답! 연속 {_correctStreak}회 — +{50 + _correctStreak * 10}점");
            }
            else
            {
                SetStatus($"'{decoded}' (입력: {_currentMorse}) — 정답: '{_questionChar}'");
            }
        }
        else
        {
            SetStatus($"알 수 없는 코드: {_currentMorse}");
        }

        _currentMorse = "";
        UpdateMorseDisplay();
    }

    private void ExecuteAction(char decoded)
    {
        var action = ActionMap.TryGetValue(decoded, out var a) ? a : GameAction.None;

        // 슬라이드는 S 또는 홀수 개의 점
        bool isSlideChar = decoded == 'S' || decoded == 'H' || decoded == '5';

        if (isSlideChar)
        {
            StartSlide();
        }
        else if (_playerY <= 0 || _isSliding)
        {
            // 지면 → 점프
            DoJump();
        }
        else if (_canDoubleJump)
        {
            // 공중 → 더블점프
            DoDoubleJump();
        }
    }

    private void DoJump()
    {
        _velocityY = -JumpForce;
        _playerY = 1;
        _canDoubleJump = true;
        _isSliding = false;
    }

    private void DoDoubleJump()
    {
        _velocityY = -JumpForce * 0.8;
        _canDoubleJump = false;
    }

    private DispatcherTimer? _slideTimer;
    private void StartSlide()
    {
        _isSliding = true;
        _slideTimer?.Stop();
        _slideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _slideTimer.Tick += (_, _) => { _isSliding = false; _slideTimer?.Stop(); };
        _slideTimer.Start();
    }

    private void PickNewQuestion()
    {
        var keys = CharToMorse.Keys.Where(char.IsLetter).ToList();
        _questionChar = keys[_rng.Next(keys.Count)];
        LblQuestion.Text = _questionChar.ToString();
        LblQuestionMorse.Text = CharToMorse[_questionChar];
    }

    private void UpdateMorseDisplay()
    {
        LblCurrentMorse.Text = string.IsNullOrEmpty(_currentMorse) ? "—" : _currentMorse;
        if (MorseToChar.TryGetValue(_currentMorse, out char c))
            LblTargetChar.Text = $"→ '{c}'";
        else
            LblTargetChar.Text = "?";
    }

    private void HighlightDot(bool on)
    {
        DotIndicator.Background = on
            ? new SolidColorBrush(Color.FromRgb(80, 200, 220))
            : new SolidColorBrush(Color.FromRgb(51, 51, 85));
    }

    private void HighlightDash(bool on)
    {
        DashIndicator.Background = on
            ? new SolidColorBrush(Color.FromRgb(80, 200, 220))
            : new SolidColorBrush(Color.FromRgb(51, 51, 85));
    }

    private void RefreshHUD()
    {
        LblScore.Text = _score.ToString();
        LblLives.Text = _lives.ToString();
        LblLevel.Text = _level.ToString();
    }

    private void SetStatus(string msg) => Dispatcher.Invoke(() => StatusBar.Text = msg);
}

// ── 데이터 클래스 ──────────────────────────────────────────────
public class ObstacleObj
{
    public required Rectangle Shape { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public bool IsHigh { get; set; }
}

public record MorseTableItem(string Char, string Code);

// DispatcherTimer 확장
public static class DispatcherTimerExt
{
    public static void RunOnce(TimeSpan delay, Action action)
    {
        var t = new DispatcherTimer { Interval = delay };
        t.Tick += (_, _) => { action(); t.Stop(); };
        t.Start();
    }
}
