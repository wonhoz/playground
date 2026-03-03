using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using TableCraft.Models;
using TableCraft.Services;
using TableCraft.Views;

namespace TableCraft;

public partial class MainWindow : Window
{
    // ── 데이터 ──────────────────────────────────────────────────────
    private string[]   _headers = [];
    private string[][] _rows    = [];
    private ColumnType[] _types = [];
    private string?    _filePath;

    // ── 서비스 ──────────────────────────────────────────────────────
    private readonly QueryEngine  _engine       = new();
    private readonly FilteredList _filteredList = new();

    // ── 상태 ────────────────────────────────────────────────────────
    private int[]  _indices    = [];
    private CancellationTokenSource? _queryCts;
    private List<ColumnDef> _columns = [];

    // ── Win32 다크 타이틀바 ─────────────────────────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    // ── FilteredList: 가상 스크롤 어댑터 ───────────────────────────
    private sealed class FilteredList : IList<string[]>, IList, INotifyCollectionChanged
    {
        private string[][] _src = [];
        private int[]      _idx = [];

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public void Update(string[][] src, int[] idx)
        {
            _src = src;
            _idx = idx;
            CollectionChanged?.Invoke(this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        // IList<string[]>
        public int    Count      => _idx.Length;
        public bool   IsReadOnly => true;

        public string[] this[int index]
        {
            get => _src[_idx[index]];
            set => throw new NotSupportedException();
        }

        public IEnumerator<string[]> GetEnumerator()
        {
            for (int i = 0; i < _idx.Length; i++)
                yield return _src[_idx[i]];
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Contains(string[] item) => Array.Exists(_idx, i => _src[i] == item);
        public int  IndexOf(string[] item)  => Array.FindIndex(_idx, i => _src[i] == item);

        public void CopyTo(string[][] array, int arrayIndex)
        {
            for (int i = 0; i < _idx.Length; i++)
                array[arrayIndex + i] = _src[_idx[i]];
        }

        public void Add(string[] item)             => throw new NotSupportedException();
        public void Clear()                        => throw new NotSupportedException();
        public void Insert(int index, string[] item) => throw new NotSupportedException();
        public bool Remove(string[] item)          => throw new NotSupportedException();
        public void RemoveAt(int index)            => throw new NotSupportedException();

        // IList (non-generic)
        object? IList.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException();
        }
        bool IList.IsFixedSize              => false;
        bool IList.IsReadOnly               => true;
        bool ICollection.IsSynchronized     => false;
        object ICollection.SyncRoot         => this;

        int  IList.Add(object? value)           => throw new NotSupportedException();
        bool IList.Contains(object? value)      => value is string[] r && Contains(r);
        int  IList.IndexOf(object? value)       => value is string[] r ? IndexOf(r) : -1;
        void IList.Insert(int index, object? v) => throw new NotSupportedException();
        void IList.Remove(object? value)        => throw new NotSupportedException();
        void ICollection.CopyTo(Array array, int index)
        {
            for (int i = 0; i < _idx.Length; i++)
                array.SetValue(_src[_idx[i]], index + i);
        }
    }

    // ────────────────────────────────────────────────────────────────
    public MainWindow() => InitializeComponent();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바 적용
        var hwnd   = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int dark   = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        Dg.ItemsSource = _filteredList;

        // 드래그 앤 드롭
        Drop += Window_Drop;
    }

