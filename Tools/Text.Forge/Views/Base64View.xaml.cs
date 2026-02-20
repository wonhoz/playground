using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace TextForge.Views;

public partial class Base64View : UserControl
{
    public Base64View() => InitializeComponent();

    private void Encode_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text;
        if (string.IsNullOrEmpty(input)) return;
        try
        {
            OutputBox.Text = Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
            ShowStatus("✓ 인코딩 완료", "#81C784");
        }
        catch (Exception ex) { ShowStatus($"✕ {ex.Message}", "#EF9A9A"); }
    }

    private void Decode_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;
        try
        {
            OutputBox.Text = Encoding.UTF8.GetString(Convert.FromBase64String(input));
            ShowStatus("✓ 디코딩 완료", "#81C784");
        }
        catch { ShowStatus("✕ 유효하지 않은 Base64 문자열입니다", "#EF9A9A"); }
    }

    private void Swap_Click(object sender, RoutedEventArgs e)
        => (InputBox.Text, OutputBox.Text) = (OutputBox.Text, InputBox.Text);

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(OutputBox.Text))
        {
            Clipboard.SetText(OutputBox.Text);
            ShowStatus("✓ 클립보드에 복사됨", "#81C784");
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        InputBox.Text   = string.Empty;
        OutputBox.Text  = string.Empty;
        StatusText.Text = string.Empty;
    }

    private void ShowStatus(string msg, string colorHex)
    {
        StatusText.Text = msg;
        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex)!);
    }
}
