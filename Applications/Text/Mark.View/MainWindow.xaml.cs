using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MarkView.Models;
using MarkView.Services;
using System.Linq;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;

namespace MarkView;

public partial class MainWindow : Window
{
    private readonly MarkdownRenderer _renderer = new();
    private readonly List<MarkDocument> _docs = [];
    private readonly List<Border> _tabs = [];
    private readonly AppSettings _settings;
    private int _activeIndex = -1;
    private bool _isEditMode;
    private bool _isTocVisible;
    private bool _suppressEditorChange;
    private DispatcherTimer? _previewTimer;
    private string _currentTheme = "dark";
    private double _pendingScrollY = 0;
    private bool _webViewReady = false;

    public MainWindow()
    {
        _settings = AppSettings.Load();
        _currentTheme = _settings.Theme;
        InitializeComponent();
        ApplySavedTheme();
        InitWebView();
        SetupPreviewTimer();
        RefreshRecentList();
    }

    private void ApplySavedTheme()
    {
        var item = CmbTheme.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(i => i.Tag as string == _currentTheme);
        if (item != null) CmbTheme.SelectedItem = item;
    }

    // ── 로딩 UI ─────────────────────────────────────────────────────────

    private DoubleAnimation? _loadingAnim;
    private long _loadingStartTick;

    private void ShowLoading(string message = "로딩 중...")
    {
        _loadingStartTick = Environment.TickCount64;
        StatusPath.Text = message;
        LoadingBarTrack.Visibility = Visibility.Visible;   // solid 바 즉시 표시
        _loadingAnim = new DoubleAnimation(-120, 1500,
            new Duration(TimeSpan.FromSeconds(1.2)))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        LoadingBarTranslate.BeginAnimation(TranslateTransform.XProperty, _loadingAnim);
    }

    // WPF가 최소 1프레임 렌더링 후 무거운 작업 시작
    private async Task ShowLoadingAsync(string message = "로딩 중...")
    {
        ShowLoading(message);
        await Task.Delay(16);
    }

    private void HideLoading()
    {
        LoadingBarTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        LoadingBarTrack.Visibility = Visibility.Collapsed;
        // 상태바 경로 텍스트 복원
        if (_activeIndex >= 0 && _activeIndex < _docs.Count)
            StatusPath.Text = _docs[_activeIndex].IsNew ? "새 문서 (저장되지 않음)" : _docs[_activeIndex].FilePath;
        else
            StatusPath.Text = "파일을 열어주세요";
    }

    // 최소 표시 시간 보장 (기본 300ms)
    private async Task HideLoadingAsync(int minMs = 300)
    {
        var elapsed = (int)(Environment.TickCount64 - _loadingStartTick);
        if (elapsed < minMs) await Task.Delay(minMs - elapsed);
        HideLoading();
    }

    // ── WebView2 초기화 ──────────────────────────────────────────────────

    private async void InitWebView()
    {
        ShowLoading("WebView2 초기화 중...");
        var env = await CoreWebView2Environment.CreateAsync();
        await Viewer.EnsureCoreWebView2Async(env);
        Viewer.CoreWebView2.Settings.IsStatusBarEnabled = false;
        Viewer.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        Viewer.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        _webViewReady = true;
        HideLoading();
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _ = HideLoadingAsync(300);
        _ = ExtractTocAsync();
        if (_pendingScrollY > 0)
            _ = RestoreScrollAsync();
    }

    private async Task<double> GetScrollYAsync()
    {
        try
        {
            var result = await Viewer.ExecuteScriptAsync("window.scrollY.toString()");
            if (double.TryParse(result.Trim('"'), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var y)) return y;
        }
        catch { }
        return 0;
    }

    private async Task RestoreScrollAsync()
    {
        await Task.Delay(50);
        try
        {
            await Viewer.ExecuteScriptAsync($"window.scrollTo(0, {(int)_pendingScrollY})");
        }
        catch { }
        _pendingScrollY = 0;
    }

