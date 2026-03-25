using System.Runtime.InteropServices;
using System.Windows.Interop;
using CrosswordCast.Models;
using CrosswordCast.Services;

namespace CrosswordCast.Views;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int val, int sz);

    private readonly GameService    _game;
    private readonly CrosswordGrid  _grid;
    private bool _updatingClue;

    public MainWindow()
    {
        InitializeComponent();

        _game = new GameService();
        _grid = new CrosswordGrid { Game = _game };

        _grid.Changed += OnGridChanged;
        GridBorder.Child = _grid;

        Loaded += (_, _) => { ApplyDarkTitleBar(); NewPuzzle(); };
    }

    // ── 새 퍼즐 ─────────────────────────────────────────────────────

    private void NewPuzzle()
    {
        TxtStatus.Text = "퍼즐 생성 중...";
        _game.NewPuzzle();
        _grid.ResetSelection();
        RefreshClues();
        TxtStatus.Text = "셀을 클릭한 후 알파벳을 입력하세요.  Tab: 방향 전환 / 백스페이스: 지우기";
    }

    private void RefreshClues()
    {
        if (_game.CurrentPuzzle is null) return;
        LstAcross.ItemsSource = _game.CurrentPuzzle.AcrossWords.Select(w => w.ClueText).ToList();
        LstDown.ItemsSource   = _game.CurrentPuzzle.DownWords.Select(w => w.ClueText).ToList();
    }

    // ── 게임 이벤트 ──────────────────────────────────────────────────

    private void OnGridChanged()
    {
        if (_game.IsCompleted())
        {
            TxtStatus.Text = "🎉 완성! 정답입니다!";
            return;
        }

        // 선택된 단어 힌트 하이라이트
        var sel = _grid.SelectedWord;
        if (sel is null) return;

        _updatingClue = true;
        if (sel.Across)
        {
            var idx = _game.CurrentPuzzle!.AcrossWords
                .Select((w, i) => (w, i)).FirstOrDefault(x => x.w == sel).i;
            LstAcross.SelectedIndex = idx;
            LstAcross.ScrollIntoView(LstAcross.SelectedItem);
            LstDown.SelectedIndex   = -1;
        }
        else
        {
            var idx = _game.CurrentPuzzle!.DownWords
                .Select((w, i) => (w, i)).FirstOrDefault(x => x.w == sel).i;
            LstDown.SelectedIndex = idx;
            LstDown.ScrollIntoView(LstDown.SelectedItem);
            LstAcross.SelectedIndex = -1;
        }
        _updatingClue = false;
    }

    // ── 힌트 클릭 → 격자 이동 ────────────────────────────────────────

    private void OnClueSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingClue || _game.CurrentPuzzle is null) return;
        var lb   = (ListBox)sender;
        int idx  = lb.SelectedIndex;
        if (idx < 0) return;

        bool across  = lb == LstAcross;
        var words    = across
            ? _game.CurrentPuzzle.AcrossWords.ToList()
            : _game.CurrentPuzzle.DownWords.ToList();
        if (idx >= words.Count) return;

        _grid.SelectWord(words[idx]);
        _grid.Focus();
    }

    // ── 버튼 ─────────────────────────────────────────────────────────

    private void OnNew(object sender, RoutedEventArgs e)   => NewPuzzle();

    private void OnClear(object sender, RoutedEventArgs e)
    {
        _game.Clear();
        _grid.Refresh();
        TxtStatus.Text = "초기화했습니다.";
    }

    private void OnCheck(object sender, RoutedEventArgs e)
    {
        if (_game.CurrentPuzzle is null) return;
        int total   = _game.CurrentPuzzle.Words.Sum(w => w.Word.Length);
        int correct = 0;
        for (int r = 0; r < Puzzle.N; r++)
        for (int c = 0; c < Puzzle.N; c++)
            if (_game.IsCorrect(r, c)) correct++;

        // 중복 셀(교차점) 보정
        var uniqueCells = new HashSet<(int, int)>();
        foreach (var w in _game.CurrentPuzzle.Words)
        {
            int dr = w.Across ? 0 : 1, dc = w.Across ? 1 : 0;
            for (int i = 0; i < w.Word.Length; i++)
                uniqueCells.Add((w.Row + dr * i, w.Col + dc * i));
        }
        total = uniqueCells.Count;
        correct = uniqueCells.Count(pos => _game.IsCorrect(pos.Item1, pos.Item2));

        TxtStatus.Text = _game.IsCompleted()
            ? "🎉 완성! 정답입니다!"
            : $"진행률: {correct} / {total} 칸 ({correct * 100 / total}%)";

        _grid.Refresh();
    }

    // ── 다크 타이틀바 ────────────────────────────────────────────────

    private void ApplyDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));
    }
}
