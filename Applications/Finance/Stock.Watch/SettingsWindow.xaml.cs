using System.Windows;
using Stock.Watch.Services;

namespace Stock.Watch;

public partial class SettingsWindow : Window
{
    private readonly AppConfig _config;

    public SettingsWindow(AppConfig config)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _config = config;

        AppKeyBox.Text = config.AppKey;
        AppSecretBox.Password = config.AppSecret;
        MockCheck.IsChecked = config.UseMockServer;
        SlackBox.Text = config.SlackWebhookUrl;
        PollBox.Text = config.PollIntervalSeconds.ToString();
        CooldownBox.Text = config.AlertCooldownSeconds.ToString();
        MarketHoursCheck.IsChecked = config.MarketHoursOnly;
        RealtimeCheck.IsChecked = config.UseRealtime;

        TestSlackBtn.Click += TestSlack;
        SaveBtn.Click += Save;
        CancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
    }

    private async void TestSlack(object sender, RoutedEventArgs e)
    {
        var temp = new AppConfig { SlackWebhookUrl = SlackBox.Text.Trim() };
        using var notifier = new SlackNotifier(temp);
        try
        {
            TestSlackBtn.IsEnabled = false;
            await notifier.SendTestAsync();
            MessageBox.Show("테스트 메시지를 전송했습니다. Slack 채널을 확인하세요.", "Stock.Watch",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"전송 실패: {ex.Message}", "Stock.Watch", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { TestSlackBtn.IsEnabled = true; }
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        _config.AppKey = AppKeyBox.Text.Trim();
        _config.AppSecret = AppSecretBox.Password.Trim();
        _config.UseMockServer = MockCheck.IsChecked == true;
        _config.SlackWebhookUrl = SlackBox.Text.Trim();
        _config.PollIntervalSeconds = ParseInt(PollBox.Text, _config.PollIntervalSeconds, 5, 3600);
        _config.AlertCooldownSeconds = ParseInt(CooldownBox.Text, _config.AlertCooldownSeconds, 0, 86400);
        _config.MarketHoursOnly = MarketHoursCheck.IsChecked == true;
        _config.UseRealtime = RealtimeCheck.IsChecked == true;

        // 자격 변경 시 캐시 토큰·approval_key 무효화
        _config.CachedToken = string.Empty;
        _config.TokenExpiresAt = DateTime.MinValue;
        _config.CachedApprovalKey = string.Empty;
        _config.ApprovalExpiresAt = DateTime.MinValue;

        _config.Save();
        DialogResult = true;
        Close();
    }

    private static int ParseInt(string text, int fallback, int min, int max)
        => int.TryParse(text, out var v) ? Math.Clamp(v, min, max) : fallback;
}
