using System.Windows;
using System.Windows.Controls;
using TextForge.Services;

namespace TextForge.Views;

public partial class HashView : UserControl
{
    private bool _initialized;

    public HashView()
    {
        InitializeComponent();
        _initialized = true;
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e) => Compute();

    private void HmacKey_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        var hasKey = !string.IsNullOrEmpty(HmacKeyBox.Text);
        HmacBorder.Opacity = hasKey ? 1.0 : 0.4;
        Compute();
    }

    private void Compute()
    {
        var text = InputBox.Text;
        var key  = HmacKeyBox?.Text ?? "";

        if (string.IsNullOrEmpty(text))
        {
            Md5Box.Text = Sha1Box.Text = Sha256Box.Text = Sha512Box.Text = HmacBox.Text = "";
            return;
        }

        Md5Box.Text    = CryptoService.Md5(text);
        Sha1Box.Text   = CryptoService.Sha1(text);
        Sha256Box.Text = CryptoService.Sha256(text);
        Sha512Box.Text = CryptoService.Sha512(text);
        HmacBox.Text   = !string.IsNullOrEmpty(key) ? CryptoService.HmacSha256(text, key) : "";
    }

    private void CopyMd5_Click(object s, RoutedEventArgs e)    => Copy(Md5Box.Text);
    private void CopySha1_Click(object s, RoutedEventArgs e)   => Copy(Sha1Box.Text);
    private void CopySha256_Click(object s, RoutedEventArgs e) => Copy(Sha256Box.Text);
    private void CopySha512_Click(object s, RoutedEventArgs e) => Copy(Sha512Box.Text);
    private void CopyHmac_Click(object s, RoutedEventArgs e)   => Copy(HmacBox.Text);

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text;
        if (string.IsNullOrEmpty(text)) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"MD5:      {Md5Box.Text}");
        sb.AppendLine($"SHA-1:    {Sha1Box.Text}");
        sb.AppendLine($"SHA-256:  {Sha256Box.Text}");
        sb.AppendLine($"SHA-512:  {Sha512Box.Text}");
        if (!string.IsNullOrEmpty(HmacBox.Text))
            sb.AppendLine($"HMAC-256: {HmacBox.Text}");

        Clipboard.SetText(sb.ToString().TrimEnd());
        ShowStatus("✓ 전체 복사됨", "#81C784");
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        InputBox.Text   = string.Empty;
        HmacKeyBox.Text = string.Empty;
        StatusText.Text = string.Empty;
    }

    private void Copy(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
            ShowStatus("✓ 클립보드에 복사됨", "#81C784");
        }
    }

    private void ShowStatus(string msg, string colorHex)
    {
        StatusText.Text = msg;
        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex)!);
    }
}
