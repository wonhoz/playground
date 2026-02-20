using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace TextForge.Views;

public partial class HashView : UserControl
{
    public HashView() => InitializeComponent();

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 실시간 해시 계산
        GenerateHash();
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => GenerateHash();

    private void GenerateHash()
    {
        var input = InputBox.Text;
        if (string.IsNullOrEmpty(input))
        {
            HashOutput.Text = string.Empty;
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(input);

        if (Md5Radio.IsChecked == true)
        {
            AlgoLabel.Text = "MD5";
            HashOutput.Text = ComputeHash(MD5.Create(), bytes);
        }
        else if (Sha1Radio.IsChecked == true)
        {
            AlgoLabel.Text = "SHA-1";
            HashOutput.Text = ComputeHash(SHA1.Create(), bytes);
        }
        else if (Sha256Radio.IsChecked == true)
        {
            AlgoLabel.Text = "SHA-256";
            HashOutput.Text = ComputeHash(SHA256.Create(), bytes);
        }
        else if (Sha512Radio.IsChecked == true)
        {
            AlgoLabel.Text = "SHA-512";
            HashOutput.Text = ComputeHash(SHA512.Create(), bytes);
        }
    }

    private static string ComputeHash(HashAlgorithm algo, byte[] data)
    {
        using (algo)
        {
            var hash = algo.ComputeHash(data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(HashOutput.Text))
        {
            Clipboard.SetText(HashOutput.Text);
            ShowStatus("✓ 클립보드에 복사됨", "#81C784");
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        InputBox.Text   = string.Empty;
        HashOutput.Text = string.Empty;
        StatusText.Text = string.Empty;
    }

    private void ShowStatus(string msg, string colorHex)
    {
        StatusText.Text = msg;
        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex)!);
    }
}
