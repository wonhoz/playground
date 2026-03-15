using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using EarTrain.Models;
using EarTrain.Services;

namespace EarTrain;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    private readonly AudioService _audio = new();
    private readonly DbService _db = new();
    private readonly Random _rng = new();

    // ─── 음표 정의 (C4 ~ B5, 2옥타브) ───────────────────────────────────
    private static readonly Note[] Notes =
    [
        new("C4",  60), new("C#4", 61, true), new("D4",  62), new("D#4", 63, true),
        new("E4",  64), new("F4",  65), new("F#4", 66, true), new("G4",  67),
        new("G#4", 68, true), new("A4", 69), new("A#4", 70, true), new("B4", 71),
        new("C5",  72), new("C#5", 73, true), new("D5",  74), new("D#5", 75, true),
        new("E5",  76), new("F5",  77), new("F#5", 78, true), new("G5",  79),
        new("G#5", 80, true), new("A5", 81), new("A#5", 82, true), new("B5", 83),
    ];

    // ─── 게임 상태 ────────────────────────────────────────────────────────
    private Note? _currentNote;
    private Interval? _currentInterval;
    private ChordType? _currentChord;
    private Note? _currentChordRoot;
    private int _sessionCorrect = 0;
    private int _sessionTotal = 0;
    private bool _noteAnswered = false;
    private bool _intervalAnswered = false;
    private bool _chordAnswered = false;
    private bool _melodicMode = false;  // 선율적 vs 화성적 인터벌

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

        BuildPiano();
        BuildIntervalChoices();
        BuildChordChoices();
        NewNoteQuestion();
    }

    void Window_Closed(object sender, EventArgs e)
    {
        _audio.Dispose();
        _db.Dispose();
    }

    // ─── 피아노 건반 그리기 ───────────────────────────────────────────────
    void BuildPiano()
    {
        PianoCanvas.Children.Clear();
        var whiteNotes = Notes.Where(n => !n.IsBlack).ToList();
        double ww = 36, wh = 140, bw = 22, bh = 88;

        // 흰 건반 먼저
        for (int i = 0; i < whiteNotes.Count; i++)
        {
            var note = whiteNotes[i];
            var rect = new Rectangle
            {
                Width = ww - 1, Height = wh,
                Fill = Brushes.White, Stroke = Brushes.Gray, StrokeThickness = 1,
                RadiusX = 3, RadiusY = 3, Tag = note, Cursor = Cursors.Hand
            };
            rect.MouseDown += PianoKey_Click;
            rect.MouseEnter += (_, _) => { if (rect.Fill != Brushes.LightBlue) rect.Fill = new SolidColorBrush(Color.FromRgb(220, 235, 255)); };
            rect.MouseLeave += (_, _) => { if (rect.Fill != Brushes.LightBlue) rect.Fill = Brushes.White; };

            Canvas.SetLeft(rect, i * ww);
            Canvas.SetTop(rect, 0);
            PianoCanvas.Children.Add(rect);

            // 음표 이름 레이블
            var lbl = new TextBlock
            {
                Text = note.Name, FontSize = 9, Foreground = Brushes.Gray,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(lbl, i * ww + 4);
            Canvas.SetTop(lbl, wh - 20);
            PianoCanvas.Children.Add(lbl);
        }

        // 검은 건반 (위에 그림)
        int wi = 0;
        for (int i = 0; i < Notes.Length; i++)
        {
            if (!Notes[i].IsBlack) { wi++; continue; }
            var note = Notes[i];
            double left = (wi - 1) * ww + ww * 0.65;
            var rect = new Rectangle
            {
                Width = bw, Height = bh,
                Fill = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
                Stroke = Brushes.Black, StrokeThickness = 1,
                RadiusX = 3, RadiusY = 3, Tag = note, Cursor = Cursors.Hand
            };
            rect.MouseDown += PianoKey_Click;
            rect.MouseEnter += (_, _) => rect.Fill = new SolidColorBrush(Color.FromRgb(50, 80, 140));
            rect.MouseLeave += (_, _) => rect.Fill = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, 0);
            Canvas.SetZIndex(rect, 1);
            PianoCanvas.Children.Add(rect);
        }

        PianoCanvas.Width = whiteNotes.Count * ww;
    }

    void PianoKey_Click(object sender, MouseButtonEventArgs e)
    {
        if (_noteAnswered) return;
        if (sender is Rectangle { Tag: Note clicked })
            CheckNoteAnswer(clicked);
    }

    // ─── 단음 퀴즈 ────────────────────────────────────────────────────────
    void NewNoteQuestion()
    {
        _noteAnswered = false;
        _currentNote = Notes[_rng.Next(Notes.Length)];
        NoteHint.Text = "";
        NoteResultBorder.Visibility = Visibility.Collapsed;
        BtnNextNote.Visibility = Visibility.Collapsed;
        ResetPianoColors();
        StatusBar.Text = $"▶ 재생을 눌러 음을 들으세요";
    }

    void ResetPianoColors()
    {
        foreach (var child in PianoCanvas.Children.OfType<Rectangle>())
        {
            if (child.Tag is Note n)
                child.Fill = n.IsBlack
                    ? new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22))
                    : Brushes.White;
        }
    }

    void BtnPlayNote_Click(object sender, RoutedEventArgs e)
    {
        if (_currentNote != null)
            _audio.PlayNote(_currentNote.Frequency);
    }

    void BtnHint_Click(object sender, RoutedEventArgs e)
    {
        if (_currentNote != null)
            NoteHint.Text = $"옥타브: {(_currentNote.MidiNote >= 72 ? "5" : "4")}옥타브 ({(_currentNote.IsBlack ? "검은" : "흰")} 건반)";
    }

    void CheckNoteAnswer(Note clicked)
    {
        if (_currentNote == null) return;
        _noteAnswered = true;
        bool correct = clicked.MidiNote == _currentNote.MidiNote;

        // 피아노 색상 표시
        foreach (var child in PianoCanvas.Children.OfType<Rectangle>())
        {
            if (child.Tag is Note n)
            {
                if (n.MidiNote == _currentNote.MidiNote)
                    child.Fill = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A));
                else if (n.MidiNote == clicked.MidiNote && !correct)
                    child.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
            }
        }

        ShowNoteResult(correct, clicked.Name, _currentNote.Name);
        UpdateElo("note_" + _currentNote.Name, correct);
        UpdateSession(correct);
        _db.SaveResult(new QuizResult(TrainMode.SingleNote, _currentNote.Name, clicked.Name, correct, DateTime.Now));
    }

    void ShowNoteResult(bool correct, string answered, string correct_name)
    {
        NoteResultBorder.Visibility = Visibility.Visible;
        NoteResultBorder.Background = new SolidColorBrush(correct ? Color.FromRgb(0x1A, 0x2A, 0x1A) : Color.FromRgb(0x2A, 0x1A, 0x1A));
        NoteResultText.Foreground = new SolidColorBrush(correct ? Color.FromRgb(0x66, 0xBB, 0x6A) : Color.FromRgb(0xEF, 0x53, 0x50));
        NoteResultText.Text = correct ? $"✅ 정답! {correct_name}" : $"❌ 오답. 정답은 {correct_name} (선택: {answered})";
        BtnNextNote.Visibility = Visibility.Visible;
        StatusBar.Text = correct ? "정답입니다!" : $"오답. 정답: {correct_name}";
    }

    void BtnNextNote_Click(object sender, RoutedEventArgs e) => NewNoteQuestion();

    // ─── 인터벌 퀴즈 ──────────────────────────────────────────────────────
    void BuildIntervalChoices()
    {
        IntervalPanel.Children.Clear();
        foreach (var interval in Interval.All)
        {
            var btn = MakeChoiceButton($"{interval.KorName}\n({interval.Name})");
            btn.Tag = interval;
            btn.Click += IntervalChoice_Click;
            btn.Width = 100; btn.Height = 60;
            btn.Margin = new Thickness(4);
            IntervalPanel.Children.Add(btn);
        }
    }

    void NewIntervalQuestion()
    {
        _intervalAnswered = false;
        _currentInterval = Interval.All[_rng.Next(Interval.All.Length)];
        IntervalResultBorder.Visibility = Visibility.Collapsed;
        BtnNextInterval.Visibility = Visibility.Collapsed;
        ResetChoiceButtons(IntervalPanel);
        StatusBar.Text = "▶ 재생을 눌러 인터벌을 들으세요";
    }

    void BtnPlayInterval_Click(object sender, RoutedEventArgs e)
    {
        if (_currentInterval == null) return;
        var root = Notes[_rng.Next(Notes.Length / 2)];  // 낮은 옥타브
        var upper = Notes.FirstOrDefault(n => n.MidiNote == root.MidiNote + _currentInterval.Semitones);
        if (upper == null) return;

        if (_melodicMode)
            _ = _audio.PlayMelodyAsync([(root.Frequency, 0.8), (upper.Frequency, 0.8)]);
        else
            _audio.PlayChord([root.Frequency, upper.Frequency], 1.5);
    }

    void BtnIntervalHarmonic_Click(object sender, RoutedEventArgs e) { _melodicMode = false; StatusBar.Text = "화성적 모드 (동시 재생)"; }
    void BtnIntervalMelodic_Click(object sender, RoutedEventArgs e) { _melodicMode = true; StatusBar.Text = "선율적 모드 (순차 재생)"; }

    void IntervalChoice_Click(object sender, RoutedEventArgs e)
    {
        if (_intervalAnswered || _currentInterval == null) return;
        _intervalAnswered = true;
        var btn = (Button)sender;
        var chosen = (Interval)btn.Tag!;
        bool correct = chosen.Semitones == _currentInterval.Semitones;

        HighlightChoice(IntervalPanel, _currentInterval.Semitones, chosen.Semitones, i => ((Interval)i).Semitones);
        IntervalResultBorder.Visibility = Visibility.Visible;
        IntervalResultBorder.Background = new SolidColorBrush(correct ? Color.FromRgb(0x1A, 0x2A, 0x1A) : Color.FromRgb(0x2A, 0x1A, 0x1A));
        IntervalResultText.Foreground = new SolidColorBrush(correct ? Color.FromRgb(0x66, 0xBB, 0x6A) : Color.FromRgb(0xEF, 0x53, 0x50));
        IntervalResultText.Text = correct ? $"✅ 정답! {_currentInterval.KorName}" : $"❌ 오답. 정답은 {_currentInterval.KorName}";
        BtnNextInterval.Visibility = Visibility.Visible;
        UpdateElo("interval_" + _currentInterval.Name, correct);
        UpdateSession(correct);
        _db.SaveResult(new QuizResult(TrainMode.Interval, _currentInterval.Name, chosen.Name, correct, DateTime.Now));
    }

    void BtnNextInterval_Click(object sender, RoutedEventArgs e) => NewIntervalQuestion();

    // ─── 화음 퀴즈 ────────────────────────────────────────────────────────
    void BuildChordChoices()
    {
        ChordPanel.Children.Clear();
        foreach (var chord in ChordType.All)
        {
            var btn = MakeChoiceButton(chord.KorName);
            btn.Tag = chord;
            btn.Click += ChordChoice_Click;
            btn.Width = 110; btn.Height = 50;
            btn.Margin = new Thickness(4);
            ChordPanel.Children.Add(btn);
        }
    }

    void NewChordQuestion()
    {
        _chordAnswered = false;
        _currentChord = ChordType.All[_rng.Next(ChordType.All.Length)];
        _currentChordRoot = Notes[_rng.Next(Notes.Length / 2)];
        ChordResultBorder.Visibility = Visibility.Collapsed;
        BtnNextChord.Visibility = Visibility.Collapsed;
        ResetChoiceButtons(ChordPanel);
        StatusBar.Text = "▶ 재생을 눌러 화음을 들으세요";
    }

    void BtnPlayChord_Click(object sender, RoutedEventArgs e)
    {
        if (_currentChord == null || _currentChordRoot == null) return;
        var freqs = _currentChord.Semitones
            .Select(s => Notes.FirstOrDefault(n => n.MidiNote == _currentChordRoot.MidiNote + s))
            .Where(n => n != null)
            .Select(n => n!.Frequency);
        _audio.PlayChord(freqs, 2.0);
    }

    void ChordChoice_Click(object sender, RoutedEventArgs e)
    {
        if (_chordAnswered || _currentChord == null) return;
        _chordAnswered = true;
        var btn = (Button)sender;
        var chosen = (ChordType)btn.Tag!;
        bool correct = chosen.Name == _currentChord.Name;

        HighlightChoice(ChordPanel, _currentChord.Name, chosen.Name, i => ((ChordType)i).Name);
        ChordResultBorder.Visibility = Visibility.Visible;
        ChordResultBorder.Background = new SolidColorBrush(correct ? Color.FromRgb(0x1A, 0x2A, 0x1A) : Color.FromRgb(0x2A, 0x1A, 0x1A));
        ChordResultText.Foreground = new SolidColorBrush(correct ? Color.FromRgb(0x66, 0xBB, 0x6A) : Color.FromRgb(0xEF, 0x53, 0x50));
        ChordResultText.Text = correct ? $"✅ 정답! {_currentChord.KorName}" : $"❌ 오답. 정답은 {_currentChord.KorName}";
        BtnNextChord.Visibility = Visibility.Visible;
        UpdateElo("chord_" + _currentChord.Name, correct);
        UpdateSession(correct);
        _db.SaveResult(new QuizResult(TrainMode.Chord, _currentChord.Name, chosen.Name, correct, DateTime.Now));
    }

    void BtnNextChord_Click(object sender, RoutedEventArgs e) => NewChordQuestion();

    // ─── 탭 전환 ─────────────────────────────────────────────────────────
    void MainTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        switch (MainTab.SelectedIndex)
        {
            case 0: NewNoteQuestion(); break;
            case 1: NewIntervalQuestion(); break;
            case 2: NewChordQuestion(); break;
            case 3: RefreshStats(); break;
        }
    }

    // ─── ELO 업데이트 ────────────────────────────────────────────────────
    void UpdateElo(string key, bool correct)
    {
        var r = _db.GetElo(key);
        double k = r.Total < 20 ? 32 : 16;
        double expected = 1.0 / (1.0 + Math.Pow(10, (1200 - r.Elo) / 400.0));
        r.Elo += k * ((correct ? 1 : 0) - expected);
        r.Total++;
        if (correct) r.Correct++;
        _db.SaveElo(r);
    }

    // ─── 세션 통계 ────────────────────────────────────────────────────────
    void UpdateSession(bool correct)
    {
        _sessionTotal++;
        if (correct) _sessionCorrect++;
        SessionStats.Text = $"정답률: {_sessionCorrect}/{_sessionTotal} ({(double)_sessionCorrect / _sessionTotal * 100:F0}%)";
    }

    void RefreshStats()
    {
        EloList.ItemsSource = _db.GetAllElo();
    }

    // ─── UI 헬퍼 ─────────────────────────────────────────────────────────
    Button MakeChoiceButton(string text)
    {
        return new Button
        {
            Content = new TextBlock { Text = text, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap },
            Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
            BorderThickness = new Thickness(1),
        };
    }

    void ResetChoiceButtons(Panel panel)
    {
        foreach (var child in panel.Children.OfType<Button>())
        {
            child.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E));
            child.BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
        }
    }

    void HighlightChoice<T>(Panel panel, T correctKey, T chosenKey, Func<object, T> keySelector)
    {
        foreach (var child in panel.Children.OfType<Button>())
        {
            var k = keySelector(child.Tag!);
            if (EqualityComparer<T>.Default.Equals(k, correctKey))
                child.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x1A));
            else if (EqualityComparer<T>.Default.Equals(k, chosenKey))
                child.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x1A, 0x1A));
        }
    }

    void BtnStats_Click(object sender, RoutedEventArgs e)
    {
        MainTab.SelectedIndex = 3;
        RefreshStats();
    }

    void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        _sessionCorrect = 0;
        _sessionTotal = 0;
        SessionStats.Text = "정답률: —";
    }
}
