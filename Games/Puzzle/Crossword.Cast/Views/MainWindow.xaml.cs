using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
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
    private DispatcherTimer? _timer;
    private int _elapsedSeconds;

    public MainWindow()
    {
        InitializeComponent();

        _game = new GameService();
        _grid = new CrosswordGrid { Game = _game };

        _grid.Changed += OnGridChanged;
        GridBorder.Child = _grid;

        Loaded += async (_, _) => { ApplyDarkTitleBar(); await NewPuzzleAsync(); };
    }

    // ── 새 퍼즐 ─────────────────────────────────────────────────────

    private async Task NewPuzzleAsync()
    {
        TxtStatus.Text = "퍼즐 생성 중...";
        IsEnabled = false;

        await Task.Run(() => _game.NewPuzzle());

        IsEnabled = true;
        _grid.ResetSelection();
        RefreshClues();
        StartTimer();
        UpdateStatus();
    }

    private void RefreshClues()
    {
        if (_game.CurrentPuzzle is null) return;
        LstAcross.ItemsSource = _game.CurrentPuzzle.AcrossWords.Select(w => w.ClueText).ToList();
        LstDown.ItemsSource   = _game.CurrentPuzzle.DownWords.Select(w => w.ClueText).ToList();
    }

    // ── 타이머 ───────────────────────────────────────────────────────

    private void StartTimer()
    {
        _timer?.Stop();
        _elapsedSeconds = 0;
        TxtTimer.Text = "00:00";
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _elapsedSeconds++;
            TxtTimer.Text = $"{_elapsedSeconds / 60:D2}:{_elapsedSeconds % 60:D2}";
        };
        _timer.Start();
    }

    private void StopTimer() => _timer?.Stop();

    // ── 상태 업데이트 ────────────────────────────────────────────────

    private void UpdateStatus()
    {
        var (done, total) = _game.WordProgress();
        TxtStatus.Text = $"단어 {done}/{total} 완성 · 셀 클릭 후 알파벳 입력  Tab: 방향 전환 / 백스페이스: 지우기";
    }

    // ── 게임 이벤트 ──────────────────────────────────────────────────

    private void OnGridChanged()
    {
        if (_game.IsCompleted())
        {
            StopTimer();
            TxtStatus.Text = $"🎉 완성! 정답입니다! ({TxtTimer.Text})";
            return;
        }

        var (done, total) = _game.WordProgress();
        TxtStatus.Text = $"단어 {done}/{total} 완성 · 셀 클릭 후 알파벳 입력  Tab: 방향 전환 / 백스페이스: 지우기";

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

    private async void OnNew(object sender, RoutedEventArgs e)    => await NewPuzzleAsync();

    private void OnReveal(object sender, RoutedEventArgs e)
    {
        var word = _grid.SelectedWord;
        if (word is null) return;
        _game.RevealWord(word);
        _grid.Refresh();
        OnGridChanged();
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        _game.Clear();
        _grid.Refresh();
        var (_, total) = _game.WordProgress();
        TxtStatus.Text = $"단어 0/{total} 완성 · 초기화했습니다.";
    }

    private void OnCheck(object sender, RoutedEventArgs e)
    {
        if (_game.CurrentPuzzle is null) return;
        var uniqueCells = new HashSet<(int, int)>();
        foreach (var w in _game.CurrentPuzzle.Words)
        {
            int dr = w.Across ? 0 : 1, dc = w.Across ? 1 : 0;
            for (int i = 0; i < w.Word.Length; i++)
                uniqueCells.Add((w.Row + dr * i, w.Col + dc * i));
        }
        int total   = uniqueCells.Count;
        int correct = uniqueCells.Count(pos => _game.IsCorrect(pos.Item1, pos.Item2));

        TxtStatus.Text = _game.IsCompleted()
            ? $"🎉 완성! 정답입니다! ({TxtTimer.Text})"
            : $"진행률: {correct} / {total} 칸 ({correct * 100 / total}%)";

        _grid.Refresh();
    }

    private void OnCopySeed(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_game.CurrentSeed.ToString());
        TxtStatus.Text = $"씨드 복사됨: {_game.CurrentSeed}  (다음 게임에 같은 씨드로 재도전 가능)";
    }

    // ── 다크 타이틀바 ────────────────────────────────────────────────

    private void ApplyDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));
    }
}
