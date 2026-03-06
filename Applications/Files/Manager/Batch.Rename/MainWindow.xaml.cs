using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BatchRename.Models;
using BatchRename.Services;

namespace BatchRename;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    private readonly ObservableCollection<RenameEntry> _entries = [];
    private readonly RenameService _svc = new();
    private readonly AppSettings   _settings;

    private bool _isPatternMode = true;

    public MainWindow()
    {
        InitializeComponent();
        _settings = SettingsService.Load();
        Grid.ItemsSource = _entries;
        _entries.CollectionChanged += (_, _) => UpdateStatus();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));

        // 최근 패턴 복원
        if (_settings.RecentPatterns.Count > 0)
            TxtPattern.Text = _settings.RecentPatterns[0];
    }

    // ─────────────────────────────────────────────
    // 파일 추가
    // ─────────────────────────────────────────────

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        AddPaths(paths);
    }

    private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.OpenFileDialog
        {
            Multiselect = true,
            Title       = "파일 선택",
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            AddPaths(dlg.FileNames);
    }

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "폴더 선택 (폴더 내 파일 전체 추가)",
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            AddPaths(Directory.GetFiles(dlg.SelectedPath));
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        var existing = _entries.Select(x => x.OriginalPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var p in paths)
        {
            if (!File.Exists(p)) continue;
            if (existing.Contains(p)) continue;

            _entries.Add(new RenameEntry
            {
                OriginalPath = p,
                OriginalName = Path.GetFileName(p),
                PreviewName  = Path.GetFileName(p),
            });
            existing.Add(p);
        }

        _svc.ClearUndo();
        BtnUndo.IsEnabled = false;
        DropHint.Visibility = _entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshPreview();
    }

    // ─────────────────────────────────────────────
    // 탭 전환
    // ─────────────────────────────────────────────

    private void TabPattern_Checked(object sender, RoutedEventArgs e)
    {
        if (TabRegex is null || PatternPanel is null || RegexPanel is null) return;
        _isPatternMode  = true;
        TabRegex.IsChecked = false;
        PatternPanel.Visibility = Visibility.Visible;
        RegexPanel.Visibility   = Visibility.Collapsed;
        RefreshPreview();
    }

    private void TabRegex_Checked(object sender, RoutedEventArgs e)
    {
        if (TabPattern is null || PatternPanel is null || RegexPanel is null) return;
        _isPatternMode  = false;
        TabPattern.IsChecked = false;
        PatternPanel.Visibility = Visibility.Collapsed;
        RegexPanel.Visibility   = Visibility.Visible;
        RefreshPreview();
    }

    // ─────────────────────────────────────────────
    // 미리보기 갱신
    // ─────────────────────────────────────────────

    private void Input_Changed(object sender, TextChangedEventArgs e) => RefreshPreview();

    private void RefreshPreview()
    {
        if (TxtInputStatus is null || BtnApply is null) return;

        if (_entries.Count == 0)
        {
            TxtInputStatus.Text = "";
            BtnApply.IsEnabled  = false;
            return;
        }

        if (_isPatternMode)
            RenameService.UpdatePreviewPattern(_entries, TxtPattern.Text);
        else
            RenameService.UpdatePreviewRegex(_entries, TxtRegexFind.Text, TxtRegexReplace.Text);

        int errors = _entries.Count(x => x.HasError);
        TxtInputStatus.Text = errors > 0
            ? $"⚠ {errors}개 항목에 오류가 있습니다."
            : $"미리보기: {_entries.Count}개 파일";

        BtnApply.IsEnabled = _entries.Count > 0 && errors < _entries.Count;

        // DataGrid 강제 갱신
        Grid.Items.Refresh();
    }

    private void VarBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var var_ = btn.Tag?.ToString() ?? "";
        int caret = TxtPattern.CaretIndex;
        TxtPattern.Text = TxtPattern.Text.Insert(caret, var_);
        TxtPattern.CaretIndex = caret + var_.Length;
        TxtPattern.Focus();
    }

    // ─────────────────────────────────────────────
    // 적용 / 되돌리기
    // ─────────────────────────────────────────────

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        var (ok, fail, _) = _svc.Apply(_entries);

        // 적용 후: 파일 목록에서 성공한 항목의 OriginalPath 갱신
        foreach (var entry in _entries)
        {
            var newPath = Path.Combine(
                Path.GetDirectoryName(entry.OriginalPath)!, entry.PreviewName);
            if (File.Exists(newPath) && !entry.HasError)
            {
                // 내부적으로 OriginalPath를 바꿀 수 없으므로 목록 재빌드
            }
        }

        // 목록 재빌드 (OriginalPath 갱신)
        var updated = _entries.Select(e2 =>
        {
            if (e2.HasError) return e2;
            var newPath = Path.Combine(
                Path.GetDirectoryName(e2.OriginalPath)!, e2.PreviewName);
            return File.Exists(newPath)
                ? new RenameEntry
                {
                    OriginalPath = newPath,
                    OriginalName = e2.PreviewName,
                    PreviewName  = e2.PreviewName,
                }
                : e2;
        }).ToList();

        _entries.Clear();
        foreach (var u in updated) _entries.Add(u);

        BtnUndo.IsEnabled = _svc.CanUndo;
        TxtStatus.Text    = $"✓ {ok}개 변경 완료" + (fail > 0 ? $" / ⚠ {fail}개 실패" : "");

        SaveRecentPattern();
        RefreshPreview();
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e)
    {
        var (ok, fail) = _svc.Undo();
        TxtStatus.Text    = $"↩ {ok}개 되돌림 완료" + (fail > 0 ? $" / {fail}개 실패" : "");
        BtnUndo.IsEnabled = _svc.CanUndo;

        // 목록 재빌드 (되돌린 이름으로)
        // 간단하게: 전체 지우기 후 재추가 유도
        TxtStatus.Text += " — 파일 목록을 다시 추가해 주세요.";
        _entries.Clear();
        DropHint.Visibility = Visibility.Visible;
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _entries.Clear();
        _svc.ClearUndo();
        BtnUndo.IsEnabled = false;
        DropHint.Visibility = Visibility.Visible;
        TxtStatus.Text      = "";
        BtnApply.IsEnabled  = false;
    }

    private void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is RenameEntry sel)
        {
            _entries.Remove(sel);
            if (_entries.Count == 0) DropHint.Visibility = Visibility.Visible;
            RefreshPreview();
        }
    }

    // ─────────────────────────────────────────────
    // 상태 갱신 / 설정 저장
    // ─────────────────────────────────────────────

    private void UpdateStatus()
    {
        BtnClear.IsEnabled = _entries.Count > 0;
        DropHint.Visibility = _entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveRecentPattern()
    {
        if (_isPatternMode && !string.IsNullOrEmpty(TxtPattern.Text))
        {
            _settings.RecentPatterns.Remove(TxtPattern.Text);
            _settings.RecentPatterns.Insert(0, TxtPattern.Text);
            if (_settings.RecentPatterns.Count > 10)
                _settings.RecentPatterns.RemoveAt(10);
        }
        SettingsService.Save(_settings);
    }

    protected override void OnClosed(EventArgs e)
    {
        SettingsService.Save(_settings);
        base.OnClosed(e);
    }
}
