using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using LocaleForge.Models;
using LocaleForge.Parsers;
using LocaleForge.ViewModels;
using Microsoft.Win32;
using WinKey = System.Windows.Input.Key;
using WinKeyboard = System.Windows.Input.Keyboard;
using WinKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WinDragEventArgs = System.Windows.DragEventArgs;
using WinModifiers = System.Windows.Input.ModifierKeys;

namespace LocaleForge;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private ObservableCollection<LocaleEntry> _filteredEntries = new();
    private bool _initialized;

    // ── 다크 타이틀바 ──
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        LstFiles.ItemsSource = _vm.LoadedFiles;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.StatsText))
                TxtStats.Text = _vm.StatsText;
            if (e.PropertyName == nameof(_vm.StatusText))
                TxtStatus.Text = _vm.StatusText;
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _initialized = true;
    }

    // ── 파일 드래그&드롭 ──
    private void Window_DragOver(object sender, WinDragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, WinDragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var file in files)
            LoadFileAndRefresh(file);
    }

    private void LoadFileAndRefresh(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var supported = new[] { ".json", ".yaml", ".yml", ".resx", ".po", ".properties" };
        if (!supported.Contains(ext))
        {
            MessageBox.Show($"지원하지 않는 파일 형식입니다: {ext}", "Locale.Forge",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_vm.LoadFile(filePath))
            RebuildColumns();
    }

    // ── DataGrid 컬럼 동적 생성 ──
    private void RebuildColumns()
    {
        DgEntries.Columns.Clear();

        // 키 컬럼
        var keyCol = new DataGridTextColumn
        {
            Header = "Key",
            Binding = new Binding("Key"),
            IsReadOnly = false,
            Width = new DataGridLength(220),
            MinWidth = 120
        };
        DgEntries.Columns.Add(keyCol);

        // 언어별 컬럼
        foreach (var lang in _vm.Languages)
        {
            var col = new DataGridTemplateColumn
            {
                Header = lang,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 120
            };

            var langCapture = lang;

            // CellTemplate (표시용)
            var cellFactory = new FrameworkElementFactory(typeof(TextBlock));
            var binding = new Binding($".");
            cellFactory.SetValue(TextBlock.MarginProperty, new Thickness(6, 0, 6, 0));
            cellFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            cellFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            // 값을 가져오는 컨버터 대신 코드비하인드에서 처리
            cellFactory.AddHandler(TextBlock.LoadedEvent, new RoutedEventHandler((s, _) =>
            {
                if (s is TextBlock tb && tb.DataContext is LocaleEntry entry)
                {
                    tb.Text = entry.GetValue(langCapture);
                    tb.Foreground = string.IsNullOrEmpty(entry.GetValue(langCapture))
                        ? new SolidColorBrush(Color.FromRgb(0x88, 0x44, 0x44))
                        : new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
                }
            }));
            col.CellTemplate = new DataTemplate { VisualTree = cellFactory };

            // CellEditingTemplate
            var editFactory = new FrameworkElementFactory(typeof(TextBox));
            editFactory.SetValue(TextBox.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x3A)));
            editFactory.SetValue(TextBox.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)));
            editFactory.SetValue(TextBox.BorderThicknessProperty, new Thickness(0));
            editFactory.SetValue(TextBox.PaddingProperty, new Thickness(6, 2, 6, 2));
            editFactory.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center);
            editFactory.AddHandler(TextBox.LoadedEvent, new RoutedEventHandler((s, _) =>
            {
                if (s is TextBox tb && tb.DataContext is LocaleEntry entry)
                {
                    tb.Text = entry.GetValue(langCapture);
                    tb.Tag = langCapture;
                    tb.Focus();
                    tb.SelectAll();
                }
            }));
            col.CellEditingTemplate = new DataTemplate { VisualTree = editFactory };

            DgEntries.Columns.Add(col);
        }

        ApplyFilter();
    }

    // ── 필터 적용 ──
    private void ApplyFilter()
    {
        if (!_initialized) return;

        var search = TxtSearch.Text.Trim().ToLowerInvariant();
        var missingOnly = ChkMissingOnly.IsChecked == true;
        var unusedOnly = ChkUnusedOnly.IsChecked == true;

        var filtered = _vm.Entries.Where(e =>
        {
            if (missingOnly && !e.IsMissing) return false;
            if (unusedOnly && !e.IsUnused) return false;
            if (!string.IsNullOrEmpty(search))
            {
                if (e.Key.ToLowerInvariant().Contains(search)) return true;
                return e.AllValues.Values.Any(v => v.ToLowerInvariant().Contains(search));
            }
            return true;
        }).ToList();

        _filteredEntries = new ObservableCollection<LocaleEntry>(filtered);
        DgEntries.ItemsSource = _filteredEntries;
    }

    // ── 셀 편집 완료 ──
    private void DgEntries_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.Item is not LocaleEntry entry) return;

        if (e.Column is DataGridTextColumn textCol && textCol.Header?.ToString() == "Key")
        {
            if (e.EditingElement is TextBox tb)
            {
                var newKey = tb.Text.Trim();
                if (!string.IsNullOrEmpty(newKey) && newKey != entry.Key)
                {
                    // 키 이름 변경 - 모든 파일에 반영
                    foreach (var file in _vm.LoadedFiles)
                    {
                        if (file.Entries.TryGetValue(entry.Key, out var val))
                        {
                            file.Entries.Remove(entry.Key);
                            file.Entries[newKey] = val;
                        }
                    }
                    entry.Key = newKey;
                }
            }
        }
        else if (e.Column is DataGridTemplateColumn && e.EditingElement is ContentPresenter cp)
        {
            // 언어 컬럼 편집
            var tb = FindVisualChild<TextBox>(cp);
            if (tb != null && tb.Tag is string langCode)
            {
                _vm.SetEntryValue(entry.Key, langCode, tb.Text);
                // 해당 파일 entries 업데이트
                var file = _vm.LoadedFiles.FirstOrDefault(f => f.LanguageCode == langCode);
                if (file != null)
                    file.Entries[entry.Key] = tb.Text;
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var found = FindVisualChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    // ── 선택 변경 → 하단 편집 패널 ──
    private void DgEntries_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DgEntries.SelectedItem is not LocaleEntry entry)
        {
            PnlEdit.Visibility = Visibility.Collapsed;
            return;
        }

        PnlEdit.Visibility = Visibility.Visible;
        TxtSelectedKey.Text = entry.Key;

        var statusParts = new List<string>();
        if (entry.IsMissing) statusParts.Add("⚠ 일부 언어에 누락됨");
        if (entry.IsUnused) statusParts.Add("⚡ 소스코드에서 미사용");
        TxtKeyStatus.Text = statusParts.Count > 0
            ? string.Join("  ", statusParts)
            : "✅ 정상";
        TxtKeyStatus.Foreground = entry.IsMissing
            ? new SolidColorBrush(Colors.OrangeRed)
            : entry.IsUnused
                ? new SolidColorBrush(Colors.Gold)
                : new SolidColorBrush(Color.FromRgb(0x44, 0xCC, 0x88));

        // 언어별 편집 TextBox 생성
        PnlLangEditors.Children.Clear();
        foreach (var lang in _vm.Languages)
        {
            var langCapture = lang;
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock
            {
                Text = lang,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE9, 0x45, 0x60)),
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                FontSize = 11
            };
            Grid.SetColumn(lbl, 0);

            var val = entry.GetValue(lang);
            var tb = new TextBox
            {
                Text = val,
                Height = 26,
                Padding = new Thickness(6, 3, 6, 3),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x3A)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                BorderBrush = string.IsNullOrEmpty(val)
                    ? new SolidColorBrush(Colors.OrangeRed)
                    : new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66)),
                BorderThickness = new Thickness(1),
                Tag = langCapture
            };
            tb.TextChanged += (s, _) =>
            {
                if (s is TextBox t && t.Tag is string lc)
                {
                    var newVal = t.Text;
                    _vm.SetEntryValue(entry.Key, lc, newVal);
                    var file = _vm.LoadedFiles.FirstOrDefault(f => f.LanguageCode == lc);
                    if (file != null) file.Entries[entry.Key] = newVal;
                    t.BorderBrush = string.IsNullOrEmpty(newVal)
                        ? new SolidColorBrush(Colors.OrangeRed)
                        : new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66));
                }
            };
            Grid.SetColumn(tb, 1);

            row.Children.Add(lbl);
            row.Children.Add(tb);
            PnlLangEditors.Children.Add(row);
        }
    }

    // ── 파일 목록 선택 → 파일 정보 표시 ──
    private void LstFiles_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    // ── 파일 제거 버튼 ──
    private void BtnRemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is LocaleFile file)
        {
            _vm.RemoveFile(file);
            RebuildColumns();
        }
    }

    // ── 파일 열기 ──
    private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "i18n 파일 선택",
            Filter = ParserRegistry.AllFilesFilter,
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;
        foreach (var f in dlg.FileNames)
            LoadFileAndRefresh(f);
    }

    // ── 모두 저장 ──
    private void BtnSaveAll_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.LoadedFiles.Count == 0)
        {
            MessageBox.Show("저장할 파일이 없습니다.", "Locale.Forge");
            return;
        }
        foreach (var file in _vm.LoadedFiles)
            _vm.SaveFile(file);
        TxtStatus.Text = $"모두 저장 완료 ({_vm.LoadedFiles.Count}개 파일)";
        MessageBox.Show("모든 파일이 저장되었습니다.", "Locale.Forge",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── 키 추가 ──
    private void BtnAddKey_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddKeyDialog { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var key = dlg.NewKey.Trim();
        if (string.IsNullOrEmpty(key)) return;
        _vm.AddKey(key);
        ApplyFilter();
    }

    // ── 키 삭제 ──
    private void BtnDeleteKey_Click(object sender, RoutedEventArgs e)
    {
        if (DgEntries.SelectedItem is not LocaleEntry entry) return;
        var result = MessageBox.Show($"키 '{entry.Key}'를 삭제할까요?\n모든 언어에서 제거됩니다.",
            "키 삭제", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        foreach (var file in _vm.LoadedFiles)
            file.Entries.Remove(entry.Key);
        _vm.DeleteKey(entry.Key);
        ApplyFilter();
        PnlEdit.Visibility = Visibility.Collapsed;
    }

    // ── 미사용 키 탐지 ──
    private void BtnScanSource_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "소스 코드 폴더를 선택하세요 (미사용 키 탐지용)"
        };
        if (dlg.ShowDialog() != true) return;

        var folder = dlg.FolderName;
        var exts = new[] { ".cs", ".ts", ".tsx", ".js", ".jsx", ".vue", ".java", ".py", ".swift", ".kt" };

        TxtStatus.Text = "소스 코드 스캔 중...";
        Task.Run(() =>
        {
            var usedKeys = new HashSet<string>();
            var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()));

            foreach (var srcFile in files)
            {
                try
                {
                    var content = File.ReadAllText(srcFile);
                    foreach (var entry in _vm.Entries)
                    {
                        if (content.Contains(entry.Key))
                            usedKeys.Add(entry.Key);
                    }
                }
                catch { /* 읽기 실패 무시 */ }
            }

            Dispatcher.Invoke(() =>
            {
                _vm.MarkUnused(usedKeys);
                ApplyFilter();
                TxtStatus.Text = $"스캔 완료: {_vm.UnusedCount}개 미사용 키 발견";
                TxtStats.Text = _vm.StatsText;
            });
        });
    }

    // ── 변환 내보내기 ──
    private void BtnExportAs_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.LoadedFiles.Count == 0)
        {
            MessageBox.Show("먼저 파일을 로드하세요.", "Locale.Forge");
            return;
        }

        var dlg = new ExportDialog(_vm) { Owner = this };
        dlg.ShowDialog();
    }

    // ── 검색/필터 ──
    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        ApplyFilter();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        ApplyFilter();
    }

    // ── 키보드 단축키 ──
    protected override void OnKeyDown(WinKeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == WinKey.O && WinKeyboard.Modifiers == WinModifiers.Control)
            BtnOpenFile_Click(this, new RoutedEventArgs());
        else if (e.Key == WinKey.S && WinKeyboard.Modifiers == WinModifiers.Control)
            BtnSaveAll_Click(this, new RoutedEventArgs());
    }
}
