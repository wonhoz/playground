using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using HashForge.Services;

namespace HashForge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        _initialized = true;

        // 초기값 생성
        TxtUuid.Text = CryptoService.GenerateUuid();
        TxtUlid.Text = CryptoService.GenerateUlid();
        GenPwInternal();

        Loaded += (_, _) =>
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var dark = 1;
            DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));
        };
    }

    // ── 해시 탭 ──────────────────────────────────────────────────
    private void Hash_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_initialized) return;
        var text = TxtHashIn.Text;
        var key  = TxtHmacKey?.Text ?? "";

        TxtMd5.Text    = string.IsNullOrEmpty(text) ? "" : CryptoService.Md5(text);
        TxtSha1.Text   = string.IsNullOrEmpty(text) ? "" : CryptoService.Sha1(text);
        TxtSha256.Text = string.IsNullOrEmpty(text) ? "" : CryptoService.Sha256(text);
        TxtSha512.Text = string.IsNullOrEmpty(text) ? "" : CryptoService.Sha512(text);
        TxtHmac.Text   = !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(key)
            ? CryptoService.HmacSha256(text, key) : "";
    }

    private void CopyMd5(object s, RoutedEventArgs e)    => Copy(TxtMd5.Text);
    private void CopySha1(object s, RoutedEventArgs e)   => Copy(TxtSha1.Text);
    private void CopySha256(object s, RoutedEventArgs e) => Copy(TxtSha256.Text);
    private void CopySha512(object s, RoutedEventArgs e) => Copy(TxtSha512.Text);
    private void CopyHmac(object s, RoutedEventArgs e)   => Copy(TxtHmac.Text);

    // ── 인코딩 탭 ────────────────────────────────────────────────
    private void EncMode_Changed(object s, System.Windows.Controls.SelectionChangedEventArgs e)
        => TxtEncOut.Text = "";

    private void DoEncode(object s, RoutedEventArgs e)
    {
        var input = TxtEncIn.Text;
        TxtEncOut.Text = GetEncMode() switch
        {
            0 => CryptoService.Base64Encode(input),
            1 => CryptoService.UrlEncode(input),
            2 => CryptoService.HexEncode(input),
            3 => CryptoService.HtmlEncode(input),
            _ => input
        };
    }

    private void DoDecode(object s, RoutedEventArgs e)
    {
        var input = TxtEncIn.Text;
        TxtEncOut.Text = GetEncMode() switch
        {
            0 => CryptoService.Base64Decode(input),
            1 => CryptoService.UrlDecode(input),
            2 => CryptoService.HexDecode(input),
            3 => CryptoService.HtmlDecode(input),
            _ => input
        };
    }

    private int GetEncMode() => CmbEncMode.SelectedIndex;
    private void CopyEncOut(object s, RoutedEventArgs e) => Copy(TxtEncOut.Text);

    // ── JWT 탭 ───────────────────────────────────────────────────
    private void Jwt_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_initialized) return;
        var token = TxtJwtIn.Text.Trim();

        if (string.IsNullOrEmpty(token))
        {
            TxtJwtHeader.Text = TxtJwtPayload.Text = TxtJwtSig.Text = TxtJwtInfo.Text = "";
            JwtInfoBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var r = JwtService.Decode(token);
        TxtJwtHeader.Text  = r.Header;
        TxtJwtPayload.Text = r.Payload;
        TxtJwtSig.Text     = r.Signature;

        if (!string.IsNullOrEmpty(r.Info))
        {
            TxtJwtInfo.Text          = r.Info;
            JwtInfoBorder.Visibility = Visibility.Visible;
        }
        else
        {
            JwtInfoBorder.Visibility = Visibility.Collapsed;
        }
    }

    // ── 생성기 탭 ────────────────────────────────────────────────
    private void GenUuid(object s, RoutedEventArgs e) => TxtUuid.Text = CryptoService.GenerateUuid();
    private void GenUlid(object s, RoutedEventArgs e) => TxtUlid.Text = CryptoService.GenerateUlid();
    private void CopyUuid(object s, RoutedEventArgs e) => Copy(TxtUuid.Text);
    private void CopyUlid(object s, RoutedEventArgs e) => Copy(TxtUlid.Text);

    private void PwOptions_Changed(object s, RoutedEventArgs e)
    {
        if (!_initialized) return;
        TxtPwLen.Text = ((int)SldPwLen.Value).ToString();
        GenPwInternal();
    }

    private void PwOptions_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;
        TxtPwLen.Text = ((int)SldPwLen.Value).ToString();
        GenPwInternal();
    }

    private void GenPw(object s, RoutedEventArgs e) => GenPwInternal();
    private void CopyPw(object s, RoutedEventArgs e) => Copy(TxtPwResult.Text);

    private void GenPwInternal()
    {
        if (!_initialized) return;
        int len     = (int)(SldPwLen?.Value ?? 16);
        bool upper  = ChkUpper?.IsChecked  == true;
        bool digits = ChkDigits?.IsChecked == true;
        bool spec   = ChkSpecial?.IsChecked == true;

        var pw = CryptoService.GeneratePassword(len, upper, digits, spec);
        TxtPwResult.Text = pw;
        UpdateStrengthBar(pw);
    }

    private void UpdateStrengthBar(string pw)
    {
        if (Str0 == null) return;
        var (score, label, hex) = CryptoService.PasswordStrength(pw);
        var bars = new[] { Str0, Str1, Str2, Str3 };
        var active = (Color)ColorConverter.ConvertFromString(hex);
        var inactive = Color.FromRgb(0x25, 0x25, 0x40);

        for (int i = 0; i < 4; i++)
            bars[i].Background = new SolidColorBrush(i < score ? active : inactive);

        TxtStrLabel.Text       = label;
        TxtStrLabel.Foreground = new SolidColorBrush(active);
    }

    // ── 유틸 ─────────────────────────────────────────────────────
    private static void Copy(string text)
    {
        if (!string.IsNullOrEmpty(text))
            Clipboard.SetText(text);
    }
}