    private async Task ExtractTocAsync()
    {
        if (!IsLoaded) return;
        try
        {
            var json = await Viewer.ExecuteScriptAsync(@"
                (function() {
                    var els = document.querySelectorAll('h1,h2,h3,h4,h5,h6');
                    var result = [];
                    els.forEach(function(el) {
                        result.push(JSON.stringify({level: parseInt(el.tagName[1]), text: el.textContent, id: el.id}));
                    });
                    return '[' + result.join(',') + ']';
                })()
            ");
            // JSON 파싱
            PopulateToc(json);
        }
        catch { }
    }

    private record TocEntry(int Level, string Text, string Id);

    private void PopulateToc(string json)
    {
        TocList.Items.Clear();
        json = json.Trim('"');
        // unescape
        json = Regex.Unescape(json.Replace("\\\"", "\""));
        var matches = Regex.Matches(json, @"\{""level"":(\d),""text"":""([^""]*)"",""id"":""([^""]*)""\}");
        foreach (Match m in matches)
        {
            int level = int.Parse(m.Groups[1].Value);
            string text = m.Groups[2].Value;
            string id = m.Groups[3].Value;
            var indent = new string(' ', (level - 1) * 2);
            var item = new ListBoxItem
            {
                Content = indent + text,
                Tag = id,
                Padding = new Thickness(12 + (level - 1) * 8, 4, 8, 4),
                FontSize = level == 1 ? 13 : level == 2 ? 12 : 11,
                FontWeight = level <= 2 ? FontWeights.SemiBold : FontWeights.Normal,
            };
            TocList.Items.Add(item);
        }
    }

    // ── 프리뷰 타이머 ───────────────────────────────────────────────────

    private void SetupPreviewTimer()
    {
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            RenderPreview(saveScroll: true);
        };
    }

    // ── 탭 관리 ─────────────────────────────────────────────────────────

    private void OpenDocument(MarkDocument doc)
    {
        // 이미 열려있는지 확인
        if (!doc.IsNew)
        {
            var existing = _docs.FindIndex(d => !d.IsNew &&
                string.Equals(d.FilePath, doc.FilePath, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0) { SwitchTo(existing); return; }
        }

        _docs.Add(doc);
        var tab = CreateTabItem(doc, _docs.Count - 1);
        _tabs.Add(tab);
        TabBar.Children.Add(tab);

        SwitchTo(_docs.Count - 1);
        UpdateEmptyState();
    }

    private Border CreateTabItem(MarkDocument doc, int index)
    {
        var tab = new Border
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0, 0, 0, 2),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(14, 8, 8, 8),
            Cursor = Cursors.Hand,
            Tag = index,
        };

        var inner = new StackPanel { Orientation = Orientation.Horizontal };

