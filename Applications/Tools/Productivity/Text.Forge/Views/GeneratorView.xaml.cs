using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TextForge.Services;

namespace TextForge.Views;

public partial class GeneratorView : UserControl
{
    private bool _initialized;

    public GeneratorView()
    {
        InitializeComponent();
        _initialized = true;

        // 초기값 생성
        UuidBox.Text = CryptoService.GenerateUuid();
        UlidBox.Text = CryptoService.GenerateUlid();
        GenPwInternal();
    }

    // ── UUID / ULID ──────────────────────────────────────────────
    private void GenUuid_Click(object sender, RoutedEventArgs e)
        => UuidBox.Text = CryptoService.GenerateUuid();

    private void GenUlid_Click(object sender, RoutedEventArgs e)
        => UlidBox.Text = CryptoService.GenerateUlid();

    private void CopyUuid_Click(object sender, RoutedEventArgs e)
        => Copy(UuidBox.Text);

    private void CopyUlid_Click(object sender, RoutedEventArgs e)
        => Copy(UlidBox.Text);

    // ── 비밀번호 생성기 ──────────────────────────────────────────
    private void PwOptions_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        LengthLabel.Text = ((int)LengthSlider.Value).ToString();
        GenPwInternal();
    }

    private void PwOptions_Changed(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;
        LengthLabel.Text = ((int)LengthSlider.Value).ToString();
        GenPwInternal();
    }

    private void GenPw_Click(object sender, RoutedEventArgs e) => GenPwInternal();

    private void CopyPw_Click(object sender, RoutedEventArgs e) => Copy(PwResultBox.Text);

    private void GenPwInternal()
    {
        if (!_initialized) return;
        int len     = (int)(LengthSlider?.Value ?? 16);
        bool upper  = ChkUpper?.IsChecked  == true;
        bool digits = ChkDigits?.IsChecked == true;
        bool spec   = ChkSpecial?.IsChecked == true;

        var pw = CryptoService.GeneratePassword(len, upper, digits, spec);
        PwResultBox.Text = pw;
        UpdateStrengthBar(pw);
    }

    private void UpdateStrengthBar(string pw)
    {
        if (Str0 == null) return;
        var (score, label, hex) = CryptoService.PasswordStrength(pw);
        var bars    = new[] { Str0, Str1, Str2, Str3 };
        var active  = (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        var inactive = Color.FromRgb(0x25, 0x25, 0x40);

        for (int i = 0; i < 4; i++)
            bars[i].Background = new SolidColorBrush(i < score ? active : inactive);

        StrLabel.Text       = label;
        StrLabel.Foreground = new SolidColorBrush(active);
    }

    // ── 유틸 ─────────────────────────────────────────────────────
    private static void Copy(string text)
    {
        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }
}
