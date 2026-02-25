using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using LogLens.Models;
using LogLens.Services;
using Microsoft.Win32;

namespace LogLens;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private readonly Dictionary<TabItem, LogTabState> _tabStates = [];

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        SetupFilterPlaceholder();
    }

    // ── 파일 열기 ──────────────────────────────────

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "로그 파일|*.log;*.txt;*.csv;*.json;*.xml|모든 파일|*.*",
            Multiselect = true,
            Title = "로그 파일 선택"
        };
        if (dlg.ShowDialog() == true)
            foreach (var f in dlg.FileNames)
                OpenLogFile(f);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "로그 폴더 선택" };
        if (dlg.ShowDialog() == true)
        {
            var files = Directory.GetFiles(dlg.FolderName)
                .Where(f => IsTextFile(f))
                .OrderBy(f => f)
                .Take(20);
            foreach (var f in files)
                OpenLogFile(f);
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            foreach (var f in files)
                if (File.Exists(f))
                    OpenLogFile(f);
    }

    private void OpenLogFile(string path)
    {
        // 이미 열려 있으면 해당 탭으로 이동
        foreach (var (tab, state) in _tabStates)
        {
            if (string.Equals(state.FilePath, path, StringComparison.OrdinalIgnoreCase))
            {
                TabLogs.SelectedItem = tab;
                return;
            }
        }

        var tailer = new LogTailer(path);
        var entries = tailer.ReadAll();

        var rtb = CreateRichTextBox();
        var tabState = new LogTabState
        {
            FilePath = path,
            Tailer = tailer,
            RichTextBox = rtb,
            AllEntries = new ObservableCollection<LogEntry>(entries)
        };

        var tabItem = new TabItem
        {
            Header = Path.GetFileName(path),
            Content = rtb,
            ToolTip = path,
            Tag = path
        };

        _tabStates[tabItem] = tabState;
        TabLogs.Items.Add(tabItem);
        TabLogs.SelectedItem = tabItem;

        RenderEntries(tabState);
        if (ChkAutoScroll.IsChecked == true) ScrollToEnd(rtb);

        tailer.NewLines += newLines => Dispatcher.BeginInvoke(() =>
        {
            foreach (var entry in newLines)
                tabState.AllEntries.Add(entry);
            RenderEntries(tabState);
            if (ChkAutoScroll.IsChecked == true) ScrollToEnd(tabState.RichTextBox);
            UpdateStatusBar();
        });

        EmptyState.Visibility = Visibility.Collapsed;
        UpdateStatusBar();
    }

    // ── 탭 관리 ──────────────────────────────────

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        // 탭 아이템 찾기
        var tabItem = FindParentTabItem(btn);
        if (tabItem == null) return;

        if (_tabStates.TryGetValue(tabItem, out var state))
        {
            state.Tailer.Dispose();
            _tabStates.Remove(tabItem);
        }

        TabLogs.Items.Remove(tabItem);

        if (TabLogs.Items.Count == 0)
            EmptyState.Visibility = Visibility.Visible;

        UpdateStatusBar();
    }

    private static TabItem? FindParentTabItem(DependencyObject child)
    {
        var current = child;
        while (current != null)
        {
            if (current is TabItem tab) return tab;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void TabLogs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStatusBar();
    }

    // ── 필터링 ──────────────────────────────────

    private void Filter_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void LevelFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (!IsLoaded) return;
        if (TabLogs.SelectedItem is not TabItem tab || !_tabStates.TryGetValue(tab, out var state)) return;
        RenderEntries(state);
        UpdateStatusBar();
    }

    private List<LogEntry> GetFilteredEntries(LogTabState state)
    {
        var filterText = GetFilterText();
        var isRegex = ChkRegex.IsChecked == true;
        var minLevel = GetMinLevel();

        Regex? filterRegex = null;
        if (isRegex && !string.IsNullOrEmpty(filterText))
        {
            try { filterRegex = new Regex(filterText, RegexOptions.IgnoreCase); }
            catch { return []; }
        }

        return state.AllEntries.Where(entry =>
        {
            if (minLevel != LogLevel.None && entry.Level != LogLevel.None && entry.Level < minLevel)
                return false;

            if (!string.IsNullOrEmpty(filterText))
            {
                if (filterRegex != null)
                    return filterRegex.IsMatch(entry.Text);
                return entry.Text.Contains(filterText, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }).ToList();
    }

    private string GetFilterText()
    {
        if (TxtFilter.Tag is string placeholder && TxtFilter.Text == placeholder) return "";
        return TxtFilter.Text;
    }

    private LogLevel GetMinLevel()
    {
        return CmbLevel.SelectedIndex switch
        {
            1 => LogLevel.Trace,
            2 => LogLevel.Debug,
            3 => LogLevel.Info,
            4 => LogLevel.Warn,
            5 => LogLevel.Error,
            6 => LogLevel.Fatal,
            _ => LogLevel.None
        };
    }

    // ── 렌더링 ──────────────────────────────────

    private void RenderEntries(LogTabState state)
    {
        var rtb = state.RichTextBox;
        var filtered = GetFilteredEntries(state);
        var filterText = GetFilterText();
        var isRegex = ChkRegex.IsChecked == true;

        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
            FontSize = 12.5,
            Background = new SolidColorBrush(Color.FromRgb(15, 15, 26)),
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 210)),
            PagePadding = new Thickness(0)
        };

        var para = new Paragraph { Margin = new Thickness(0), LineHeight = 20 };

        foreach (var entry in filtered)
        {
            if (para.Inlines.Count > 0)
                para.Inlines.Add(new LineBreak());

            // 라인 번호
            var lineNum = new Run($"{entry.LineNumber,6}  ") { Foreground = Brushes.Gray };
            para.Inlines.Add(lineNum);

            // 레벨 컬러
            var levelBrush = GetLevelBrush(entry.Level);

            // 하이라이트 적용
            if (!string.IsNullOrEmpty(filterText) && !isRegex)
            {
                AddHighlightedText(para, entry.Text, filterText, levelBrush);
            }
            else if (!string.IsNullOrEmpty(filterText) && isRegex)
            {
                AddRegexHighlightedText(para, entry.Text, filterText, levelBrush);
            }
            else
            {
                AddColoredLogLine(para, entry.Text, entry.Level, levelBrush);
            }
        }

        doc.Blocks.Add(para);
        rtb.Document = doc;

        // 필터 정보 업데이트
        TxtFilterInfo.Text = state.AllEntries.Count != filtered.Count
            ? $"필터: {filtered.Count}/{state.AllEntries.Count}"
            : "";
    }

    private static void AddColoredLogLine(Paragraph para, string text, LogLevel level, Brush brush)
    {
        // ERROR/WARN 키워드 부분 하이라이트
        if (level >= LogLevel.Warn)
        {
            para.Inlines.Add(new Run(text) { Foreground = brush });
        }
        else
        {
            para.Inlines.Add(new Run(text) { Foreground = brush });
        }
    }

    private static void AddHighlightedText(Paragraph para, string text, string keyword, Brush baseBrush)
    {
        var idx = 0;
        while (idx < text.Length)
        {
            var pos = text.IndexOf(keyword, idx, StringComparison.OrdinalIgnoreCase);
            if (pos < 0)
            {
                para.Inlines.Add(new Run(text[idx..]) { Foreground = baseBrush });
                break;
            }

            if (pos > idx)
                para.Inlines.Add(new Run(text[idx..pos]) { Foreground = baseBrush });

            para.Inlines.Add(new Run(text[pos..(pos + keyword.Length)])
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 255, 200, 0)),
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold
            });

            idx = pos + keyword.Length;
        }
    }

    private static void AddRegexHighlightedText(Paragraph para, string text, string pattern, Brush baseBrush)
    {
        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var matches = regex.Matches(text);
            var idx = 0;

            foreach (Match match in matches)
            {
                if (match.Index > idx)
                    para.Inlines.Add(new Run(text[idx..match.Index]) { Foreground = baseBrush });

                para.Inlines.Add(new Run(match.Value)
                {
                    Background = new SolidColorBrush(Color.FromArgb(180, 255, 200, 0)),
                    Foreground = Brushes.Black,
                    FontWeight = FontWeights.Bold
                });

                idx = match.Index + match.Length;
            }

            if (idx < text.Length)
                para.Inlines.Add(new Run(text[idx..]) { Foreground = baseBrush });
        }
        catch
        {
            para.Inlines.Add(new Run(text) { Foreground = baseBrush });
        }
    }

    private static Brush GetLevelBrush(LogLevel level) => level switch
    {
        LogLevel.Fatal => new SolidColorBrush(Color.FromRgb(255, 50, 50)),
        LogLevel.Error => new SolidColorBrush(Color.FromRgb(255, 90, 90)),
        LogLevel.Warn => new SolidColorBrush(Color.FromRgb(255, 210, 60)),
        LogLevel.Info => new SolidColorBrush(Color.FromRgb(80, 220, 120)),
        LogLevel.Debug => new SolidColorBrush(Color.FromRgb(130, 170, 255)),
        LogLevel.Trace => new SolidColorBrush(Color.FromRgb(120, 120, 140)),
        _ => new SolidColorBrush(Color.FromRgb(200, 200, 210))
    };

    private RichTextBox CreateRichTextBox()
    {
        var rtb = new RichTextBox
        {
            IsReadOnly = true,
            Background = new SolidColorBrush(Color.FromRgb(15, 15, 26)),
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 210)),
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
            FontSize = 12.5,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ChkWordWrap.IsChecked == true
                ? ScrollBarVisibility.Disabled
                : ScrollBarVisibility.Auto
        };

        if (ChkWordWrap.IsChecked == true)
        {
            rtb.Document.PageWidth = double.NaN;
        }

        return rtb;
    }

    private static void ScrollToEnd(RichTextBox rtb)
    {
        rtb.ScrollToEnd();
    }

    // ── 줄 바꿈 토글 ──────────────────────────────

    private void WordWrap_Changed(object sender, RoutedEventArgs e)
    {
        foreach (var (_, state) in _tabStates)
        {
            state.RichTextBox.HorizontalScrollBarVisibility = ChkWordWrap.IsChecked == true
                ? ScrollBarVisibility.Disabled
                : ScrollBarVisibility.Auto;
        }
    }

    // ── 상태 바 업데이트 ──────────────────────────

    private void UpdateStatusBar()
    {
        if (TabLogs.SelectedItem is TabItem tab && _tabStates.TryGetValue(tab, out var state))
        {
            TxtStatus.Text = $"📂 {state.FilePath}";
            TxtLineInfo.Text = $"전체: {state.AllEntries.Count:N0} 라인";
        }
        else
        {
            TxtStatus.Text = "준비";
            TxtLineInfo.Text = "";
            TxtFilterInfo.Text = "";
        }
    }

    // ── 플레이스홀더 ──────────────────────────────

    private void SetupFilterPlaceholder()
    {
        TxtFilter.Text = TxtFilter.Tag as string ?? "";
        TxtFilter.Foreground = new SolidColorBrush(Color.FromRgb(96, 96, 112));
    }

    private void FilterBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (TxtFilter.Text == (TxtFilter.Tag as string ?? ""))
        {
            TxtFilter.Text = "";
            TxtFilter.Foreground = new SolidColorBrush(Color.FromRgb(224, 224, 224));
        }
    }

    private void FilterBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtFilter.Text))
        {
            SetupFilterPlaceholder();
        }
    }

    // ── 단축키 ──────────────────────────────────

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OpenFile_Click(this, e);
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            TxtFilter.Focus();
            TxtFilter.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (TabLogs.SelectedItem is TabItem tab)
            {
                if (_tabStates.TryGetValue(tab, out var state))
                {
                    state.Tailer.Dispose();
                    _tabStates.Remove(tab);
                }
                TabLogs.Items.Remove(tab);
                if (TabLogs.Items.Count == 0)
                    EmptyState.Visibility = Visibility.Visible;
                UpdateStatusBar();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.End && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (TabLogs.SelectedItem is TabItem tab && _tabStates.TryGetValue(tab, out var state))
                ScrollToEnd(state.RichTextBox);
            e.Handled = true;
        }
    }

    // ── 유틸 ──────────────────────────────────

    private static bool IsTextFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".log" or ".txt" or ".csv" or ".json" or ".xml" or ".yml" or ".yaml" or ".ini" or ".cfg" or ".conf" or "";
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (var (_, state) in _tabStates)
            state.Tailer.Dispose();
        _tabStates.Clear();
        base.OnClosed(e);
    }
}

internal sealed class LogTabState
{
    public required string FilePath { get; init; }
    public required LogTailer Tailer { get; init; }
    public required RichTextBox RichTextBox { get; set; }
    public required ObservableCollection<LogEntry> AllEntries { get; init; }
}
