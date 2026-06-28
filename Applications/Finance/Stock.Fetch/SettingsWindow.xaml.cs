using System.Windows;
using Stock.Fetch.Services;

namespace Stock.Fetch;

public partial class SettingsWindow : Window
{
    private readonly AppConfig _config;

    public SettingsWindow(AppConfig config)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _config = config;

        KeyBox.Text = config.AppKey;
        SecretBox.Password = config.AppSecret;
        MockCheck.IsChecked = config.UseMockServer;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _config.AppKey = KeyBox.Text.Trim();
        _config.AppSecret = SecretBox.Password.Trim();
        _config.UseMockServer = MockCheck.IsChecked == true;
        // 자격 변경 시 캐시 토큰 무효화
        _config.CachedToken = string.Empty;
        _config.TokenExpiresAt = DateTime.MinValue;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
