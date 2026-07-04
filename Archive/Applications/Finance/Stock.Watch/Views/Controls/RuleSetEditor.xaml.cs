using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Stock.Watch.Conditions;
using Condition = Stock.Watch.Conditions.Condition;

namespace Stock.Watch.Views.Controls;

/// <summary>
/// 하나의 <see cref="RuleSet"/>(매수 또는 매도)을 편집하는 컨트롤.
/// 조건 행을 동적으로 추가/삭제하며, 변경 시 <see cref="Changed"/>를 발생시킨다.
/// </summary>
public partial class RuleSetEditor : UserControl
{
    private RuleSet? _rules;
    private bool _suppress;

    /// <summary>조건/결합/활성화가 바뀔 때 발생(자동 저장·재평가용).</summary>
    public event Action? Changed;

    private sealed record Opt(object Value, string Label)
    {
        // 닫힌 콤보(SelectionBox)가 레코드 ToString 대신 라벨을 표시하도록 한다.
        public override string ToString() => Label;
    }

    private static readonly Opt[] OperandOpts =
        Enum.GetValues<Operand>().Select(o => new Opt(o, Condition.OperandLabel(o))).ToArray();
    private static readonly Opt[] OpOpts =
        Enum.GetValues<CompareOp>().Select(o => new Opt(o, Condition.OpLabel(o))).ToArray();
    private static readonly Opt[] RightKindOpts =
    {
        new(RightKind.Constant, "상수값"),
        new(RightKind.Indicator, "지표 × 배수")
    };
    private static readonly Opt[] CombineOpts =
    {
        new(CombineMode.All, "모두 충족 (AND)"),
        new(CombineMode.Any, "하나라도 충족 (OR)")
    };

    public RuleSetEditor()
    {
        InitializeComponent();
        FillCombo(CombineCombo, CombineOpts);
        CombineCombo.SelectionChanged += (_, _) =>
        {
            if (_suppress || _rules == null) return;
            _rules.Combine = (CombineMode)CombineCombo.SelectedValue;
            Changed?.Invoke();
        };
        EnabledCheck.Checked += (_, _) => OnEnabledToggle();
        EnabledCheck.Unchecked += (_, _) => OnEnabledToggle();
        AddBtn.Click += (_, _) =>
        {
            if (_rules == null) return;
            _rules.Conditions.Add(new Condition());
            BuildRows();
            Changed?.Invoke();
        };
    }

    public void Bind(RuleSet rules, string title)
    {
        _rules = rules;
        TitleText.Text = title;
        TitleText.Foreground = title.Contains("매수")
            ? new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50))
            : new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF));

        _suppress = true;
        EnabledCheck.IsChecked = rules.Enabled;
        CombineCombo.SelectedValue = rules.Combine;
        _suppress = false;
        BuildRows();
    }

    private void OnEnabledToggle()
    {
        if (_suppress || _rules == null) return;
        _rules.Enabled = EnabledCheck.IsChecked == true;
        Changed?.Invoke();
    }

    private void BuildRows()
    {
        RowsHost.Children.Clear();
        if (_rules == null) return;
        foreach (var cond in _rules.Conditions.ToList())
            RowsHost.Children.Add(BuildRow(cond));
    }

    private FrameworkElement BuildRow(Condition cond)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var panel = new WrapPanel();
        Grid.SetColumn(panel, 0);

        var leftCombo = MakeCombo(OperandOpts, cond.Left, 96);
        var opCombo = MakeCombo(OpOpts, cond.Op, 82);
        var kindCombo = MakeCombo(RightKindOpts, cond.RightType, 96);
        var rightOperandCombo = MakeCombo(OperandOpts, cond.RightOperand, 96);
        var valueBox = new TextBox { Width = 64, Margin = new Thickness(3), VerticalContentAlignment = VerticalAlignment.Center };
        valueBox.Text = FormatValue(cond.RightValue);

        void SyncRightVisibility()
        {
            bool indicator = cond.RightType == RightKind.Indicator;
            rightOperandCombo.Visibility = indicator ? Visibility.Visible : Visibility.Collapsed;
            valueBox.ToolTip = indicator ? "배수 (예: 2 = 2배)" : "비교값";
        }

        leftCombo.SelectionChanged += (_, _) => { cond.Left = (Operand)leftCombo.SelectedValue; Fire(); };
        opCombo.SelectionChanged += (_, _) => { cond.Op = (CompareOp)opCombo.SelectedValue; Fire(); };
        kindCombo.SelectionChanged += (_, _) =>
        {
            cond.RightType = (RightKind)kindCombo.SelectedValue;
            SyncRightVisibility();
            Fire();
        };
        rightOperandCombo.SelectionChanged += (_, _) => { cond.RightOperand = (Operand)rightOperandCombo.SelectedValue; Fire(); };
        valueBox.TextChanged += (_, _) =>
        {
            if (double.TryParse(valueBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
                || double.TryParse(valueBox.Text, out v))
            {
                cond.RightValue = v;
                Fire();
            }
        };

        panel.Children.Add(leftCombo);
        panel.Children.Add(opCombo);
        panel.Children.Add(kindCombo);
        panel.Children.Add(rightOperandCombo);
        panel.Children.Add(valueBox);
        SyncRightVisibility();

        var del = new Button { Content = "✕", Width = 26, Margin = new Thickness(3), Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Top };
        del.Click += (_, _) =>
        {
            _rules?.Conditions.Remove(cond);
            BuildRows();
            Fire();
        };
        Grid.SetColumn(del, 1);

        grid.Children.Add(panel);
        grid.Children.Add(del);
        return grid;
    }

    private void Fire()
    {
        if (_suppress) return;
        Changed?.Invoke();
    }

    private ComboBox MakeCombo(Opt[] opts, object selected, double width)
    {
        var cb = new ComboBox
        {
            Width = width,
            Margin = new Thickness(3),
            ItemsSource = opts,
            DisplayMemberPath = nameof(Opt.Label),
            SelectedValuePath = nameof(Opt.Value),
            SelectedValue = selected
        };
        return cb;
    }

    private static void FillCombo(ComboBox cb, Opt[] opts)
    {
        cb.ItemsSource = opts;
        cb.DisplayMemberPath = nameof(Opt.Label);
        cb.SelectedValuePath = nameof(Opt.Value);
    }

    private static string FormatValue(double v)
        => v == Math.Floor(v) ? ((long)v).ToString() : v.ToString("0.###", CultureInfo.InvariantCulture);
}
