using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using InkCast.Models;
using InkCast.Services;
using InkCast.Views;
using Microsoft.Web.WebView2.Core;

namespace InkCast;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // ── 서비스 ───────────────────────────────────────────────
    private readonly DatabaseService _db      = new();
    private readonly ExportService   _export  = new();
    private          NoteService?    _notes;

    // ── 상태 ────────────────────────────────────────────────
    private int    _workspaceId;
    private Note?  _currentNote;
    private string _activeTag = "";
    private bool   _isDirty;
    private bool   _previewReady;
    private List<string> _allTitles = [];

    private readonly DispatcherTimer _previewTimer;
    private readonly DispatcherTimer _saveTimer;

    // ── WikiLink 자동완성 ────────────────────────────────────
    private int    _wikiStart;   // [[ 시작 인덱스
    private string _wikiQuery = "";

    // ── 생성자 ──────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _previewTimer.Tick += async (_, _) => { _previewTimer.Stop(); await UpdatePreviewAsync(); };

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
        _saveTimer.Tick += async (_, _) => { _saveTimer.Stop(); await AutoSaveAsync(); };

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    // ── 초기화 ──────────────────────────────────────────────
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        if (PresentationSource.FromVisual(this) is HwndSource src)
        {
            int v = 1;
            DwmSetWindowAttribute(src.Handle, 20, ref v, sizeof(int));
        }

        // WebView2 초기화
        try
        {
            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "InkCast", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, userDataDir);
            await Preview.EnsureCoreWebView2Async(env);
            Preview.CoreWebView2.Settings.IsScriptEnabled               = false;
            Preview.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Preview.CoreWebView2.Settings.IsStatusBarEnabled            = false;
            _previewReady = true;
            ShowEmptyPreview();
        }
        catch
        {
            // WebView2 런타임 없을 경우 미리보기 패널 숨김
            Preview.Visibility      = Visibility.Collapsed;
            EditorSplitter.Visibility = Visibility.Collapsed;
            ColPreview.Width        = new System.Windows.GridLength(0);
            BtnPreviewToggle.IsEnabled = false;
        }

        // DB + 워크스페이스 초기화
        _notes = new NoteService(_db);
        _workspaceId = await _notes.EnsureDefaultWorkspaceAsync();

        await LoadWorkspacesAsync();
        await LoadNotesAsync();
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isDirty) await AutoSaveAsync();
    }

    // ── 워크스페이스 ─────────────────────────────────────────
    private async Task LoadWorkspacesAsync()
    {
        var list = await _notes!.GetWorkspacesAsync();
        CmbWorkspace.ItemsSource    = list;
        CmbWorkspace.DisplayMemberPath = "Name";
        var current = list.FirstOrDefault(w => w.Id == _workspaceId);
        CmbWorkspace.SelectedItem   = current ?? list.FirstOrDefault();
    }

    private async void CmbWorkspace_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CmbWorkspace.SelectedItem is not Workspace ws) return;
        if (_isDirty) await AutoSaveAsync();
        _workspaceId = ws.Id;
        await _notes!.UpdateWorkspaceLastOpenedAsync(ws.Id);
        ClearEditor();
        await LoadNotesAsync();
    }

    private async void BtnAddWorkspace_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputDialog("새 워크스페이스 이름을 입력하세요:", "새 워크스페이스") { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Result)) return;
        var id = await _notes!.CreateWorkspaceAsync(dlg.Result.Trim());
        _workspaceId = id;
        ClearEditor();
        await LoadWorkspacesAsync();
        await LoadNotesAsync();
    }

    private async void BtnDeleteWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (CmbWorkspace.SelectedItem is not Workspace ws) return;
        var list = await _notes!.GetWorkspacesAsync();
        if (list.Count <= 1) { MessageBox.Show("마지막 워크스페이스는 삭제할 수 없습니다.", "Ink.Cast"); return; }
        if (MessageBox.Show($"'{ws.Name}' 워크스페이스와 모든 노트를 삭제할까요?",
            "워크스페이스 삭제", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await _notes.DeleteWorkspaceAsync(ws.Id);
        _workspaceId = 0;
        ClearEditor();
        await LoadWorkspacesAsync();
        _workspaceId = (CmbWorkspace.SelectedItem as Workspace)?.Id ?? 0;
        await LoadNotesAsync();
    }

    // ── 노트 목록 ────────────────────────────────────────────
    private async Task LoadNotesAsync()
    {
        if (_notes is null) return;
        List<Note> notes = string.IsNullOrWhiteSpace(TxtSearch.Text)
            ? (string.IsNullOrEmpty(_activeTag)
                ? await _notes.GetAllAsync(_workspaceId)
                : await _notes.GetByTagAsync(_workspaceId, _activeTag))
            : await _notes.SearchAsync(_workspaceId, TxtSearch.Text);

        LstNotes.ItemsSource = notes;
        TxtNoteCount.Text    = $"노트 {notes.Count}개";
        _allTitles           = await _notes.GetAllTitlesAsync(_workspaceId);

        await LoadTagsAsync();
    }

    private async Task LoadTagsAsync()
    {
        var tags = await _notes!.GetAllTagsAsync(_workspaceId);
        TagPanel.Children.Clear();
        foreach (var (name, cnt) in tags)
        {
            var btn = new System.Windows.Controls.Button
            {
                Content    = $"#{name} ({cnt})",
                Margin     = new Thickness(0, 0, 4, 4),
                Padding    = new Thickness(7, 3, 7, 3),
                FontSize   = 11,
                Cursor     = Cursors.Hand,
                Background = name == _activeTag
                    ? new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x5A))
                    : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                Foreground = name == _activeTag
                    ? new SolidColorBrush(Color.FromRgb(0xCB, 0xA6, 0xF7))
                    : new SolidColorBrush(Color.FromRgb(0x7C, 0x6A, 0xF4)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x78)),
            };
            var tag = name;
            btn.Click += async (_, _) =>
            {
                _activeTag = _activeTag == tag ? "" : tag;
                await LoadTagsAsync();
                await LoadNotesAsync();
            };

            // 템플릿 간단히 적용
            var style = new System.Windows.Style(typeof(System.Windows.Controls.Button));
            style.Setters.Add(new Setter(System.Windows.Controls.Control.TemplateProperty,
                Application.Current.Resources.Contains("ToolBtn")
                    ? Application.Current.Resources["ToolBtn"]
                    : null));
            TagPanel.Children.Add(btn);
        }
    }

    private async void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _activeTag = "";
        await LoadNotesAsync();
    }

    private async void BtnClearTag_Click(object sender, RoutedEventArgs e)
    {
        _activeTag = "";
        TxtSearch.Text = "";
        await LoadNotesAsync();
    }

    private async void LstNotes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (LstNotes.SelectedItem is not Note note) return;
        if (_isDirty) await AutoSaveAsync();
        await OpenNoteAsync(note);
    }

    // ── 노트 열기/생성 ───────────────────────────────────────
    private async Task OpenNoteAsync(Note note)
    {
        _currentNote = note;
        TxtTitle.IsEnabled  = true;
        TxtEditor.IsEnabled = true;
        BtnDelete.IsEnabled = true;
        BtnExport.IsEnabled = true;

        TxtTitle.Text  = note.Title;
        TxtEditor.Text = note.Content;
        _isDirty = false;

        await UpdateStatusBarAsync();
        await UpdatePreviewAsync();
    }

    private async void BtnNewNote_Click(object sender, RoutedEventArgs e)
    {
        if (_isDirty) await AutoSaveAsync();

        var title = $"새 노트 {DateTime.Now:HH:mm:ss}";
        var note  = new Note { WorkspaceId = _workspaceId, Title = title, Content = "" };
        note.Id   = await _notes!.CreateAsync(note);

        await LoadNotesAsync();
        // 새 노트 선택
        foreach (Note item in LstNotes.Items)
        {
            if (item.Id == note.Id) { LstNotes.SelectedItem = item; break; }
        }
        TxtTitle.Focus();
        TxtTitle.SelectAll();
    }

    private async void BtnDailyNote_Click(object sender, RoutedEventArgs e)
    {
        if (_isDirty) await AutoSaveAsync();
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var existing = await _notes!.GetByTitleAsync(_workspaceId, today);
        if (existing is not null)
        {
            foreach (Note item in LstNotes.Items)
            {
                if (item.Id == existing.Id) { LstNotes.SelectedItem = item; break; }
            }
            return;
        }

        var content = $"# {today} 일별 노트\n\n## 오늘의 할 일\n- [ ] \n\n## 메모\n\n## 회고\n";
        var note    = new Note { WorkspaceId = _workspaceId, Title = today, Content = content };
        note.Id     = await _notes.CreateAsync(note);
        await LoadNotesAsync();
        foreach (Note item in LstNotes.Items)
        {
            if (item.Id == note.Id) { LstNotes.SelectedItem = item; break; }
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_currentNote is null) return;
        if (MessageBox.Show($"'{_currentNote.Title}' 노트를 삭제할까요?",
            "노트 삭제", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await _notes!.DeleteAsync(_currentNote.Id);
        ClearEditor();
        await LoadNotesAsync();
    }

    // ── 에디터 이벤트 ────────────────────────────────────────
    private async void TxtTitle_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || _currentNote is null) return;
        _currentNote.Title = TxtTitle.Text;
        _isDirty = true;
        _saveTimer.Stop();
        _saveTimer.Start();
        await UpdatePreviewAsync();
    }

    private void TxtEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || _currentNote is null) return;
        _currentNote.Content = TxtEditor.Text;
        _isDirty = true;
        _saveTimer.Stop();
        _saveTimer.Start();
        _previewTimer.Stop();
        _previewTimer.Start();
        UpdateWordCount();
        CheckWikiLinkAutoComplete();
    }

    private void TxtEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (WikiPopup.IsOpen)
        {
            if (e.Key == Key.Down)  { WikiList.SelectedIndex = Math.Min(WikiList.SelectedIndex + 1, WikiList.Items.Count - 1); WikiList.Focus(); e.Handled = true; }
            if (e.Key == Key.Up)    { WikiList.SelectedIndex = Math.Max(WikiList.SelectedIndex - 1, 0); WikiList.Focus(); e.Handled = true; }
            if (e.Key == Key.Enter) { CompleteWikiLink(); e.Handled = true; }
            if (e.Key == Key.Escape){ WikiPopup.IsOpen = false; e.Handled = true; }
            return;
        }

        // Tab → 스페이스 2칸 삽입
        if (e.Key == Key.Tab)
        {
            var idx = TxtEditor.CaretIndex;
            TxtEditor.Text = TxtEditor.Text.Insert(idx, "  ");
            TxtEditor.CaretIndex = idx + 2;
            e.Handled = true;
        }
    }

    // ── WikiLink 자동완성 ────────────────────────────────────
    private void CheckWikiLinkAutoComplete()
    {
        var text  = TxtEditor.Text;
        var caret = TxtEditor.CaretIndex;
        var start = Math.Max(0, caret - 200);
        var before = text.Substring(start, caret - start);
        var lastOpen = before.LastIndexOf("[[");

        if (lastOpen >= 0)
        {
            var after = before[(lastOpen + 2)..];
            if (!after.Contains("]]") && !after.Contains('\n'))
            {
                _wikiStart  = start + lastOpen;
                _wikiQuery  = after;

                var matches = _allTitles
                    .Where(t => t.Contains(_wikiQuery, StringComparison.OrdinalIgnoreCase))
                    .Take(10)
                    .ToList();

                if (matches.Count > 0)
                {
                    WikiList.ItemsSource     = matches;
                    WikiList.SelectedIndex   = 0;
                    PositionWikiPopup();
                    WikiPopup.IsOpen = true;
                    return;
                }
            }
        }

        WikiPopup.IsOpen = false;
    }

    private void PositionWikiPopup()
    {
        try
        {
            var rect = TxtEditor.GetRectFromCharacterIndex(TxtEditor.CaretIndex);
            WikiPopup.HorizontalOffset = rect.Left + 16;
            WikiPopup.VerticalOffset   = rect.Bottom + 4;
        }
        catch { /* 위치 계산 실패 시 무시 */ }
    }

    private void WikiList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void WikiList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => CompleteWikiLink();

    private void WikiList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CompleteWikiLink(); e.Handled = true; }
        if (e.Key == Key.Escape){ WikiPopup.IsOpen = false; TxtEditor.Focus(); e.Handled = true; }
    }

    private void CompleteWikiLink()
    {
        if (WikiList.SelectedItem is not string title) return;
        var text  = TxtEditor.Text;
        var caret = TxtEditor.CaretIndex;
        var insertEnd = caret;
        var replace = text[_wikiStart..insertEnd];
        var completion = $"[[{title}]]";
        TxtEditor.Text = text[.._wikiStart] + completion + text[insertEnd..];
        TxtEditor.CaretIndex = _wikiStart + completion.Length;
        WikiPopup.IsOpen = false;
        TxtEditor.Focus();
    }

    // ── 저장 / 미리보기 ─────────────────────────────────────
    private async Task AutoSaveAsync()
    {
        if (_currentNote is null || !_isDirty) return;
        try
        {
            await _notes!.UpdateAsync(_currentNote);
            _isDirty = false;
            TxtStatus.Text = $"저장됨 {DateTime.Now:HH:mm:ss}";
            _allTitles = await _notes.GetAllTitlesAsync(_workspaceId);
            // 목록 새로고침 (제목 변경 반영)
            var sel = _currentNote.Id;
            await LoadNotesAsync();
            foreach (Note item in LstNotes.Items)
                if (item.Id == sel) { LstNotes.SelectedItem = item; break; }
        }
        catch (Exception ex) { TxtStatus.Text = $"저장 오류: {ex.Message}"; }
    }

    private async Task UpdatePreviewAsync()
    {
        if (!_previewReady || _currentNote is null) return;
        var html = _export.ToPreviewHtml(_currentNote.Content);
        Preview.NavigateToString(html);
        await UpdateStatusBarAsync();
    }

    private void ShowEmptyPreview()
    {
        if (!_previewReady) return;
        Preview.NavigateToString("""
            <!DOCTYPE html>
            <html><head><style>
            body{background:#1e1e2e;display:flex;align-items:center;justify-content:center;
                 height:100vh;margin:0;font-family:'Segoe UI',sans-serif;}
            .hint{text-align:center;color:#585b70;}
            .hint h2{color:#313244;font-size:2em;margin-bottom:.5em;}
            </style></head>
            <body><div class="hint">
              <h2>✦ Ink.Cast</h2>
              <p>왼쪽에서 노트를 선택하거나 새 노트를 만들어보세요.</p>
            </div></body></html>
            """);
    }

    private async Task UpdateStatusBarAsync()
    {
        if (_currentNote is null) return;
        var words = _currentNote.Content.Split([' ', '\n', '\r', '\t'],
            StringSplitOptions.RemoveEmptyEntries).Length;
        TxtWordCount.Text = $"{words}단어 · {_currentNote.Content.Length}자";
        TxtTagCount.Text  = _currentNote.Tags.Count > 0
            ? string.Join("  ", _currentNote.Tags.Select(t => $"#{t}"))
            : "";
        var backlinks = await _notes!.GetBacklinksAsync(_workspaceId, _currentNote.Title);
        TxtBacklinks.Text = $"백링크 {backlinks.Count}개";
    }

    private void UpdateWordCount()
    {
        if (_currentNote is null) return;
        var words = TxtEditor.Text.Split([' ', '\n', '\r', '\t'],
            StringSplitOptions.RemoveEmptyEntries).Length;
        TxtWordCount.Text = $"{words}단어 · {TxtEditor.Text.Length}자";
    }

    private void ClearEditor()
    {
        _currentNote       = null;
        _isDirty           = false;
        TxtTitle.Text      = "";
        TxtEditor.Text     = "";
        TxtTitle.IsEnabled = false;
        TxtEditor.IsEnabled= false;
        BtnDelete.IsEnabled= false;
        BtnExport.IsEnabled= false;
        TxtWordCount.Text  = "";
        TxtTagCount.Text   = "";
        TxtBacklinks.Text  = "";
        ShowEmptyPreview();
    }

    // ── 미리보기 / 그래프 토글 ───────────────────────────────
    private void BtnPreviewToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ColPreview.Width   = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
        EditorSplitter.Visibility = Visibility.Visible;
    }

    private void BtnPreviewToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ColPreview.Width   = new System.Windows.GridLength(0);
        EditorSplitter.Visibility = Visibility.Collapsed;
    }

    private async void BtnGraphToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        GraphPanel.Visibility = Visibility.Visible;
        // 그래프 데이터 로드
        var notes = await _notes!.GetAllAsync(_workspaceId);
        var links = await _notes.GetAllLinksAsync(_workspaceId);
        GraphPanel.LoadGraph(notes, links);
    }

    private void BtnGraphToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        GraphPanel.Visibility = Visibility.Collapsed;
    }

    private async void GraphPanel_NoteSelected(int noteId)
    {
        BtnGraphToggle.IsChecked = false;
        foreach (Note item in LstNotes.Items)
        {
            if (item.Id == noteId) { LstNotes.SelectedItem = item; break; }
        }
        if (LstNotes.SelectedItem is Note note) await OpenNoteAsync(note);
    }

    // ── HTML 내보내기 ─────────────────────────────────────────
    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_currentNote is null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "HTML 내보내기",
            FileName   = SanitizeFileName(_currentNote.Title) + ".html",
            Filter     = "HTML 파일|*.html",
            DefaultExt = ".html",
        };
        if (dlg.ShowDialog() != true) return;
        if (_isDirty) await AutoSaveAsync();
        await _export.ExportToFileAsync(_currentNote, dlg.FileName);
        TxtStatus.Text = $"내보내기 완료: {Path.GetFileName(dlg.FileName)}";
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }
}
