using System.Runtime.InteropServices;
using System.Windows.Interop;
using HexPeek.Dialogs;

namespace HexPeek;

public partial class MainWindow : Window
{
    // ── DWM 다크 타이틀바 ────────────────────────────────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ── 상태 ─────────────────────────────────────────────────────────────
    private HexDocument? _doc;
    private HexRowList?  _rows;
    private long         _searchOffset  = 0;
    private bool         _structVisible = false;

    // 구조체 필드 표시용 래퍼
    private sealed record FieldViewModel(long OffsetVal, int Length, string Name, string Description)
    {
        public string OffsetHex => $"0x{OffsetVal:X6}";
    }

    public MainWindow()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Drop     += OnDrop;
        DragOver += OnDragOver;
        KeyDown  += OnKeyDown;
    }

    // ── 초기화 ───────────────────────────────────────────────────────────
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }

    // ── 드래그 앤 드롭 ───────────────────────────────────────────────────
    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (files.Length > 0) OpenFile(files[0]);
    }

    // ── 키보드 ───────────────────────────────────────────────────────────
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var mod = e.KeyboardDevice.Modifiers;
        if (mod == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.G: ShowGotoDialog(); e.Handled = true; break;
                case Key.F: TxtSearch.Focus(); TxtSearch.SelectAll(); e.Handled = true; break;
                case Key.S: SaveFile(); e.Handled = true; break;
            }
        }
    }

    // ── 파일 열기 ────────────────────────────────────────────────────────
    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "파일 열기", Filter = "모든 파일 (*.*)|*.*" };
        if (dlg.ShowDialog() == true) OpenFile(dlg.FileName);
    }

    private void OpenFile(string path)
    {
        try
        {
            _doc?.Dispose();
            _doc         = HexDocument.Load(path);
            _rows        = new HexRowList(_doc);
            _searchOffset = 0;

            HexList.ItemsSource = _rows;
            HexList.ScrollIntoView(HexList.Items[0]);

            var fi       = new FileInfo(path);
            bool readOnly = _doc.Length > 50 * 1024 * 1024;
            Title        = $"Hex.Peek — {fi.Name}{(readOnly ? " [읽기 전용]" : "")}";
            TxtFileInfo.Text = $"{_doc.Length:N0} bytes  |  {FormatSize(_doc.Length)}  |  {_doc.RowCount:N0} rows";
            TxtStatus.Text   = path;

            BtnSave.IsEnabled     = !readOnly;
            BtnTemplate.IsEnabled = true;

            DecodePanel.Visibility = Visibility.Visible;
            ResetDecodePanel();

            // 구조체 자동 분석
            var format = StructureParser.DetectFormat(_doc);
            if (format != "Unknown") ShowStructureFields(format);
            else HideStructPanel();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파일을 열 수 없습니다:\n{ex.Message}", "Hex.Peek",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 저장 ─────────────────────────────────────────────────────────────
    private void BtnSave_Click(object sender, RoutedEventArgs e) => SaveFile();

    private void SaveFile()
    {
        if (_doc == null || !_doc.IsDirty) return;
        try
        {
            _doc.Save();
            BtnSave.IsEnabled = false;
            TxtStatus.Text    = "저장 완료";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패:\n{ex.Message}", "Hex.Peek", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 비교 모드 ────────────────────────────────────────────────────────
    private void BtnCompare_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "비교할 파일 열기", Filter = "모든 파일 (*.*)|*.*" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var other = HexDocument.Load(dlg.FileName);
            var win   = new CompareWindow(_doc!, other);
            win.Owner = this;
            win.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"비교 파일을 열 수 없습니다:\n{ex.Message}", "Hex.Peek",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 오프셋 이동 ──────────────────────────────────────────────────────
    private void BtnGoto_Click(object sender, RoutedEventArgs e) => ShowGotoDialog();

    private void ShowGotoDialog()
    {
        if (_doc == null) return;
        var dlg = new GotoDialog { Owner = this };
        if (dlg.ShowDialog() != true) return;

        long offset = dlg.Offset;
        if (offset < 0 || offset >= _doc.Length)
        {
            TxtStatus.Text = $"오프셋 0x{offset:X8}은 파일 범위를 벗어납니다 (크기: {_doc.Length})";
            return;
        }

        NavigateToOffset(offset);
    }

    // ── 구조체 분석 ──────────────────────────────────────────────────────
    private void BtnTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null) return;
        _structVisible = !_structVisible;
        if (_structVisible)
        {
            var format = StructureParser.DetectFormat(_doc);
            ShowStructureFields(format);
        }
        else HideStructPanel();
    }

    private void ShowStructureFields(string format)
    {
        var fields = StructureParser.Parse(_doc!);
        if (fields.Count == 0) { HideStructPanel(); return; }

        TxtFormatName.Text = format;
        LvFields.ItemsSource = fields.Select(f => new FieldViewModel(f.Offset, f.Length, f.Name, f.Description)).ToList();
        ColStruct.Width = new GridLength(380);
        _structVisible  = true;
    }

    private void HideStructPanel()
    {
        ColStruct.Width = new GridLength(0);
        _structVisible  = false;
    }

    // 구조체 필드 선택 → 헥스 뷰 해당 오프셋으로 이동
    private void LvFields_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LvFields.SelectedItem is not FieldViewModel fv) return;
        NavigateToOffset(fv.OffsetVal);
    }

    // ── 행 선택 → 디코드 패널 ────────────────────────────────────────────
    private void HexList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (HexList.SelectedItem is not HexRow row) return;

        UpdateDecodePanel(row);
        TxtStatus.Text = $"오프셋: 0x{row.Offset:X8}  ({row.Offset:N0})  |  {row.Count} bytes  |  Row {HexList.SelectedIndex + 1}";
    }

    // 더블클릭 → 편집 다이얼로그 (Phase 2)
    private void HexList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (HexList.SelectedItem is not HexRow row) return;
        // TODO: Phase 2 — 바이트 편집
        TxtStatus.Text = $"편집 기능은 준비 중입니다 (Offset: 0x{row.Offset:X8})";
    }

    // ── 디코드 패널 ──────────────────────────────────────────────────────
    private void UpdateDecodePanel(HexRow row)
    {
        var b = row.Bytes;
        int n = row.Count;

        TxtInt8.Text   = n >= 1 ? ((sbyte)b[0]).ToString()                         : "—";
        TxtUInt8.Text  = n >= 1 ? b[0].ToString()                                  : "—";
        TxtInt16Le.Text = n >= 2 ? BitConverter.ToInt16(b, 0).ToString()           : "—";
        TxtInt16Be.Text = n >= 2 ? ((short)((b[0] << 8) | b[1])).ToString()       : "—";
        TxtInt32Le.Text = n >= 4 ? BitConverter.ToInt32(b, 0).ToString()           : "—";
        TxtInt32Be.Text = n >= 4 ? ((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]).ToString() : "—";
        TxtInt64Le.Text = n >= 8 ? BitConverter.ToInt64(b, 0).ToString()           : "—";
        TxtFloat.Text   = n >= 4 ? BitConverter.ToSingle(b, 0).ToString("G7")     : "—";
        TxtDouble.Text  = n >= 8 ? BitConverter.ToDouble(b, 0).ToString("G10")    : "—";
    }

    private void ResetDecodePanel()
    {
        TxtInt8.Text = TxtUInt8.Text = TxtInt16Le.Text = TxtInt16Be.Text = "—";
        TxtInt32Le.Text = TxtInt32Be.Text = TxtInt64Le.Text = TxtFloat.Text = TxtDouble.Text = "—";
    }

    // ── 검색 ─────────────────────────────────────────────────────────────
    private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Shift) SearchPrev();
            else                                                   SearchNext();
            e.Handled = true;
        }
    }

    private void BtnSearchNext_Click(object sender, RoutedEventArgs e) => SearchNext();
    private void BtnSearchPrev_Click(object sender, RoutedEventArgs e) => SearchPrev();

    private void SearchNext()
    {
        if (_doc == null || string.IsNullOrWhiteSpace(TxtSearch.Text)) return;
        var pattern = GetSearchPattern();
        if (pattern == null) return;

        var mode = GetSearchMode();
        long result = mode == SearchMode.Regex
            ? SearchService.SearchRegex(_doc, TxtSearch.Text.Trim(), _searchOffset + 1, forward: true)
            : SearchService.Search(_doc, pattern, _searchOffset + 1, forward: true);

        if (result >= 0) { _searchOffset = result; NavigateToOffset(result); TxtStatus.Text = $"검색 결과: 0x{result:X8}  ({result:N0})"; }
        else             { TxtStatus.Text = "더 이상 검색 결과가 없습니다. (처음부터 다시 검색: Enter)"; }
    }

    private void SearchPrev()
    {
        if (_doc == null || string.IsNullOrWhiteSpace(TxtSearch.Text)) return;
        var pattern = GetSearchPattern();
        if (pattern == null) return;

        var mode = GetSearchMode();
        long result = mode == SearchMode.Regex
            ? SearchService.SearchRegex(_doc, TxtSearch.Text.Trim(), _searchOffset - 1, forward: false)
            : SearchService.Search(_doc, pattern, _searchOffset - 1, forward: false);

        if (result >= 0) { _searchOffset = result; NavigateToOffset(result); TxtStatus.Text = $"검색 결과: 0x{result:X8}  ({result:N0})"; }
        else             { TxtStatus.Text = "이전 검색 결과가 없습니다."; }
    }

    private SearchMode GetSearchMode()
    {
        return ((ComboBoxItem)CboSearchMode.SelectedItem)?.Content?.ToString() switch
        {
            "ASCII" => SearchMode.Ascii,
            "Regex" => SearchMode.Regex,
            _       => SearchMode.Hex
        };
    }

    private byte[]? GetSearchPattern()
    {
        var mode = GetSearchMode();
        var text = TxtSearch.Text.Trim();
        try
        {
            return mode switch
            {
                SearchMode.Ascii => Encoding.ASCII.GetBytes(text),
                SearchMode.Regex => Encoding.ASCII.GetBytes(text), // placeholder
                _                => SearchService.ParseHex(text)
            };
        }
        catch
        {
            TxtStatus.Text = "검색어 형식이 올바르지 않습니다 (HEX: FF D8 FF)";
            return null;
        }
    }

    // ── 네비게이션 헬퍼 ──────────────────────────────────────────────────
    private void NavigateToOffset(long offset)
    {
        if (_doc == null || _rows == null) return;
        long rowIndex = offset / 16;
        if (rowIndex >= _rows.Count) return;

        // VirtualizingStackPanel: ScrollIntoView + SelectedIndex
        HexList.SelectedIndex = (int)rowIndex;
        HexList.ScrollIntoView(HexList.SelectedItem);
        _searchOffset = offset;
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────
    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024               => $"{bytes} B",
        < 1024 * 1024        => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:F1} MB",
        _                    => $"{bytes / 1024.0 / 1024 / 1024:F2} GB"
    };
}