    // ── 파일 열기 ───────────────────────────────────────────────────
    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter      = "CSV/TSV 파일|*.csv;*.tsv;*.txt|모든 파일|*.*",
            Title       = "CSV/TSV 파일 열기"
        };
        if (dlg.ShowDialog() == true)
            _ = LoadFileAsync(dlg.FileName);
    }

    private void BtnRecent_Click(object sender, RoutedEventArgs e)
    {
        var recent = CsvParser.GetRecentFiles();
        if (recent.Count == 0)
        {
            MessageBox.Show("최근 파일이 없습니다.", "Table.Craft",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var menu = new ContextMenu();
        foreach (var path in recent)
        {
            var item = new MenuItem { Header = path };
            item.Click += (_, _) => _ = LoadFileAsync(path);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    // ── 드래그 앤 드롭 ──────────────────────────────────────────────
    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            _ = LoadFileAsync(files[0]);
    }

    // ── 파일 로드 ───────────────────────────────────────────────────
    private async Task LoadFileAsync(string path)
    {
        TxtStatus.Text = $"로딩 중: {Path.GetFileName(path)} ...";
        TxtRowInfo.Text = "";
        Dg.Columns.Clear();
        _filteredList.Update([], []);

        try
        {
            var progress = new Progress<(int Rows, long Bytes)>(p =>
                TxtStatus.Text = $"로딩 중: {p.Rows:N0}행 읽음...");

            var (headers, rows, _) = await CsvParser.LoadAsync(path, progress);
            var types = CsvParser.InferTypes(rows, headers.Length);

            _headers  = headers;
            _rows     = rows;
            _types    = types;
            _filePath = path;

            _engine.Load(headers, rows, types);
            BuildColumns();

            _indices = await Task.Run(() => _engine.Compute());
            _filteredList.Update(_rows, _indices);

            CsvParser.AddRecentFile(path);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"로드 실패: {ex.Message}";
        }
    }

    // ── 컬럼 구성 ───────────────────────────────────────────────────
    private void BuildColumns()
    {
        Dg.Columns.Clear();
        _columns.Clear();

        var template = (DataTemplate)Resources["ColHdrTemplate"];

        for (int i = 0; i < _headers.Length; i++)
        {
            var col = new ColumnDef
            {
                Index = i,
                Name  = _headers[i],
                Type  = _types[i]
            };
            col.FilterChanged += OnColumnFilterChanged;
            _columns.Add(col);

            var dgCol = new DataGridTextColumn
            {
                Header         = col,
                HeaderTemplate = template,
                Binding        = new Binding($"[{i}]"),
                Width          = new DataGridLength(120),
                MinWidth       = 60,
            };
            Dg.Columns.Add(dgCol);
        }

        // 집계 패널 컬럼 목록 업데이트
        CmbAggCol.ItemsSource    = _columns;
        CmbAggCol.SelectedIndex  = _columns.Count > 0 ? 0 : -1;
    }

    // ── 빠른 필터 변경 ──────────────────────────────────────────────
    private void OnColumnFilterChanged(int colIndex, string text)
    {
        if (!IsLoaded) return;
        _engine.SetQuickFilter(colIndex, text);
        _ = ApplyQueryAsync();
    }

    // ── 필터 TextBox 클릭 → 이벤트 버블링 차단 (DataGrid 클릭 이벤트 방지) ──
    private void FilterBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = false;  // 텍스트박스 포커스는 허용, 상위 전파는 막지 않음
    }

    // ── 정렬 버튼 클릭 ──────────────────────────────────────────────
    private void SortBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ColumnDef col) return;

        bool additive = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        var newSort = col.Sort is SortState.None or SortState.Desc
            ? SortState.Asc
            : SortState.Desc;

        if (!additive)
            foreach (var c in _columns) c.Sort = SortState.None;

        col.Sort = newSort;
        _engine.SetSort(col.Index, newSort, additive);
        _ = ApplyQueryAsync();
    }

    // ── 필터/정렬 적용 ──────────────────────────────────────────────
    private async Task ApplyQueryAsync()
    {
        _queryCts?.Cancel();
        _queryCts = new CancellationTokenSource();
        var ct = _queryCts.Token;

        try
        {
            var indices = await Task.Run(() => _engine.Compute(ct), ct);
            if (!ct.IsCancellationRequested)
            {
                _indices = indices;
                _filteredList.Update(_rows, indices);
                UpdateStatus();
                RefreshAggregate();
            }
        }
        catch (OperationCanceledException) { }
    }

    // ── 상태바 업데이트 ─────────────────────────────────────────────
    private void UpdateStatus()
    {
        if (_filePath is null) return;

        int total    = _rows.Length;
        int filtered = _indices.Length;
        string fn    = Path.GetFileName(_filePath);

        TxtStatus.Text = $"{fn}  |  {_headers.Length}컬럼";

        int fCount = _engine.Filters.Count + _columns.Count(c => c.FilterText.Length > 0);
        string filterInfo = fCount > 0 ? $"  [필터: {fCount}개]" : "";
        TxtRowInfo.Text   = $"{filtered:N0} / {total:N0} 행{filterInfo}";
    }

    // ── 집계 패널 ───────────────────────────────────────────────────
    private void Dg_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        if (Dg.SelectedCells.Count <= 0) return;
        var cell = Dg.SelectedCells[0];
        if (cell.Column is DataGridTextColumn tc && tc.Header is ColumnDef cd)
        {
            CmbAggCol.SelectedItem = cd;
            ShowAggregate(cd.Index);
        }
    }

    private void CmbAggCol_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CmbAggCol.SelectedItem is ColumnDef cd)
            ShowAggregate(cd.Index);
    }

    private void ShowAggregate(int colIndex)
    {
        if (colIndex < 0 || _rows.Length == 0)
        {
            AggContent.Visibility  = Visibility.Collapsed;
            TxtAggEmpty.Visibility = Visibility.Visible;
            return;
        }

        var r = _engine.Aggregate(colIndex, _indices);

        TxtCount.Text    = r.Count.ToString("N0");
        TxtDistinct.Text = r.Distinct.ToString("N0");
        TxtEmpty.Text    = r.Empty.ToString("N0");
        TxtMin.Text      = r.Min;
        TxtMax.Text      = r.Max;

        if (r.IsNumeric)
        {
            TxtSum.Text          = r.Sum.ToString("G10");
            TxtAvg.Text          = r.Avg.ToString("G6");
            NumericPanel.Visibility = Visibility.Visible;
        }
        else
        {
            NumericPanel.Visibility = Visibility.Collapsed;
        }

        AggContent.Visibility  = Visibility.Visible;
        TxtAggEmpty.Visibility = Visibility.Collapsed;
    }

    private void RefreshAggregate()
    {
        if (CmbAggCol.SelectedItem is ColumnDef cd)
            ShowAggregate(cd.Index);
    }

    // ── 고급 필터 ───────────────────────────────────────────────────
    private void BtnFilter_Click(object sender, RoutedEventArgs e)
    {
        if (_headers.Length == 0) return;

        var dlg = new FilterDialog(_headers, _engine.Filters.ToList()) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _engine.ClearFilters();
            foreach (var f in dlg.Result)
                _engine.AddFilter(f);
            _ = ApplyQueryAsync();
        }
    }

    private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
    {
        _engine.ClearFilters();
        foreach (var c in _columns) c.FilterText = "";
        _ = ApplyQueryAsync();
    }

    private void BtnClearSort_Click(object sender, RoutedEventArgs e)
    {
        _engine.ClearSort();
        foreach (var c in _columns) c.Sort = SortState.None;
        _ = ApplyQueryAsync();
    }

    // ── 계산 컬럼 ───────────────────────────────────────────────────
    private void BtnCalcCol_Click(object sender, RoutedEventArgs e)
    {
        if (_headers.Length == 0) return;

        var dlg = new CalcColDialog(_headers) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        string colName = dlg.ColumnName;
        string expr    = dlg.Expression;

        // 컬럼 이름 중복 확인
        if (_columns.Any(c => c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show($"컬럼 '{colName}'이 이미 존재합니다.", "Table.Craft",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 수식 평가 결과를 새 컬럼으로 추가
        int newIdx = _headers.Length;
        _headers = [.. _headers, colName];
        _types   = [.. _types, ColumnType.Text];

        // 모든 행에 수식 결과 추가
        var newRows = new string[_rows.Length][];
        for (int i = 0; i < _rows.Length; i++)
        {
            var val = _engine.EvalExpression(expr, _rows[i]);
            newRows[i] = [.. _rows[i], val];
        }
        _rows = newRows;

        _engine.Load(_headers, _rows, _types);
        BuildColumns();
        _ = ApplyQueryAsync();
    }

    // ── 피벗 ────────────────────────────────────────────────────────
    private void BtnPivot_Click(object sender, RoutedEventArgs e)
    {
        if (_headers.Length == 0) return;

        var win = new PivotView(_engine, _headers, _indices) { Owner = this };
        win.Show();
    }

    // ── 내보내기 ─────────────────────────────────────────────────────
    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_headers.Length == 0) return;

        var dlg = new SaveFileDialog
        {
            Filter      = "CSV 파일|*.csv|TSV 파일|*.tsv|모든 파일|*.*",
            DefaultExt  = ".csv",
            FileName    = Path.GetFileNameWithoutExtension(_filePath) + "_export"
        };
        if (dlg.ShowDialog() != true) return;

        char delimiter = dlg.FilterIndex == 2 ? '\t' : ',';

        try
        {
            await ExportService.ExportAsync(dlg.FileName, _headers, _rows, _indices, delimiter);
            MessageBox.Show($"내보내기 완료!\n{_indices.Length:N0}행 저장됨",
                "Table.Craft", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"내보내기 실패: {ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 키보드 단축키 ────────────────────────────────────────────────
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            BtnOpen_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            BtnFilter_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
        {
            BtnExport_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }
}
