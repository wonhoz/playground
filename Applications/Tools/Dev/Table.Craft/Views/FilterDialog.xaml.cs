using System.Windows;
using System.Windows.Controls;
using TableCraft.Models;

namespace TableCraft.Views;

public partial class FilterDialog : Window
{
    private readonly string[]  _headers;
    private readonly List<FilterCondition> _conditions;

    public IReadOnlyList<FilterCondition> Result { get; private set; } = [];

    // 연산자 문자열 ↔ FilterOperator 매핑
    private static readonly (string Label, FilterOperator Op)[] OperatorMap =
    [
        ("포함",            FilterOperator.Contains),
        ("포함 안함",       FilterOperator.NotContains),
        ("같음",            FilterOperator.Equals),
        ("같지 않음",       FilterOperator.NotEquals),
        ("시작",            FilterOperator.StartsWith),
        ("끝",              FilterOperator.EndsWith),
        ("보다 큼",         FilterOperator.GreaterThan),
        ("보다 작음",       FilterOperator.LessThan),
        ("범위 (Between)",  FilterOperator.Between),
        ("비어있음",        FilterOperator.IsEmpty),
        ("비어있지 않음",   FilterOperator.IsNotEmpty),
        ("정규식",          FilterOperator.Regex),
    ];

    public FilterDialog(string[] headers, List<FilterCondition> existing)
    {
        InitializeComponent();
        _headers    = headers;
        _conditions = existing.Select(c => new FilterCondition
        {
            ColumnIndex   = c.ColumnIndex,
            ColumnName    = c.ColumnName,
            Operator      = c.Operator,
            Value         = c.Value,
            Value2        = c.Value2,
            CaseSensitive = c.CaseSensitive
        }).ToList();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        foreach (var cond in _conditions)
            AddRow(cond);
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    // ── 행 생성 ──────────────────────────────────────────────────────
    private void AddRow(FilterCondition? seed = null)
    {
        var cond = seed ?? new FilterCondition
        {
            ColumnIndex = 0,
            ColumnName  = _headers.Length > 0 ? _headers[0] : "",
            Operator    = FilterOperator.Contains
        };

        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

        // 컬럼 선택 ComboBox
        var cmbCol = new ComboBox { Height = 26, Margin = new Thickness(0, 0, 4, 0) };
        for (int i = 0; i < _headers.Length; i++)
            cmbCol.Items.Add(new ComboBoxItem { Content = _headers[i] });
        cmbCol.SelectedIndex = Math.Clamp(cond.ColumnIndex, 0, _headers.Length - 1);
        cmbCol.SelectionChanged += (_, _) =>
        {
            cond.ColumnIndex = cmbCol.SelectedIndex;
            cond.ColumnName  = _headers[cmbCol.SelectedIndex];
        };
        Grid.SetColumn(cmbCol, 0);
        row.Children.Add(cmbCol);

        // 연산자 ComboBox
        var cmbOp = new ComboBox { Height = 26, Margin = new Thickness(0, 0, 4, 0) };
        foreach (var (label, _) in OperatorMap)
            cmbOp.Items.Add(new ComboBoxItem { Content = label });
        int opIdx = Array.FindIndex(OperatorMap, t => t.Op == cond.Operator);
        cmbOp.SelectedIndex = opIdx >= 0 ? opIdx : 0;
        Grid.SetColumn(cmbOp, 1);
        row.Children.Add(cmbOp);

        // 값1 TextBox
        var txtVal = new TextBox
        {
            Height      = 26,
            Margin      = new Thickness(0, 0, 4, 0),
            Text        = cond.Value,
            FontFamily  = new System.Windows.Media.FontFamily("Consolas"),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        txtVal.TextChanged += (_, _) => cond.Value = txtVal.Text;
        Grid.SetColumn(txtVal, 2);
        row.Children.Add(txtVal);

        // 값2 TextBox (Between 전용)
        var txtVal2 = new TextBox
        {
            Height      = 26,
            Margin      = new Thickness(0, 0, 4, 0),
            Text        = cond.Value2,
            FontFamily  = new System.Windows.Media.FontFamily("Consolas"),
            VerticalContentAlignment = VerticalAlignment.Center,
            Visibility  = cond.Operator == FilterOperator.Between
                            ? Visibility.Visible : Visibility.Collapsed,
            ToolTip     = "Between 범위의 끝값"
        };
        txtVal2.TextChanged += (_, _) => cond.Value2 = txtVal2.Text;
        Grid.SetColumn(txtVal2, 3);
        row.Children.Add(txtVal2);

        // 연산자 변경 시 값2 표시 토글
        cmbOp.SelectionChanged += (_, _) =>
        {
            if (cmbOp.SelectedIndex >= 0)
            {
                cond.Operator       = OperatorMap[cmbOp.SelectedIndex].Op;
                txtVal2.Visibility  = cond.Operator == FilterOperator.Between
                                       ? Visibility.Visible : Visibility.Collapsed;
                var needsVal = cond.Operator is not FilterOperator.IsEmpty
                                              and not FilterOperator.IsNotEmpty;
                txtVal.IsEnabled = needsVal;
            }
        };

        // 대소문자 구분 CheckBox
        var chk = new CheckBox
        {
            IsChecked           = cond.CaseSensitive,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            ToolTip             = "대소문자 구분"
        };
        chk.Checked   += (_, _) => cond.CaseSensitive = true;
        chk.Unchecked += (_, _) => cond.CaseSensitive = false;
        Grid.SetColumn(chk, 4);
        row.Children.Add(chk);

        // 삭제 버튼
        var btnDel = new Button
        {
            Content    = "✕",
            Width      = 28,
            Height     = 26,
            Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(0xF3, 0x8B, 0xA8)),
            ToolTip    = "이 조건 삭제"
        };
        btnDel.Click += (_, _) =>
        {
            _conditions.Remove(cond);
            FilterStack.Children.Remove(row);
        };
        Grid.SetColumn(btnDel, 5);
        row.Children.Add(btnDel);

        // 조건 목록에 추가 (신규일 때만)
        if (seed is null) _conditions.Add(cond);

        FilterStack.Children.Add(row);
    }

    // ── 이벤트 핸들러 ────────────────────────────────────────────────
    private void BtnAdd_Click(object sender, RoutedEventArgs e) => AddRow();

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Result = _conditions.ToList();
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
