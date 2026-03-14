using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace BugHunt;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    // ─── 게임 상태 ────────────────────────────────────────────────────────
    Puzzle[] _puzzles = [];
    int _currentIdx = 0;
    int _score = 0;
    int _timeLeft = 30;
    DispatcherTimer _timer = new();
    bool _answered = false;
    HashSet<int> _clickedBugLines = [];

    // 구문 강조 색상 (간소화)
    static readonly Brush BrushKeyword = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6));
    static readonly Brush BrushString = new SolidColorBrush(Color.FromRgb(0xCE, 0x91, 0x78));
    static readonly Brush BrushComment = new SolidColorBrush(Color.FromRgb(0x6A, 0x99, 0x55));
    static readonly Brush BrushNumber = new SolidColorBrush(Color.FromRgb(0xB5, 0xCE, 0xA8));
    static readonly Brush BrushDefault = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
    static readonly Brush BrushBugHighlight = new SolidColorBrush(Color.FromArgb(80, 0xEF, 0x53, 0x50));
    static readonly Brush BrushCorrectHighlight = new SolidColorBrush(Color.FromArgb(80, 0x00, 0xAA, 0x44));
    static readonly Brush BrushHover = new SolidColorBrush(Color.FromArgb(30, 0xFF, 0xFF, 0xFF));

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer_Tick;

        LoadPuzzles();
        ShowPuzzle();
    }

    void LangCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        LoadPuzzles();
        ShowPuzzle();
    }

    void LoadPuzzles()
    {
        string lang = (LangCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "C#";
        string diff = (DiffCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "주니어";
        _puzzles = PuzzleDb.Filter(lang, diff);
        _currentIdx = 0;
        _score = 0;
        UpdateScore();
    }

    void ShowPuzzle()
    {
        _timer.Stop();
        _answered = false;
        _clickedBugLines.Clear();
        _timeLeft = 30;
        TimerLabel.Text = "30";
        TimerLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
        ResultLabel.Text = "";

        if (_puzzles.Length == 0)
        {
            StatusBar.Text = "선택한 언어/난이도에 문제가 없습니다.";
            CodeLines.ItemsSource = null;
            return;
        }

        var puzzle = _puzzles[_currentIdx % _puzzles.Length];
        QuestionDesc.Text = puzzle.Description;
        QuestionLabel.Text = $"{(_currentIdx % _puzzles.Length) + 1}/{_puzzles.Length}";

        var lines = new List<CodeLine>();
        for (int i = 0; i < puzzle.Lines.Length; i++)
        {
            lines.Add(new CodeLine
            {
                LineNumber = i + 1,
                Code = puzzle.Lines[i],
                ForeColor = GetLineColor(puzzle.Lines[i], puzzle.Language),
                BackColor = Brushes.Transparent,
                IsBug = puzzle.BugLines.Contains(i + 1)
            });
        }
        CodeLines.ItemsSource = lines;
        StatusBar.Text = "버그 있는 줄을 클릭하세요! (줄이 여러 개일 수 있습니다)";

        _timer.Start();
    }

    void Line_Click(object sender, MouseButtonEventArgs e)
    {
        if (_answered) return;
        if (sender is not FrameworkElement el || el.DataContext is not CodeLine line) return;

        var puzzle = _puzzles[_currentIdx % _puzzles.Length];
        bool isBug = puzzle.BugLines.Contains(line.LineNumber);

        if (isBug)
        {
            _clickedBugLines.Add(line.LineNumber);
            line.BackColor = BrushCorrectHighlight;
            line.ForeColor = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88));

            if (_clickedBugLines.Count >= puzzle.BugLines.Length)
            {
                AnswerCorrect();
            }
            else
            {
                StatusBar.Text = $"✅ 버그 줄 발견! ({_clickedBugLines.Count}/{puzzle.BugLines.Length}) 더 있습니다.";
            }
        }
        else
        {
            line.BackColor = BrushBugHighlight;
            _score = Math.Max(0, _score - 5);
            UpdateScore();
            StatusBar.Text = $"❌ 줄 {line.LineNumber}은 버그가 아닙니다. -5점";
        }

        // 강제 UI 갱신
        CodeLines.ItemsSource = null;
        CodeLines.ItemsSource = ((List<CodeLine>)CodeLines.Tag ?? GetCurrentLines());
        RefreshLines();
    }

    void RefreshLines()
    {
        var items = (CodeLines.ItemsSource as List<CodeLine>)?.ToList();
        CodeLines.ItemsSource = null;
        CodeLines.ItemsSource = items;
    }

    List<CodeLine> GetCurrentLines() => [];

    void AnswerCorrect()
    {
        _timer.Stop();
        _answered = true;
        int bonus = _timeLeft * 3;
        _score += 100 + bonus;
        UpdateScore();
        ResultLabel.Text = $"✅ 정답! +{100 + bonus}점 (시간 보너스: {bonus})";
        StatusBar.Text = $"💡 해설: {_puzzles[_currentIdx % _puzzles.Length].Explanation}";
    }

    void Timer_Tick(object? sender, EventArgs e)
    {
        _timeLeft--;
        TimerLabel.Text = _timeLeft.ToString();
        if (_timeLeft <= 10)
            TimerLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x22));
        if (_timeLeft <= 0)
        {
            _timer.Stop();
            _answered = true;
            ResultLabel.Text = "⏰ 시간 초과!";
            var puzzle = _puzzles[_currentIdx % _puzzles.Length];
            StatusBar.Text = $"정답 줄: {string.Join(", ", puzzle.BugLines)} | {puzzle.Explanation}";
            ShowBugLines(puzzle);
        }
    }

    void ShowBugLines(Puzzle puzzle)
    {
        var items = (CodeLines.ItemsSource as List<CodeLine>)?.ToList();
        if (items == null) return;
        foreach (var cl in items)
            if (puzzle.BugLines.Contains(cl.LineNumber))
                cl.BackColor = BrushBugHighlight;
        CodeLines.ItemsSource = null;
        CodeLines.ItemsSource = items;
    }

    void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        LoadPuzzles();
        ShowPuzzle();
    }

    void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        _currentIdx++;
        ShowPuzzle();
    }

    void UpdateScore() => ScoreLabel.Text = _score.ToString();

    // ─── 간단한 구문 강조 ─────────────────────────────────────────────────
    static readonly string[] CsharpKeywords = ["public", "private", "static", "int", "long", "string", "bool",
        "var", "return", "if", "else", "for", "foreach", "in", "new", "class", "void",
        "async", "await", "using", "double", "null", "true", "false", "readonly"];
    static readonly string[] PythonKeywords = ["def", "return", "if", "else", "for", "in", "None", "True", "False", "not", "and", "or", "lambda"];
    static readonly string[] JsKeywords = ["function", "const", "let", "var", "return", "if", "else", "async", "await", "true", "false", "null"];
    static readonly string[] JavaKeywords = ["public", "private", "static", "int", "long", "String", "boolean",
        "return", "if", "else", "for", "new", "class", "void", "null", "true", "false"];

    static Brush GetLineColor(string line, string lang)
    {
        string trimmed = line.TrimStart();

        // 주석
        if (lang == "C#" || lang == "Java")
            if (trimmed.StartsWith("//")) return BrushComment;
        if (lang == "Python")
            if (trimmed.StartsWith("#")) return BrushComment;
        if (lang == "JavaScript")
            if (trimmed.StartsWith("//")) return BrushComment;

        // 문자열 포함 줄
        if (line.Contains('"') || line.Contains('\'')) return BrushString;

        // 키워드 시작 줄
        string[] kw = lang switch
        {
            "Python" => PythonKeywords,
            "JavaScript" => JsKeywords,
            "Java" => JavaKeywords,
            _ => CsharpKeywords
        };
        string first = trimmed.Split(' ')[0].TrimEnd('(');
        if (kw.Contains(first)) return BrushKeyword;

        // 숫자 포함
        if (trimmed.Length > 0 && char.IsDigit(trimmed[0])) return BrushNumber;

        return BrushDefault;
    }
}
