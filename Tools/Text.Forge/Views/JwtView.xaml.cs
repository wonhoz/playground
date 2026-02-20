using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace TextForge.Views;

public partial class JwtView : UserControl
{
    public JwtView() => InitializeComponent();

    private void Token_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 자동 디코딩
        Decode();
    }

    private void Decode_Click(object sender, RoutedEventArgs e) => Decode();

    private void Decode()
    {
        var token = TokenBox.Text.Trim();
        if (string.IsNullOrEmpty(token))
        {
            ClearOutputs();
            return;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            ShowStatus("✕ 유효한 JWT 형식이 아닙니다 (aaa.bbb.ccc)", "#EF9A9A");
            return;
        }

        try
        {
            HeaderBox.Text    = PrettyDecodeBase64Url(parts[0]);
            PayloadBox.Text   = PrettyDecodeBase64Url(parts[1]);
            SignatureBox.Text  = parts[2];

            // Payload에서 exp, iat 추출
            ShowClaimsInfo(parts[1]);

            ShowStatus("✓ 디코딩 완료", "#81C784");
        }
        catch (Exception ex)
        {
            ShowStatus($"✕ {ex.Message}", "#EF9A9A");
        }
    }

    private static string PrettyDecodeBase64Url(string base64Url)
    {
        // Base64Url → Base64 변환
        var base64 = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        // 패딩 추가
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "=";  break;
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));

        // Pretty Print
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement,
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }

    private void ShowClaimsInfo(string payloadBase64)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(
                payloadBase64
                    .Replace('-', '+')
                    .Replace('_', '/')
                    .PadRight(payloadBase64.Length + (4 - payloadBase64.Length % 4) % 4, '=')));

            using var doc = JsonDocument.Parse(json);

            ExpLabel.Text = doc.RootElement.TryGetProperty("exp", out var exp)
                ? $"exp : {DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()):yyyy-MM-dd HH:mm:ss (KST+9)}"
                : "exp : (없음)";

            IatLabel.Text = doc.RootElement.TryGetProperty("iat", out var iat)
                ? $"iat : {DateTimeOffset.FromUnixTimeSeconds(iat.GetInt64()):yyyy-MM-dd HH:mm:ss}"
                : "iat : (없음)";
        }
        catch
        {
            ExpLabel.Text = string.Empty;
            IatLabel.Text = string.Empty;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        TokenBox.Text   = string.Empty;
        ClearOutputs();
        StatusText.Text = string.Empty;
    }

    private void ClearOutputs()
    {
        HeaderBox.Text    = string.Empty;
        PayloadBox.Text   = string.Empty;
        SignatureBox.Text  = string.Empty;
        ExpLabel.Text     = string.Empty;
        IatLabel.Text     = string.Empty;
    }

    private void ShowStatus(string msg, string colorHex)
    {
        StatusText.Text = msg;
        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex)!);
    }
}