        var title = new TextBlock
        {
            Text = doc.TabTitle,
            FontSize = 12,
            Foreground = (SolidColorBrush)FindResource("TextDimBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = "title",
        };

        var closeBtn = new TextBlock
        {
            Text = " ×",
            FontSize = 14,
            Foreground = (SolidColorBrush)FindResource("TextDimBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
            Tag = index,
        };
        closeBtn.MouseLeftButtonUp += CloseTab_Click;
        closeBtn.MouseEnter += (s, _) => ((TextBlock)s).Foreground =
            (SolidColorBrush)FindResource("TextBrush");
        closeBtn.MouseLeave += (s, _) => ((TextBlock)s).Foreground =
            (SolidColorBrush)FindResource("TextDimBrush");

        inner.Children.Add(title);
        inner.Children.Add(closeBtn);
        tab.Child = inner;

        tab.MouseLeftButtonDown += Tab_Click;
        tab.MouseDown += Tab_MouseDown;
        tab.MouseEnter += (s, _) =>
        {
            if ((int)((Border)s).Tag != _activeIndex)
                ((Border)s).Background = (SolidColorBrush)FindResource("HoverBrush");
        };
        tab.MouseLeave += (s, _) =>
        {
            if ((int)((Border)s).Tag != _activeIndex)
                ((Border)s).Background = Brushes.Transparent;
        };

        return tab;
    }

    private void Tab_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b)
            SwitchTo((int)b.Tag);
    }

    private void Tab_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && sender is Border b)
        {
            e.Handled = true;
            CloseTab((int)b.Tag);
        }
    }

    private void CloseTab_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is TextBlock tb)
            CloseTab((int)tb.Tag);
    }

    private async void SwitchTo(int index)
    {
        if (index < 0 || index >= _docs.Count) return;

        // 현재 탭 스크롤 저장
        if (_activeIndex >= 0 && _activeIndex < _docs.Count)
            _docs[_activeIndex].ScrollY = await GetScrollYAsync();

        // 이전 탭 스타일 복원
        if (_activeIndex >= 0 && _activeIndex < _tabs.Count)
        {
            _tabs[_activeIndex].BorderBrush = Brushes.Transparent;
            _tabs[_activeIndex].Background = Brushes.Transparent;
            UpdateTabTitle(_activeIndex);
        }

        _activeIndex = index;
        _pendingScrollY = _docs[index].ScrollY;
        var tab = _tabs[index];
        tab.BorderBrush = (SolidColorBrush)FindResource("AccentBrush");
        if (tab.Child is StackPanel sp && sp.Children[0] is TextBlock t)
            t.Foreground = (SolidColorBrush)FindResource("TextBrush");

        LoadDocumentToUI(_docs[index]);
    }

    private void LoadDocumentToUI(MarkDocument doc)
    {
        _suppressEditorChange = true;
        Editor.Text = doc.Content;
        _suppressEditorChange = false;

        RenderPreview();
        UpdateStatusBar(doc);
        Title = $"Mark.View — {doc.TabTitle}";
    }

    private void UpdateTabTitle(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        if (_tabs[index].Child is StackPanel sp && sp.Children[0] is TextBlock t)
        {
            t.Text = _docs[index].TabTitle;
            t.Foreground = index == _activeIndex
                ? (SolidColorBrush)FindResource("TextBrush")
                : (SolidColorBrush)FindResource("TextDimBrush");
        }
    }

    private void CloseTab(int index)
    {
        if (index < 0 || index >= _docs.Count) return;
        var doc = _docs[index];

        if (doc.IsModified)
        {
            var result = MessageBox.Show(
                $"'{doc.FileName}'의 변경 사항을 저장하시겠습니까?",
                "저장 확인",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel) return;
            if (result == MessageBoxResult.Yes) SaveDocument(doc);
        }

        TabBar.Children.RemoveAt(index);
        _docs.RemoveAt(index);
        _tabs.RemoveAt(index);

        // 인덱스 재매핑
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Tag = i;
            if (_tabs[i].Child is StackPanel sp)
                foreach (var c in sp.Children.OfType<TextBlock>())
                    if (c.Tag is int) c.Tag = i;
        }

        _activeIndex = -1;
        if (_docs.Count > 0)
            SwitchTo(Math.Min(index, _docs.Count - 1));
        else
            UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        bool hasDoc = _docs.Count > 0;
        EmptyState.Visibility = hasDoc ? Visibility.Collapsed : Visibility.Visible;
        ContentArea.Visibility = hasDoc ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── 렌더링 ──────────────────────────────────────────────────────────

    private async void RenderPreview(bool saveScroll = false)
    {
        if (!_webViewReady) return;
        if (_activeIndex < 0 || _activeIndex >= _docs.Count) return;
        if (saveScroll)
            _pendingScrollY = await GetScrollYAsync();
        var doc = _docs[_activeIndex];
        var content = doc.Content;
        var filePath = doc.IsNew ? null : doc.FilePath;
        var theme = _currentTheme;

        await ShowLoadingAsync("렌더링 중...");
        var html = await Task.Run(() => _renderer.RenderToHtml(content, filePath, theme));
        Viewer.NavigateToString(html);
        // HideLoading은 OnNavigationCompleted에서 호출됨
    }

    // ── 편집 모드 ────────────────────────────────────────────────────────

    private void SetEditMode(bool editMode)
    {
        _isEditMode = editMode;
        if (editMode)
        {
            EditorColumn.Width = new GridLength(1, GridUnitType.Star);
            SplitterColumn.Width = new GridLength(4);
            TxtEditIcon.Foreground = (SolidColorBrush)FindResource("AccentBrush");
            StatusMode.Text = "편집";
        }
        else
        {
            EditorColumn.Width = new GridLength(0);
            SplitterColumn.Width = new GridLength(0);
            TxtEditIcon.Foreground = (SolidColorBrush)FindResource("TextDimBrush");
            StatusMode.Text = "뷰";
        }
    }

    // ── TOC ─────────────────────────────────────────────────────────────

    private void SetTocVisible(bool visible)
    {
        _isTocVisible = visible;
        TocPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        TocSplitter.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (!visible) TocColumn.Width = new GridLength(0);
        else TocColumn.Width = new GridLength(220);
    }

    // ── 파일 작업 ────────────────────────────────────────────────────────

    private bool SaveDocument(MarkDocument doc)
    {
        if (doc.IsNew) return SaveDocumentAs(doc);
        try
        {
            File.WriteAllText(doc.FilePath, doc.Content, new UTF8Encoding(true));
            doc.IsModified = false;
            int i = _docs.IndexOf(doc);
            UpdateTabTitle(i);
            if (i == _activeIndex) Title = $"Mark.View — {doc.TabTitle}";
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool SaveDocumentAs(MarkDocument doc)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Markdown 파일 (*.md;*.markdown)|*.md;*.markdown|모든 파일 (*.*)|*.*",
            DefaultExt = ".md",
            FileName = doc.FileName,
        };
        if (doc.Directory != null) dlg.InitialDirectory = doc.Directory;
        if (dlg.ShowDialog() != true) return false;

        doc.FilePath = dlg.FileName;
        return SaveDocument(doc);
    }

    // ── 상태바 ──────────────────────────────────────────────────────────

    private void UpdateStatusBar(MarkDocument doc)
    {
        StatusPath.Text = doc.IsNew ? "새 문서 (저장되지 않음)" : doc.FilePath;
        var lines = doc.Content.Split('\n').Length;
        var words = string.IsNullOrWhiteSpace(doc.Content)
            ? 0
            : Regex.Matches(doc.Content, @"\S+").Count;
        StatusLines.Text = $"{lines}줄";
        StatusWords.Text = $"{words}단어";
        StatusMode.Text = _isEditMode ? "편집" : "뷰";
    }

    // ── 찾기 ────────────────────────────────────────────────────────────

    private void FindInPreview(string keyword)
    {
        if (string.IsNullOrEmpty(keyword)) return;
        _ = Viewer.ExecuteScriptAsync(
            $"window.find({System.Text.Json.JsonSerializer.Serialize(keyword)}, false, false, true)");
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────────────────

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        OpenDocument(new MarkDocument());
        if (!_isEditMode) SetEditMode(true);
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Markdown 파일 (*.md;*.markdown)|*.md;*.markdown|텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog() != true) return;
        foreach (var f in dlg.FileNames) OpenFile(f);
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_activeIndex < 0) return;
        SaveDocument(_docs[_activeIndex]);
    }

    private void BtnToggleEdit_Click(object sender, RoutedEventArgs e)
    {
        SetEditMode(!_isEditMode);
    }

    private void BtnToc_Click(object sender, RoutedEventArgs e)
    {
        SetTocVisible(!_isTocVisible);
    }

    private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CmbTheme.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _currentTheme = tag;
            _settings.Theme = tag;
            _settings.Save();
            RenderPreview(saveScroll: true);
        }
    }

    private async void BtnReload_Click(object sender, RoutedEventArgs e)
    {
        if (_activeIndex < 0 || _activeIndex >= _docs.Count) return;
        _pendingScrollY = await GetScrollYAsync();
        var doc = _docs[_activeIndex];
        if (!doc.IsNew && File.Exists(doc.FilePath))
        {
            await ShowLoadingAsync("파일 다시 읽기 중...");
            doc.Content = await Task.Run(() => File.ReadAllText(doc.FilePath));
            doc.IsModified = false;
            LoadDocumentToUI(doc);
            UpdateTabTitle(_activeIndex);
        }
        else RenderPreview();
    }

    private async void BtnExportHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_activeIndex < 0) return;
        var doc = _docs[_activeIndex];
        var dlg = new SaveFileDialog
        {
            Filter = "HTML 파일 (*.html)|*.html",
            DefaultExt = ".html",
            FileName = System.IO.Path.GetFileNameWithoutExtension(doc.FileName) + ".html",
        };
        if (doc.Directory != null) dlg.InitialDirectory = doc.Directory;
        if (dlg.ShowDialog() != true) return;

        await ShowLoadingAsync("HTML 내보내기 중...");
        var content = doc.Content;
        var filePath = doc.IsNew ? null : doc.FilePath;
        await Task.Run(() =>
        {
            var html = _renderer.RenderToHtml(content, filePath);
            File.WriteAllText(dlg.FileName, html, new UTF8Encoding(true));
        });
        HideLoading();
        MessageBox.Show($"HTML 내보내기 완료:\n{dlg.FileName}", "완료",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressEditorChange || _activeIndex < 0) return;
        var doc = _docs[_activeIndex];
        doc.Content = Editor.Text;
        if (!doc.IsModified)
        {
            doc.IsModified = true;
            UpdateTabTitle(_activeIndex);
            Title = $"Mark.View — {doc.TabTitle}";
        }
        UpdateStatusBar(doc);
        _previewTimer?.Stop();
        _previewTimer?.Start();
    }

    private void Editor_KeyDown(object sender, KeyEventArgs e)
    {
        // Tab 키 처리 (들여쓰기)
        if (e.Key == Key.Tab)
        {
            e.Handled = true;
            int caret = Editor.CaretIndex;
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                // 역들여쓰기
                if (caret > 0 && Editor.Text[caret - 1] == ' ')
                {
                    int start = caret - 1;
                    int count = 1;
                    while (start > 0 && count < 4 && Editor.Text[start - 1] == ' ')
                    { start--; count++; }
                    Editor.Text = Editor.Text.Remove(start, count);
                    Editor.CaretIndex = start;
                }
            }
            else
            {
                Editor.Text = Editor.Text.Insert(caret, "    ");
                Editor.CaretIndex = caret + 4;
            }
        }
    }

    private void TxtFind_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        FindInPreview(TxtFind.Text);
    }

    private void TxtFind_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) FindInPreview(TxtFind.Text);
        if (e.Key == Key.Escape) TxtFind.Text = "";
    }

    private void TocList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (TocList.SelectedItem is ListBoxItem item && item.Tag is string id && !string.IsNullOrEmpty(id))
        {
            _ = Viewer.ExecuteScriptAsync(
                $"document.getElementById({System.Text.Json.JsonSerializer.Serialize(id)})?.scrollIntoView({{behavior:'smooth'}})");
        }
    }

    // ── Keyboard Shortcuts (Window level) ───────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        { BtnOpen_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        { BtnSave_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.S && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        { if (_activeIndex >= 0) SaveDocumentAs(_docs[_activeIndex]); e.Handled = true; }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        { BtnNew_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
        { if (_activeIndex >= 0) CloseTab(_activeIndex); e.Handled = true; }
        else if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
        { SetEditMode(!_isEditMode); e.Handled = true; }
        else if (e.Key == Key.F5)
        { BtnReload_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        { TxtFind.Focus(); e.Handled = true; }
        else if (e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Control)
        { BtnExportHtml_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
        { SetTocVisible(!_isTocVisible); e.Handled = true; }
        else if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Control && _docs.Count > 1)
        { SwitchTo((_activeIndex + 1) % _docs.Count); e.Handled = true; }
    }

    // ── 드래그 앤 드롭 ───────────────────────────────────────────────────

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var f in files)
            if (File.Exists(f)) OpenFile(f);
    }

    private async void OpenFile(string path)
    {
        await ShowLoadingAsync("파일 열기 중...");
        try
        {
            var content = await Task.Run(() => File.ReadAllText(path));
            OpenDocument(new MarkDocument { FilePath = path, Content = content });
            _settings.AddRecentFile(path);
            _settings.Save();
            RefreshRecentList();
        }
        catch (Exception ex)
        {
            HideLoading();
            MessageBox.Show($"파일 열기 실패:\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        // 정상 경로는 RenderPreview → NavigationCompleted 에서 HideLoading 호출
    }

    // ── 최근 파일 ────────────────────────────────────────────────────────

    private record RecentFileItem(string FileName, string Directory, string FullPath);

    private void RefreshRecentList()
    {
        var valid = _settings.RecentFiles.Where(File.Exists).ToList();
        if (valid.Count != _settings.RecentFiles.Count)
        {
            _settings.RecentFiles = valid;
            _settings.Save();
        }

        if (valid.Count == 0)
        {
            RecentFilesPanel.Visibility = Visibility.Collapsed;
            return;
        }

        RecentFilesList.ItemsSource = valid.Select(p => new RecentFileItem(
            System.IO.Path.GetFileName(p),
            System.IO.Path.GetDirectoryName(p) ?? "",
            p)).ToList();
        RecentFilesPanel.Visibility = Visibility.Visible;
    }

    private void RecentFile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border b && b.DataContext is RecentFileItem item)
            OpenFile(item.FullPath);
    }

    private void RecentFile_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border b)
            b.Background = (System.Windows.Media.SolidColorBrush)FindResource("HoverBrush");
    }

    private void RecentFile_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border b)
            b.Background = System.Windows.Media.Brushes.Transparent;
    }

    // ── 종료 ────────────────────────────────────────────────────────────

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        var modified = _docs.Where(d => d.IsModified).ToList();
        if (modified.Count == 0) return;

        var names = string.Join("\n  • ", modified.Select(d => d.FileName));
        var result = MessageBox.Show(
            $"저장되지 않은 파일이 있습니다:\n  • {names}\n\n종료하시겠습니까?",
            "종료 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.No)
            e.Cancel = true;
    }
}
