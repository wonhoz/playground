using KeyMacro.Services;

namespace KeyMacro.Views;

/// <summary>다크 테마 알림 · 확인 대화상자</summary>
public partial class DarkDialog : Window
{
    private DarkDialog(string message, string title, bool confirm, string okText)
    {
        InitializeComponent();
        Title = title;
        TxtMessage.Text = message;
        BtnOk.Content = okText;
        BtnCancel.Visibility = confirm ? Visibility.Visible : Visibility.Collapsed;
        DarkTitle.Apply(this);
    }

    /// <summary>단순 알림</summary>
    public static void Info(Window owner, string message, string title = "Key.Macro")
        => new DarkDialog(message, title, false, "확인") { Owner = owner }.ShowDialog();

    /// <summary>예/아니오 확인 — 확인을 누르면 true</summary>
    public static bool Confirm(Window owner, string message, string okText = "확인", string title = "Key.Macro")
        => new DarkDialog(message, title, true, okText) { Owner = owner }.ShowDialog() == true;

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
