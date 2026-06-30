using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Stock.Fetch.Services;

namespace Stock.Fetch;

public partial class SettingsWindow : Window
{
    private readonly AppConfig _config;
    private readonly SlackNotifier? _slack;

    public SettingsWindow(AppConfig config, SlackNotifier? slack = null)
    {
        InitializeComponent();
        NativeTheme.ApplyDarkTitleBar(this);
        _config = config;
        _slack = slack;

        KeyBox.Text = config.AppKey;
        SecretBox.Password = config.AppSecret;
        MockCheck.IsChecked = config.UseMockServer;
        SelectMarketDiv(config.KisMarketDiv);
        PortfolioPathBox.Text = config.PortfolioPath;

        FinnhubKeyBox.Text = config.FinnhubApiKey;
        AlpacaKeyIdBox.Text = config.AlpacaApiKeyId;
        AlpacaSecretBox.Password = config.AlpacaApiSecret;

        SlackUrlBox.Text = config.SlackWebhookUrl;
        SlackChannelBox.Text = config.SlackChannel;
        MonitorCheck.IsChecked = config.MonitorEnabled;
        MarketHoursCheck.IsChecked = config.MonitorMarketHoursOnly;
        IntervalBox.Text = config.MonitorIntervalSeconds.ToString();
        ThresholdsBox.Text = string.Join(", ", config.AlertThresholds);
        FailAlertBox.Text = config.FetchFailAlertThreshold.ToString();
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        // 입력 중인 값으로 테스트(저장 전). 임시로 config에 반영해 SlackNotifier가 읽게 한다.
        _config.SlackWebhookUrl = SlackUrlBox.Text.Trim();
        _config.SlackChannel = SlackChannelBox.Text.Trim();
        if (_slack is null) { TestResult.Text = "테스트를 사용할 수 없습니다."; return; }
        TestResult.Text = "전송 중…";
        try
        {
            await _slack.SendTestAsync();
            TestResult.Text = "✓ 전송 성공 — Slack을 확인하세요.";
        }
        catch (Exception ex)
        {
            TestResult.Text = "⚠ 전송 실패: " + ex.Message;
        }
    }

    private void BrowsePortfolio_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "자산 포트폴리오 저장 경로 선택",
            Filter = "포트폴리오 JSON (*.json)|*.json|모든 파일 (*.*)|*.*",
            DefaultExt = ".json",
            FileName = "portfolio.json",
            OverwritePrompt = false,
            CheckPathExists = true
        };

        var current = PortfolioPathBox.Text.Trim();
        if (!string.IsNullOrEmpty(current))
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(current);
                if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                    dlg.InitialDirectory = dir;
                var file = System.IO.Path.GetFileName(current);
                if (!string.IsNullOrEmpty(file)) dlg.FileName = file;
            }
            catch { /* 경로 파싱 실패 시 기본값 사용 */ }
        }

        if (dlg.ShowDialog(this) == true)
            PortfolioPathBox.Text = dlg.FileName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _config.AppKey = KeyBox.Text.Trim();
        _config.AppSecret = SecretBox.Password.Trim();
        _config.UseMockServer = MockCheck.IsChecked == true;
        _config.KisMarketDiv = (MarketDivCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "UN";
        _config.PortfolioPath = PortfolioPathBox.Text.Trim();

        _config.FinnhubApiKey = FinnhubKeyBox.Text.Trim();
        _config.AlpacaApiKeyId = AlpacaKeyIdBox.Text.Trim();
        _config.AlpacaApiSecret = AlpacaSecretBox.Password.Trim();

        _config.SlackWebhookUrl = SlackUrlBox.Text.Trim();
        _config.SlackChannel = SlackChannelBox.Text.Trim();
        _config.MonitorEnabled = MonitorCheck.IsChecked == true;
        _config.MonitorMarketHoursOnly = MarketHoursCheck.IsChecked == true;
        if (int.TryParse(IntervalBox.Text.Trim(), out var sec)) _config.MonitorIntervalSeconds = Math.Max(10, sec);
        _config.AlertThresholds = ParseThresholds(ThresholdsBox.Text);
        if (int.TryParse(FailAlertBox.Text.Trim(), out var fail)) _config.FetchFailAlertThreshold = Math.Max(0, fail);

        // 자격 변경 시 캐시 토큰 무효화
        _config.CachedToken = string.Empty;
        _config.TokenExpiresAt = DateTime.MinValue;
        DialogResult = true;
    }

    private static List<double> ParseThresholds(string text)
    {
        var list = text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? Math.Abs(d) : -1)
            .Where(d => d > 0)
            .Distinct()
            .OrderBy(d => d)
            .ToList();
        return list.Count > 0 ? list : new List<double> { 2, 5, 7, 10, 12 };
    }

    private void SelectMarketDiv(string code)
    {
        string target = string.IsNullOrWhiteSpace(code) ? "UN" : code.Trim().ToUpperInvariant();
        foreach (ComboBoxItem it in MarketDivCombo.Items)
            if ((it.Tag as string) == target) { MarketDivCombo.SelectedItem = it; return; }
        MarketDivCombo.SelectedIndex = 0;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
