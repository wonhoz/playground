using System.Windows.Controls;

namespace Prompt.Forge.Views;

public partial class FillVarsDialog : Window
{
    readonly List<string>              _varNames;
    readonly Dictionary<string, TextBox> _inputs = [];
    readonly string                    _templateContent;

    public string? FilledContent { get; private set; }
    public Dictionary<string, string> LastInputValues { get; private set; } = [];

    public FillVarsDialog(string templateContent, List<string> varNames,
                          Dictionary<string, string>? prevValues = null)
    {
        InitializeComponent();
        _varNames        = varNames;
        _templateContent = templateContent;

        Loaded += (_, _) => App.ApplyDarkTitleBar(this);

        BuildInputs(varNames, prevValues);
    }

    void BuildInputs(List<string> varNames, Dictionary<string, string>? prevValues)
    {
        foreach (var name in varNames)
        {
            VarPanel.Children.Add(new Label { Content = $"{{{{ {name} }}}}" });
            var tb = new TextBox { Tag = name };
            if (prevValues != null && prevValues.TryGetValue(name, out var prev))
                tb.Text = prev;
            tb.TextChanged += (_, _) => UpdatePreview();
            // Enter: 다음 변수 필드로 이동, 마지막이면 OK 실행
            tb.KeyDown += (_, e) =>
            {
                if (e.Key != System.Windows.Input.Key.Enter) return;
                var idx = _varNames.IndexOf(name);
                if (idx >= 0 && idx < _varNames.Count - 1)
                    _inputs[_varNames[idx + 1]].Focus();
                else
                    Ok_Click(this, new RoutedEventArgs());
                e.Handled = true;
            };
            _inputs[name] = tb;
            VarPanel.Children.Add(tb);
        }
        UpdatePreview();
    }

    void UpdatePreview()
    {
        var content = _templateContent;
        foreach (var (name, tb) in _inputs)
            content = content.Replace($"{{{{{name}}}}}", string.IsNullOrEmpty(tb.Text) ? $"{{{{{name}}}}}" : tb.Text);
        TxtPreview.Text = content;
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        var unfilled = _inputs.Where(kv => string.IsNullOrEmpty(kv.Value.Text))
                              .Select(kv => $"{{{{{kv.Key}}}}}").ToList();
        if (unfilled.Count > 0)
        {
            var names = string.Join(", ", unfilled);
            var r = MessageBox.Show(
                $"입력하지 않은 변수가 있습니다:\n{names}\n\n그대로 복사하시겠습니까?",
                "미입력 변수", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
        }

        var content = _templateContent;
        foreach (var (name, tb) in _inputs)
            content = content.Replace($"{{{{{name}}}}}", tb.Text);

        FilledContent   = content;
        LastInputValues = _inputs.ToDictionary(kv => kv.Key, kv => kv.Value.Text);

        try { System.Windows.Clipboard.SetText(content); }
        catch { }

        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
