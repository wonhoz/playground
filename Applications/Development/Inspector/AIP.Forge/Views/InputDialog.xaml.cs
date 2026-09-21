using AIPForge.Services;

namespace AIPForge.Views;

/// <summary>한 줄 입력 대화상자 — 선택적 검증 함수(오류 메시지 반환, 정상이면 null)</summary>
public partial class InputDialog : Window
{
    private readonly Func<string, string?>? _validate;

    private InputDialog(string prompt, string initial, Func<string, string?>? validate)
    {
        InitializeComponent();
        _validate = validate;
        TxtPrompt.Text = prompt;
        TxtValue.Text = initial;
        DarkTitle.Apply(this);
        Loaded += (_, _) => { TxtValue.Focus(); TxtValue.SelectAll(); };
    }

    public static string? Ask(Window owner, string prompt, string initial = "", Func<string, string?>? validate = null)
    {
        var dlg = new InputDialog(prompt, initial, validate) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.TxtValue.Text.Trim() : null;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        var error = _validate?.Invoke(TxtValue.Text.Trim());
        if (error != null)
        {
            TxtError.Text = error;
            TxtError.Visibility = Visibility.Visible;
            return;
        }
        DialogResult = true;
    }
}
