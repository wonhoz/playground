using System.Data;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using TableCraft.Models;
using TableCraft.Services;

namespace TableCraft.Views;

public partial class PivotView : Window
{
    private readonly QueryEngine _engine;
    private readonly string[]    _headers;
    private int[]                _indices;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public PivotView(QueryEngine engine, string[] headers, int[] indices)
    {
        InitializeComponent();
        _engine  = engine;
        _headers = headers;
        _indices = indices;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        // 컬럼 목록 채우기 (첫 항목은 "(없음)")
        var colItems = new List<object> { new { Name = "(없음)", Index = -1 } };
        for (int i = 0; i < _headers.Length; i++)
            colItems.Add(new { Name = _headers[i], Index = i });

        CmbRow.ItemsSource = colItems;
        CmbCol.ItemsSource = colItems;
        CmbVal.ItemsSource = colItems;

        CmbRow.SelectedIndex = _headers.Length > 0 ? 1 : 0;
        CmbCol.SelectedIndex = _headers.Length > 1 ? 2 : 0;
        CmbVal.SelectedIndex = 0;
        CmbAgg.SelectedIndex = 0;
    }

    private void Config_Changed(object sender, SelectionChangedEventArgs e)
    {
        // 설정 변경 시 자동 갱신 안 함 — 버튼 클릭으로만 갱신
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        BuildPivot();
    }

    private void BuildPivot()
    {
        int rowIdx = GetSelectedIndex(CmbRow);
        int colIdx = GetSelectedIndex(CmbCol);
        int valIdx = GetSelectedIndex(CmbVal);

        if (rowIdx < 0 || colIdx < 0)
        {
            TxtPivotStatus.Text = "행 필드와 열 필드를 선택하세요";
            return;
        }
        if (rowIdx == colIdx)
        {
            TxtPivotStatus.Text = "행 필드와 열 필드는 서로 달라야 합니다";
            return;
        }

        var aggMap = new[]
        {
            PivotAgg.Count, PivotAgg.Sum, PivotAgg.Avg,
            PivotAgg.Min,   PivotAgg.Max
        };
        var cfg = new PivotConfig
        {
            RowColumnIndex = rowIdx,
            ColColumnIndex = colIdx,
            ValColumnIndex = valIdx,
            Aggregation    = CmbAgg.SelectedIndex >= 0
                             ? aggMap[CmbAgg.SelectedIndex]
                             : PivotAgg.Count
        };

        var (rowKeys, colKeys, matrix) = _engine.BuildPivot(cfg, _indices);

        if (rowKeys.Length == 0)
        {
            TxtPivotStatus.Text = "피벗 결과 없음";
            PivotGrid.ItemsSource = null;
            return;
        }

        // DataTable 로 변환 (DataGrid AutoGenerateColumns 활용)
        var dt = new DataTable();
        dt.Columns.Add(_headers[rowIdx]);
        foreach (var ck in colKeys)
            dt.Columns.Add(ck);

        for (int r = 0; r < rowKeys.Length; r++)
        {
            var dr = dt.NewRow();
            dr[0] = rowKeys[r];
            for (int c = 0; c < colKeys.Length; c++)
                dr[c + 1] = matrix[r, c];
            dt.Rows.Add(dr);
        }

        PivotGrid.ItemsSource = dt.DefaultView;

        TxtPivotStatus.Text =
            $"{rowKeys.Length}행  ×  {colKeys.Length}열  |  집계: {CmbAgg.Text}";
    }

    private static int GetSelectedIndex(ComboBox cmb)
    {
        if (cmb.SelectedItem is null) return -1;
        // 익명 타입의 Index 프로퍼티 리플렉션으로 읽기
        var prop = cmb.SelectedItem.GetType().GetProperty("Index");
        return prop?.GetValue(cmb.SelectedItem) is int idx ? idx : -1;
    }
}
