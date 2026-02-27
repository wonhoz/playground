using System.Windows;
using System.Windows.Controls;
using TextForge.Services;

namespace TextForge.Views;

public partial class EncodingView : UserControl
{
    public EncodingView() => InitializeComponent();

    private void Encode_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text;
        OutputBox.Text = GetMode() switch
        {
            0 => CryptoService.Base64Encode(input),
            1 => CryptoService.UrlEncode(input),
            2 => CryptoService.HexEncode(input),
            3 => CryptoService.HtmlEncode(input),
            _ => input
        };
        ShowStatus("✓ 인코딩 완료", "#81C784");
    }

    private void Decode_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text;
        OutputBox.Text = GetMode() switch
        {
            0 => CryptoService.Base64Decode(input),
            1 => CryptoService.UrlDecode(input),
            2 => CryptoService.HexDecode(input),
            3 => CryptoService.HtmlDecode(input),
            _ => input
        };
        ShowStatus("✓ 디코딩 완료", "#81C784");
    }

    private void Swap_Click(object sender, RoutedEventArgs e)
    {
        (InputBox.Text, OutputBox.Text) = (OutputBox.Text, InputBox.Text);
    }

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

    private int GetMode()
    {
        if (UrlRadio.IsChecked  == true) return 1;
        if (HexRadio.IsChecked  == true) return 2;
        if (HtmlRadio.IsChecked == true) return 3;
        return 0; // Base64 기본
    }

    private void ShowStatus(string msg, string colorHex)
    {
        StatusText.Text = msg;
        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex)!);
    }
}
