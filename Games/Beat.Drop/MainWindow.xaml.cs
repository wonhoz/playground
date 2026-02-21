using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using BeatDrop.Engine;

namespace BeatDrop;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // 레이아웃 상수
    private const double LaneWidth = 80;
    private const double LaneCount = 4;
    private const double FieldWidth = LaneWidth * LaneCount; // 320
    private const double FieldLeft = (484 - FieldWidth) / 2; // 중앙 정렬
    private const double HitLineY = 580; // 판정 라인 Y
    private const double NoteHeight = 20;
    private const double NoteSpeed = 450; // px/초
    private const double SongDuration = 45; // 곡 길이 (초)

    private static readonly Color[] LaneColors = [
        Color.FromRgb(0x3A, 0x86, 0xFF), // D - 파랑
        Color.FromRgb(0x2E, 0xCC, 0x71), // F - 초록
        Color.FromRgb(0xFF, 0xD7, 0x00), // J - 노랑
        Color.FromRgb(0xE7, 0x4C, 0x3C), // K - 빨강
    ];

    private static readonly string[] LaneKeys = ["D", "F", "J", "K"];
    private static readonly Key[] LaneKeyBinds = [Key.D, Key.F, Key.J, Key.K];

    private readonly GameLoop _loop = new();
    private readonly ScoreManager _score = new();
    private readonly Random _rng = new();

    private List<Note> _notes = [];
    private readonly Dictionary<Note, Rectangle> _noteVisuals = [];
    private readonly Rectangle[] _laneFlash = new Rectangle[4];
    private readonly double[] _flashTimer = new double[4];

    // 설정
    private Difficulty _difficulty = Difficulty.Normal;
    private int _bpm = 140;

    // 상태
    private enum GameState { Title, Playing, Result }
    private GameState _state = GameState.Title;
    private double _songTime;
    private double _judgeShowTimer;

    // 스태틱 비주얼
    private readonly List<UIElement> _staticVisuals = [];

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                int value = 1;
                DwmSetWindowAttribute(source.Handle, 20, ref value, sizeof(int));
            }
            _loop.OnUpdate += OnUpdate;
            _loop.OnRender += OnRender;
            _loop.Start();
            Focus();
        };
    }

    // ── 게임 시작 ──────────────────────────────────────

    private void StartGame()
    {
        ClearField();
        DrawField();

        _score.Reset();
        _songTime = 0;
        _judgeShowTimer = 0;

        _notes = PatternGenerator.Generate(_bpm, SongDuration, _difficulty, _rng);
        _score.TotalNotes = _notes.Count;

        // 노트 비주얼 생성
        foreach (var note in _notes)
        {
            var rect = new Rectangle
            {
                Width = LaneWidth - 8,
                Height = note.IsLong ? Math.Max(NoteHeight, note.Duration * NoteSpeed) : NoteHeight,
                Fill = new SolidColorBrush(LaneColors[note.Lane]),
                RadiusX = 6, RadiusY = 6,
                Opacity = 0.9,
                Stroke = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                StrokeThickness = 1
            };
            _noteVisuals[note] = rect;
            GameCanvas.Children.Add(rect);
        }

        _state = GameState.Playing;
        TitlePanel.Visibility = Visibility.Collapsed;
        ResultOverlay.Visibility = Visibility.Collapsed;
        HudPanel.Visibility = Visibility.Visible;
        JudgeText.Visibility = Visibility.Collapsed;
    }

    private void DrawField()
    {
        // 레인 배경
        for (int i = 0; i < LaneCount; i++)
        {
            var laneBg = new Rectangle
            {
                Width = LaneWidth,
                Height = 662,
                Fill = new SolidColorBrush(Color.FromArgb((byte)(i % 2 == 0 ? 20 : 30), 255, 255, 255))
            };
            Canvas.SetLeft(laneBg, FieldLeft + i * LaneWidth);
            _staticVisuals.Add(laneBg);
            GameCanvas.Children.Add(laneBg);

            // 레인 구분선
            if (i > 0)
            {
                var divider = new Rectangle
                {
                    Width = 1, Height = 662,
                    Fill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
                };
                Canvas.SetLeft(divider, FieldLeft + i * LaneWidth);
                _staticVisuals.Add(divider);
                GameCanvas.Children.Add(divider);
            }
        }

        // 판정 라인
        var hitLine = new Rectangle
        {
            Width = FieldWidth + 4, Height = 3,
            Fill = new SolidColorBrush(Colors.White),
            Opacity = 0.8
        };
        Canvas.SetLeft(hitLine, FieldLeft - 2);
        Canvas.SetTop(hitLine, HitLineY);
        _staticVisuals.Add(hitLine);
        GameCanvas.Children.Add(hitLine);

        // 키 라벨 + 플래시 영역
        for (int i = 0; i < LaneCount; i++)
        {
            // 키 라벨
            var label = new TextBlock
            {
                Text = LaneKeys[i],
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(LaneColors[i]),
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Canvas.SetLeft(label, FieldLeft + i * LaneWidth + LaneWidth / 2 - 7);
            Canvas.SetTop(label, HitLineY + 10);
            _staticVisuals.Add(label);
            GameCanvas.Children.Add(label);

            // 플래시 영역 (키 누를 때 빛남)
            _laneFlash[i] = new Rectangle
            {
                Width = LaneWidth - 2, Height = 40,
                Fill = new SolidColorBrush(Color.FromArgb(0, LaneColors[i].R, LaneColors[i].G, LaneColors[i].B)),
                RadiusX = 4, RadiusY = 4
            };
            Canvas.SetLeft(_laneFlash[i], FieldLeft + i * LaneWidth + 1);
            Canvas.SetTop(_laneFlash[i], HitLineY - 20);
            _staticVisuals.Add(_laneFlash[i]);
            GameCanvas.Children.Add(_laneFlash[i]);
        }

        // 사이드 장식
        var leftBar = new Rectangle
        {
            Width = 3, Height = 662,
            Fill = new LinearGradientBrush(
                Color.FromRgb(0x00, 0xFF, 0xCC),
                Color.FromRgb(0xFF, 0x66, 0xAA), 90),
            Opacity = 0.4
        };
        Canvas.SetLeft(leftBar, FieldLeft - 3);
        _staticVisuals.Add(leftBar);
        GameCanvas.Children.Add(leftBar);

        var rightBar = new Rectangle
        {
            Width = 3, Height = 662,
            Fill = new LinearGradientBrush(
                Color.FromRgb(0xFF, 0x66, 0xAA),
                Color.FromRgb(0x00, 0xFF, 0xCC), 90),
            Opacity = 0.4
        };
        Canvas.SetLeft(rightBar, FieldLeft + FieldWidth);
        _staticVisuals.Add(rightBar);
        GameCanvas.Children.Add(rightBar);
    }

    // ── 게임 루프 ──────────────────────────────────────

    private void OnUpdate(double dt)
    {
        if (_state != GameState.Playing) return;

        _songTime += dt;

        // 판정 표시 타이머
        if (_judgeShowTimer > 0)
        {
            _judgeShowTimer -= dt;
            if (_judgeShowTimer <= 0) JudgeText.Visibility = Visibility.Collapsed;
        }

        // 플래시 감쇠
        for (int i = 0; i < 4; i++)
        {
            if (_flashTimer[i] > 0)
            {
                _flashTimer[i] -= dt;
                byte alpha = (byte)(Math.Max(0, _flashTimer[i] / 0.15) * 120);
                _laneFlash[i].Fill = new SolidColorBrush(
                    Color.FromArgb(alpha, LaneColors[i].R, LaneColors[i].G, LaneColors[i].B));
            }
        }

        // 미스 체크 (판정 라인 지남)
        foreach (var note in _notes)
        {
            if (note.IsProcessed) continue;
            if (_songTime - note.HitTime > ScoreManager.MissWindow)
            {
                note.IsMissed = true;
                note.Grade = HitGrade.Miss;
                _score.RegisterHit(HitGrade.Miss);
                ShowJudge(HitGrade.Miss);
            }
        }

        // HUD
        ScoreText.Text = _score.Score.ToString("N0");
        ComboText.Text = _score.Combo >= 2 ? $"{_score.Combo}" : "";
        AccText.Text = $"{_score.Accuracy:F1}%";

        // 곡 종료
        if (_songTime >= SongDuration + 2)
            ShowResult();
    }

    private void OnRender()
    {
        if (_state != GameState.Playing) return;

        // 노트 위치 동기화
        foreach (var note in _notes)
        {
            if (!_noteVisuals.TryGetValue(note, out var rect)) continue;

            if (note.IsProcessed)
            {
                rect.Visibility = Visibility.Collapsed;
                continue;
            }

            double timeDiff = note.HitTime - _songTime;
            double y = HitLineY - timeDiff * NoteSpeed - NoteHeight;
            double x = FieldLeft + note.Lane * LaneWidth + 4;

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);

            // 화면 밖이면 숨기기
            rect.Visibility = y > -100 && y < 700 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ── 입력 처리 ──────────────────────────────────────

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_state == GameState.Playing)
        {
            for (int i = 0; i < 4; i++)
            {
                if (e.Key == LaneKeyBinds[i])
                {
                    ProcessLaneInput(i);
                    _flashTimer[i] = 0.15;
                    break;
                }
            }
        }

        if (e.Key == Key.Enter)
        {
            if (_state == GameState.Title) StartGame();
            else if (_state == GameState.Result) ShowTitle();
        }

        if (e.Key == Key.Escape && _state == GameState.Playing)
            ShowResult();
    }

    private void Window_KeyUp(object sender, KeyEventArgs e) { }

    private void ProcessLaneInput(int lane)
    {
        // 이 레인에서 가장 가까운 미처리 노트 찾기
        Note? closest = null;
        double closestDiff = double.MaxValue;

        foreach (var note in _notes)
        {
            if (note.IsProcessed || note.Lane != lane) continue;
            double diff = Math.Abs(_songTime - note.HitTime);
            if (diff < closestDiff && diff <= ScoreManager.MissWindow)
            {
                closest = note;
                closestDiff = diff;
            }
        }

        if (closest is null) return;

        var grade = _score.Judge(_songTime - closest.HitTime);
        closest.IsHit = true;
        closest.Grade = grade;
        _score.RegisterHit(grade);
        ShowJudge(grade);
    }

    private void ShowJudge(HitGrade grade)
    {
        var (text, color) = grade switch
        {
            HitGrade.Perfect => ("PERFECT!", Color.FromRgb(0x00, 0xFF, 0xCC)),
            HitGrade.Great => ("GREAT!", Color.FromRgb(0x3A, 0x86, 0xFF)),
            HitGrade.Good => ("GOOD", Color.FromRgb(0xFF, 0xD7, 0x00)),
            _ => ("MISS", Color.FromRgb(0xE7, 0x4C, 0x3C))
        };
        JudgeText.Text = text;
        JudgeText.Foreground = new SolidColorBrush(color);
        ((DropShadowEffect)JudgeText.Effect).Color = color;
        JudgeText.Visibility = Visibility.Visible;
        _judgeShowTimer = 0.4;
    }

    // ── 결과 ──────────────────────────────────────────

    private void ShowResult()
    {
        _state = GameState.Result;
        HudPanel.Visibility = Visibility.Collapsed;
        JudgeText.Visibility = Visibility.Collapsed;

        var rankColor = _score.Rank switch
        {
            "S" => Color.FromRgb(0xFF, 0xD7, 0x00),
            "A" => Color.FromRgb(0x00, 0xFF, 0xCC),
            "B" => Color.FromRgb(0x3A, 0x86, 0xFF),
            "C" => Color.FromRgb(0xFF, 0xA5, 0x00),
            _ => Color.FromRgb(0xE7, 0x4C, 0x3C)
        };

        RankText.Text = _score.Rank;
        RankText.Foreground = new SolidColorBrush(rankColor);
        ((DropShadowEffect)RankText.Effect).Color = rankColor;
        FinalScoreText.Text = $"SCORE: {_score.Score:N0}";
        FinalAccText.Text = $"ACCURACY: {_score.Accuracy:F1}%";
        FinalDetailText.Text = $"PERFECT: {_score.PerfectCount}  GREAT: {_score.GreatCount}\n" +
                               $"GOOD: {_score.GoodCount}  MISS: {_score.MissCount}\n" +
                               $"MAX COMBO: {_score.MaxCombo}";

        ResultOverlay.Visibility = Visibility.Visible;
    }

    private void ShowTitle()
    {
        ClearField();
        _state = GameState.Title;
        HudPanel.Visibility = Visibility.Collapsed;
        ResultOverlay.Visibility = Visibility.Collapsed;
        TitlePanel.Visibility = Visibility.Visible;
        Focus();
    }

    private void ClearField()
    {
        foreach (var v in _staticVisuals) GameCanvas.Children.Remove(v);
        _staticVisuals.Clear();
        foreach (var (_, rect) in _noteVisuals) GameCanvas.Children.Remove(rect);
        _noteVisuals.Clear();
    }

    // ── 난이도/BPM 선택 ──────────────────────────────

    private void Easy_Click(object s, System.Windows.Input.MouseButtonEventArgs e) { _difficulty = Difficulty.Easy; UpdateDiffButtons(); }
    private void Normal_Click(object s, System.Windows.Input.MouseButtonEventArgs e) { _difficulty = Difficulty.Normal; UpdateDiffButtons(); }
    private void Hard_Click(object s, System.Windows.Input.MouseButtonEventArgs e) { _difficulty = Difficulty.Hard; UpdateDiffButtons(); }
    private void Bpm120_Click(object s, System.Windows.Input.MouseButtonEventArgs e) { _bpm = 120; }
    private void Bpm140_Click(object s, System.Windows.Input.MouseButtonEventArgs e) { _bpm = 140; }
    private void Bpm160_Click(object s, System.Windows.Input.MouseButtonEventArgs e) { _bpm = 160; }
    private void Bpm180_Click(object s, System.Windows.Input.MouseButtonEventArgs e) { _bpm = 180; }

    private void UpdateDiffButtons()
    {
        EasyBtn.BorderThickness = new Thickness(_difficulty == Difficulty.Easy ? 3 : 1);
        NormalBtn.BorderThickness = new Thickness(_difficulty == Difficulty.Normal ? 3 : 1);
        HardBtn.BorderThickness = new Thickness(_difficulty == Difficulty.Hard ? 3 : 1);
    }
}
