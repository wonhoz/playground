using System.Windows.Controls;
using System.Windows.Media;

namespace Dict.Cast;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    const int  HotkeyId  = 0x44;   // Win+Shift+D
    const uint MOD_WIN   = 0x0008;
    const uint MOD_SHIFT = 0x0004;
    const uint VK_D      = 0x44;

    static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Dict.Cast");

    readonly MainViewModel     _vm;
    readonly AppDatabase       _db;
    readonly TtsService        _tts;
    readonly DictionaryService _dict;
    readonly TranslationService _translation;

    CancellationTokenSource? _searchCts;
    CancellationTokenSource? _buildCts;
    CancellationTokenSource? _translateCts;

    public MainWindow()
    {
        _db          = new AppDatabase(Path.Combine(AppDataDir, "user.db"));
        _dict        = new DictionaryService(Path.Combine(AppDataDir, "dict.db"));
        _tts         = new TtsService();
        _translation = new TranslationService();
        _vm          = new MainViewModel(_dict, _db, _translation);
        DataContext = _vm;
        InitializeComponent();

        Loaded  += OnLoaded;
        Closing += OnClosing;
    }

    // ── 초기화 ────────────────────────────────────────────────────────────

    async void OnLoaded(object s, RoutedEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        RegisterHotKey(handle, HotkeyId, MOD_WIN | MOD_SHIFT, VK_D);
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);

        _vm.RefreshHistory();

        if (!_dict.IsReady)
            await StartBuildAsync();
    }

    async Task StartBuildAsync()
    {
        _vm.IsBuilding    = true;
        _vm.BuildProgress = 0;
        _buildCts = new CancellationTokenSource();

        var progress = new Progress<(string Message, int Percent)>(t =>
        {
            _vm.BuildMessage  = t.Message;
            _vm.BuildProgress = t.Percent;
            UpdateProgressBar(t.Percent);
        });

        var builder = new WordNetBuilder(Path.Combine(AppDataDir, "dict.db"));
        try
        {
            await builder.BuildAsync(progress, _buildCts.Token);
            _vm.IsBuilding   = false;
            _vm.StatusText   = "";
            TxtSearch.Focus();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _vm.IsBuilding  = false;
            _vm.StatusText  = $"사전 빌드 실패: {ex.Message}";
        }
    }

    void UpdateProgressBar(int percent)
    {
        if (!IsLoaded) return;
        // ProgressFill 너비를 부모 너비 * percent / 100으로 설정
        if (FindName("ProgressFill") is Border fill &&
            fill.Parent is Grid grid)
        {
            fill.Width = Math.Max(0, grid.ActualWidth * percent / 100.0);
        }
    }

    // ── 전역 단축키 ──────────────────────────────────────────────────────

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            if (Visibility == Visibility.Visible && IsActive)
                Hide();
            else
            {
                Show();
                Activate();
                // 클립보드에 텍스트가 있으면 자동으로 검색창에 채우기
                try
                {
                    var clip = Clipboard.GetText().Trim();
                    if (!string.IsNullOrEmpty(clip) && clip.Length <= 50 && !clip.Contains('\n'))
                    {
                        TxtSearch.Text = clip;
                        TxtSearch.SelectAll();
                        DoSearch(clip);
                    }
                    else
                    {
                        TxtSearch.Clear();
                        _vm.ClearResult();
                    }
                }
                catch { }
                TxtSearch.Focus();
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    void OnClosing(object? s, System.ComponentModel.CancelEventArgs e)
    {
        _buildCts?.Cancel();
        _translateCts?.Cancel();
        var handle = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(handle, HotkeyId);
        _tts.Dispose();
        _translation.Dispose();
        _db.Dispose();
    }

    // ── 검색 ─────────────────────────────────────────────────────────────

    void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var text = TxtSearch.Text;

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        if (string.IsNullOrWhiteSpace(text))
        {
            _vm.ClearResult();
            HideSuggestions();
            return;
        }

        // 디바운스 300ms 후 제안 업데이트
        _ = Task.Delay(200, token).ContinueWith(_ =>
        {
            if (token.IsCancellationRequested) return;
            Dispatcher.Invoke(() =>
            {
                if (token.IsCancellationRequested) return;
                _vm.UpdateSuggestions(TxtSearch.Text);
                ShowSuggestions(_vm.Suggestions.Count > 0);
            });
        }, token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (!IsLoaded) return;

        if (e.Key == Key.Enter)
        {
            HideSuggestions();
            DoSearch(TxtSearch.Text);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && _vm.Suggestions.Count > 0)
        {
            ShowSuggestions(true);
            LstSuggestions.Focus();
            LstSuggestions.SelectedIndex = 0;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HideSuggestions();
            if (string.IsNullOrEmpty(TxtSearch.Text))
                Hide();
            e.Handled = true;
        }
    }

    void DoSearch(string word)
    {
        if (string.IsNullOrWhiteSpace(word) || _vm.IsBuilding) return;
        word = word.Trim();
        TxtSearch.Text = word;
        _vm.Search(word);

        // 이전 번역 취소 후 새 번역 시작
        _translateCts?.Cancel();
        _translateCts = new CancellationTokenSource();
        _ = _vm.TranslateCurrentSensesAsync(_translateCts.Token);
    }

    // ── 제안 드롭다운 ─────────────────────────────────────────────────────

    void ShowSuggestions(bool show)
    {
        SuggestionPanel.Visibility = (show && _vm.Suggestions.Count > 0)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    void HideSuggestions() => SuggestionPanel.Visibility = Visibility.Collapsed;

    void Suggestion_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 키보드로 선택 이동만 — 아직 확정 아님
    }

    void Suggestion_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        CommitSuggestion();
    }

    void Suggestion_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)   { CommitSuggestion(); e.Handled = true; }
        if (e.Key == Key.Escape)  { HideSuggestions(); TxtSearch.Focus(); e.Handled = true; }
    }

    void CommitSuggestion()
    {
        if (LstSuggestions.SelectedItem is string word)
        {
            HideSuggestions();
            TxtSearch.Text = word;
            DoSearch(word);
            TxtSearch.Focus();
        }
    }

    // ── 버튼 핸들러 ──────────────────────────────────────────────────────

    void Speak_Click(object sender, RoutedEventArgs e)
    {
        var word = !string.IsNullOrEmpty(_vm.CurrentWord)
            ? _vm.CurrentWord
            : TxtSearch.Text.Trim();
        if (!string.IsNullOrEmpty(word))
            _tts.Speak(word);
    }

    void Fav_Click(object sender, RoutedEventArgs e)
    {
        _vm.ToggleWordlist();
    }

    void WordlistMenu_Click(object sender, RoutedEventArgs e)
    {
        CtxWordlist.PlacementTarget = (Button)sender;
        CtxWordlist.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        CtxWordlist.IsOpen = true;
    }

    void ExportCsv_Click(object sender, RoutedEventArgs e)   => _vm.ExportWordlist(anki: false);
    void ExportAnki_Click(object sender, RoutedEventArgs e)  => _vm.ExportWordlist(anki: true);

    void History_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string word)
        {
            TxtSearch.Text = word;
            DoSearch(word);
        }
    }
}
