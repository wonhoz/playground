using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WinEvent.Windows;

public partial class AlertRulesWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private readonly AlertService _alertService;
    private List<AlertRule> _rules = [];
    private AlertRule? _current;
    private bool _updating;

    public AlertRulesWindow(AlertService alertService)
    {
        InitializeComponent();
        _alertService = alertService;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int));

        _rules = _alertService.Rules.Select(r => new AlertRule
        {
            Name           = r.Name,
            Level          = r.Level,
            EventId        = r.EventId,
            SourcePattern  = r.SourcePattern,
            MessagePattern = r.MessagePattern,
            IsEnabled      = r.IsEnabled
        }).ToList();

        LbRules.ItemsSource = _rules;
    }

    private void LbRules_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _current = LbRules.SelectedItem as AlertRule;
        PanelEdit.IsEnabled = _current is not null;
        if (_current is null) return;

        _updating = true;
        TxtRuleName.Text    = _current.Name;
        TxtRuleEventId.Text = _current.EventId?.ToString() ?? "";
        TxtRuleSource.Text  = _current.SourcePattern ?? "";
        TxtRuleMessage.Text = _current.MessagePattern ?? "";
        ChkRuleEnabled.IsChecked = _current.IsEnabled;

        // 레벨 선택
        int levelTag = _current.Level ?? -1;
        foreach (ComboBoxItem item in CmbLevel.Items)
        {
            if (item.Tag is string t && int.TryParse(t, out int tVal) && tVal == levelTag)
            { CmbLevel.SelectedItem = item; break; }
        }
        if (levelTag == -1) CmbLevel.SelectedIndex = 0;
        _updating = false;
    }

    private void RuleField_Changed(object sender, RoutedEventArgs e) => SyncToRule();
    private void RuleField_Changed(object sender, TextChangedEventArgs e) => SyncToRule();
    private void RuleField_Changed(object sender, SelectionChangedEventArgs e) => SyncToRule();

    private void SyncToRule()
    {
        if (_updating || _current is null) return;
        _current.Name = TxtRuleName.Text.Trim();

        int levelTag = -1;
        if (CmbLevel.SelectedItem is ComboBoxItem ci && ci.Tag is string t)
            int.TryParse(t, out levelTag);
        _current.Level = levelTag == -1 ? null : levelTag;

        _current.EventId = long.TryParse(TxtRuleEventId.Text.Trim(), out long eid) ? eid : null;
        _current.SourcePattern  = string.IsNullOrWhiteSpace(TxtRuleSource.Text)  ? null : TxtRuleSource.Text.Trim();
        _current.MessagePattern = string.IsNullOrWhiteSpace(TxtRuleMessage.Text) ? null : TxtRuleMessage.Text.Trim();
        _current.IsEnabled      = ChkRuleEnabled.IsChecked == true;

        // 목록 표시 갱신
        LbRules.Items.Refresh();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var rule = new AlertRule { Name = "새 규칙", IsEnabled = true };
        _rules.Add(rule);
        LbRules.Items.Refresh();
        LbRules.SelectedItem = rule;
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        _rules.Remove(_current);
        _current = null;
        LbRules.Items.Refresh();
        PanelEdit.IsEnabled = false;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _alertService.SetRules(_rules);
        DialogResult = true;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
