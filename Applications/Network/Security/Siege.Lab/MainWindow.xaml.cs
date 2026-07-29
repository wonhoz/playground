using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SiegeLab.Models;
using SiegeLab.Services;

namespace SiegeLab;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private readonly SafetyGate _gate = new();
    private bool _loading = true;
    private string _activeUrl = "";

    private readonly LoadTestEngine _loadEngine = new();
    private readonly SecurityAuditor _auditor = new();
    private readonly HealthMonitor _health = new();
    private readonly VulnScanner _scanner = new();
    private readonly LoginResilienceTester _login = new();

    private CancellationTokenSource? _loadCts, _healthCts, _scanCts, _loginCts;

    private List<SecurityFinding> _reportFindings = [];
    private LoadSnapshot? _lastLoad;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        var dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        _loading = false;
        RefreshTargets();

        // 최초 실행 편의: 대상 프로파일 폼에 예시 URL 미리 채움(허가 서약은 사용자가 직접).
        if (_gate.Profiles.Count == 0)
        {
            txtProfName.Text = "i-pacs";
            txtProfUrl.Text = "https://i-pacs.net";
        }
        Log("준비됨 — 대상 탭에서 소유·허가 서약된 프로파일을 등록하세요.");
    }

    // ── 대상 관리 ────────────────────────────────────────────────
    private void RefreshTargets()
    {
        gridTargets.ItemsSource = null;
        gridTargets.ItemsSource = _gate.Profiles.ToList();

        var prev = _activeUrl;
        cboTarget.Items.Clear();
        foreach (var p in _gate.Profiles)
            cboTarget.Items.Add(new ComboBoxItem { Content = $"{p.Name} — {p.BaseUrl}", Tag = p });
        if (cboTarget.Items.Count > 0)
            cboTarget.SelectedIndex = 0;
        else
            UpdateAuthBadge();
    }

    private void OnTargetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (cboTarget.SelectedItem is ComboBoxItem { Tag: TargetProfile p })
        {
            _activeUrl = p.BaseUrl;
            PrefillUrls(p.BaseUrl);
            UpdateAuthBadge();
        }
    }

    private void PrefillUrls(string baseUrl)
    {
        void Set(TextBox tb) { if (string.IsNullOrWhiteSpace(tb.Text)) tb.Text = baseUrl; }
        Set(txtLoadUrl); Set(txtHealthUrl);
        if (string.IsNullOrWhiteSpace(txtLoginUrl.Text))
            txtLoginUrl.Text = baseUrl.TrimEnd('/') + "/login";
    }

    private void UpdateAuthBadge()
    {
        bool ok = !string.IsNullOrEmpty(_activeUrl) && _gate.IsAuthorized(_activeUrl);
        txtAuthBadge.Text = ok ? "인가됨 ✓" : "미인가";
        txtAuthBadge.Foreground = (System.Windows.Media.Brush)
            Application.Current.Resources[ok ? "OkBrush" : "CriticalBrush"];
    }

    private void OnTargetGridSelect(object sender, SelectionChangedEventArgs e)
    {
        if (gridTargets.SelectedItem is TargetProfile p)
        {
            txtProfName.Text = p.Name;
            txtProfUrl.Text = p.BaseUrl;
            txtProfAuth.Text = p.AuthHeader;
            chkPledge.IsChecked = p.Authorized;
        }
    }

    private void OnSaveTarget(object sender, RoutedEventArgs e)
    {
        var url = txtProfUrl.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            MessageBox.Show("올바른 http(s) URL을 입력하세요.", "Siege.Lab",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (chkPledge.IsChecked != true)
        {
            MessageBox.Show("소유·허가 서약에 체크해야 능동 테스트 대상으로 등록됩니다.",
                "Siege.Lab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _gate.AddOrUpdate(new TargetProfile
        {
            Name = string.IsNullOrWhiteSpace(txtProfName.Text) ? uri.Host : txtProfName.Text.Trim(),
            BaseUrl = url,
            AuthHeader = txtProfAuth.Text.Trim(),
            Authorized = true
        });
        _activeUrl = url;
        RefreshTargets();
        Log($"대상 등록됨: {uri.Host}");
    }

    private void OnDeleteTarget(object sender, RoutedEventArgs e)
    {
        if (gridTargets.SelectedItem is TargetProfile p)
        {
            _gate.Remove(p);
            RefreshTargets();
            Log($"대상 삭제됨: {p.BaseUrl}");
        }
    }

    private bool Guard(string url)
    {
        var (ok, reason) = _gate.Validate(url);
        if (!ok)
        {
            MessageBox.Show(reason, "안전 게이트", MessageBoxButton.OK, MessageBoxImage.Stop);
            Log("차단됨: " + reason);
        }
        return ok;
    }

    private static string ComboText(ComboBox c) =>
        (c.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "GET";

    private static int ParseInt(string s, int fallback) =>
        int.TryParse(s.Trim(), out var v) ? v : fallback;

    private void Log(string msg) =>
        txtLog.Text = $"[{DateTime.Now:HH:mm:ss}] {msg}";

    // ── 부하 테스트 ──────────────────────────────────────────────
    private async void OnLoadStart(object sender, RoutedEventArgs e)
    {
        var url = txtLoadUrl.Text.Trim();
        if (!Guard(url)) return;

        var cfg = new LoadTestConfig
        {
            Url = url,
            Method = ComboText(cboLoadMethod),
            Concurrency = ParseInt(txtConc.Text, 20),
            DurationSeconds = ParseInt(txtDur.Text, 30),
            RampUpSeconds = ParseInt(txtRamp.Text, 5),
            MaxRps = ParseInt(txtRps.Text, 0),
            TimeoutMs = ParseInt(txtTimeout.Text, 10_000),
            Body = txtLoadBody.Text,
            ExtraHeader = txtLoadHeaders.Text
        };
        int totalDur = SafetyGate.ClampDuration(cfg.DurationSeconds);

        _loadCts = new CancellationTokenSource();
        btnLoadStart.IsEnabled = false; btnLoadStop.IsEnabled = true;
        Log($"부하 테스트 시작 — {SafetyGate.HostOf(url)} (동시 {cfg.Concurrency}, {totalDur}s)");

        var progress = new Progress<LoadSnapshot>(s =>
        {
            _lastLoad = s;
            pbLoad.Value = Math.Min(100, s.ElapsedSec / totalDur * 100);
            lvElapsed.Text = s.ElapsedSec.ToString("F0");
            lvTotal.Text = s.Total.ToString("N0");
            lvRps.Text = s.CurrentRps.ToString("F0");
            lvWorkers.Text = s.ActiveWorkers.ToString();
            lvSuccess.Text = s.Success.ToString("N0");
            lvFailed.Text = s.Failed.ToString("N0");
            lvAvgRps.Text = s.AvgRps.ToString("F0");
            lvMbps.Text = s.MbPerSec.ToString("F2");
            lvP50.Text = s.LatP50.ToString("F0");
            lvP95.Text = s.LatP95.ToString("F0");
            lvP99.Text = s.LatP99.ToString("F0");
            lvMax.Text = s.LatMax.ToString("F0");
            txtStatusDist.Text = FormatDist(s.StatusDist);
            txtErrDist.Text = FormatDist(s.ErrorDist);
        });

        try
        {
            await _loadEngine.RunAsync(cfg, progress, _loadCts.Token);
            Log($"부하 테스트 완료 — 총 {_lastLoad?.Total:N0}건, 평균 {_lastLoad?.AvgRps:F0} RPS");
        }
        catch (OperationCanceledException) { Log("부하 테스트 중지됨"); }
        catch (Exception ex) { Log("부하 오류: " + ex.Message); }
        finally
        {
            btnLoadStart.IsEnabled = true; btnLoadStop.IsEnabled = false;
            pbLoad.Value = 100;
        }
    }

    private void OnLoadStop(object sender, RoutedEventArgs e)
    {
        _loadCts?.Cancel();
        Log("부하 테스트 중지 요청…");
    }

    private static string FormatDist(IReadOnlyDictionary<string, long> d)
    {
        if (d.Count == 0) return "(없음)";
        var sb = new StringBuilder();
        foreach (var kv in d.OrderByDescending(k => k.Value))
            sb.AppendLine($"{kv.Key,-24} {kv.Value,10:N0}");
        return sb.ToString();
    }

    // ── 보안 점검 ────────────────────────────────────────────────
    private async void OnAuditStart(object sender, RoutedEventArgs e)
    {
        var url = _activeUrl;
        if (string.IsNullOrWhiteSpace(url)) url = txtLoadUrl.Text.Trim();
        if (!Guard(url)) return;

        btnAuditStart.IsEnabled = false;
        gridAudit.ItemsSource = null;
        txtScore.Text = "…";
        var log = new Progress<string>(Log);
        try
        {
            var findings = await _auditor.RunAsync(url, log, CancellationToken.None);
            findings = findings.OrderByDescending(SevRank).ToList();
            gridAudit.ItemsSource = findings;
            _reportFindings = findings;
            var (score, grade) = SecurityAuditor.Score(findings);
            txtScore.Text = $"{score} ({grade})";
            Log($"보안 점검 완료 — 점수 {score}/100 ({grade}), 항목 {findings.Count}건");
        }
        catch (Exception ex) { Log("점검 오류: " + ex.Message); }
        finally { btnAuditStart.IsEnabled = true; }
    }

    private static int SevRank(SecurityFinding f) =>
        f.Severity == Severity.Ok ? -1 : (int)f.Severity;

    private void OnExportReport(object sender, RoutedEventArgs e)
    {
        if (_reportFindings.Count == 0 && _lastLoad == null)
        {
            MessageBox.Show("먼저 보안 점검 또는 부하 테스트를 실행하세요.", "Siege.Lab");
            return;
        }
        var dlg = new SaveFileDialog
        {
            Filter = "Markdown (*.md)|*.md",
            FileName = $"siege-report-{SafetyGate.HostOf(_activeUrl)}-{DateTime.Now:yyyyMMdd_HHmmss}.md"
        };
        if (dlg.ShowDialog() == true)
        {
            var md = ReportExporter.BuildMarkdown(_activeUrl, _reportFindings, _lastLoad);
            File.WriteAllText(dlg.FileName, md, Encoding.UTF8);
            Log("리포트 저장됨: " + dlg.FileName);
        }
    }

    // ── 가용성 ───────────────────────────────────────────────────
    private async void OnHealthStart(object sender, RoutedEventArgs e)
    {
        var url = txtHealthUrl.Text.Trim();
        if (!Guard(url)) return;

        _health.Reset();
        _healthCts = new CancellationTokenSource();
        btnHealthStart.IsEnabled = false; btnHealthStop.IsEnabled = true;
        txtHealthLog.Clear();
        Log("헬스 모니터 시작 — " + SafetyGate.HostOf(url));

        var progress = new Progress<HealthSnapshot>(s =>
        {
            hvStatus.Text = s.CurrentlyUp ? "UP" : "DOWN";
            hvStatus.Foreground = (System.Windows.Media.Brush)
                Application.Current.Resources[s.CurrentlyUp ? "OkBrush" : "CriticalBrush"];
            hvUptime.Text = s.UptimePercent.ToString("F1");
            hvFails.Text = s.ConsecutiveFailures.ToString();
            hvChecks.Text = s.Checks.ToString("N0");
            hvLast.Text = s.LastLatencyMs.ToString("F0");
            hvAvg.Text = s.AvgLatencyMs.ToString("F0");
            if (s.Recent.Count > 0)
            {
                var last = s.Recent[^1];
                txtHealthLog.AppendText(
                    $"[{last.At:HH:mm:ss}] {(last.Up ? "UP  " : "DOWN")} " +
                    $"HTTP {last.Status,3}  {last.LatencyMs,7:F0} ms\r\n");
                txtHealthLog.ScrollToEnd();
            }
        });

        try { await _health.RunAsync(url, ParseInt(txtHealthInterval.Text, 5),
                  ParseInt(txtHealthTimeout.Text, 8000), progress, _healthCts.Token); }
        catch (OperationCanceledException) { Log("헬스 모니터 중지됨"); }
        catch (Exception ex) { Log("헬스 오류: " + ex.Message); }
        finally { btnHealthStart.IsEnabled = true; btnHealthStop.IsEnabled = false; }
    }

    private void OnHealthStop(object sender, RoutedEventArgs e)
    {
        _healthCts?.Cancel();
        Log("헬스 모니터 중지 요청…");
    }

    // ── 취약점/노출 스캔 ─────────────────────────────────────────
    private async void OnScanStart(object sender, RoutedEventArgs e)
    {
        var url = _activeUrl;
        if (string.IsNullOrWhiteSpace(url)) url = txtLoadUrl.Text.Trim();
        if (!Guard(url)) return;

        if (chkDefaultCreds.IsChecked == true)
        {
            var r = MessageBox.Show(
                "기본 자격증명 확인은 공격적 점검입니다. 인가된 대상에서만 진행하세요. 계속할까요?",
                "공격적 점검 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
        }

        var cfg = new VulnScanConfig
        {
            ProbeSensitiveFiles = chkFiles.IsChecked == true,
            ProbeAdminConsoles = chkConsoles.IsChecked == true,
            ProbeBackdoors = chkBackdoor.IsChecked == true,
            ProbeDirListing = chkDirList.IsChecked == true,
            ProbeVerboseErrors = chkErrors.IsChecked == true,
            ProbeTrace = chkTrace.IsChecked == true,
            ProbeDefaultCreds = chkDefaultCreds.IsChecked == true,
            DelayMs = ParseInt(txtScanDelay.Text, 150)
        };

        _scanCts = new CancellationTokenSource();
        btnScanStart.IsEnabled = false; btnScanStop.IsEnabled = true;
        gridScan.ItemsSource = null;
        var log = new Progress<string>(Log);
        try
        {
            var findings = await _scanner.RunAsync(url, cfg, log, _scanCts.Token);
            findings = findings.OrderByDescending(SevRank).ToList();
            gridScan.ItemsSource = findings;
            _reportFindings = findings;
            int risky = findings.Count(f => f.Severity is Severity.Critical or Severity.High);
            Log($"스캔 완료 — 총 {findings.Count}건 (고위험 {risky}건)");
        }
        catch (OperationCanceledException) { Log("스캔 중지됨"); }
        catch (Exception ex) { Log("스캔 오류: " + ex.Message); }
        finally { btnScanStart.IsEnabled = true; btnScanStop.IsEnabled = false; }
    }

    private void OnScanStop(object sender, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
        Log("스캔 중지 요청…");
    }

    // ── 로그인 방어 ──────────────────────────────────────────────
    private async void OnLoginStart(object sender, RoutedEventArgs e)
    {
        var url = txtLoginUrl.Text.Trim();
        if (!Guard(url)) return;

        var cfg = new LoginTestConfig
        {
            Url = url,
            Method = ComboText(cboLoginMethod),
            ContentType = ComboText(cboLoginCt),
            BodyTemplate = txtLoginBody.Text,
            Username = txtLoginUser.Text.Trim(),
            Attempts = ParseInt(txtLoginAttempts.Text, 50),
            DelayMs = ParseInt(txtLoginDelay.Text, 200),
            SuccessStatuses = txtLoginSuccess.Text
        };

        _loginCts = new CancellationTokenSource();
        btnLoginStart.IsEnabled = false; btnLoginStop.IsEnabled = true;
        Log($"로그인 방어 테스트 시작 — {SafetyGate.HostOf(url)} ({cfg.Attempts}회)");

        var progress = new Progress<LoginSnapshot>(s =>
        {
            lgProgress.Text = $"{s.Attempt}/{s.Total}";
            lgStatus.Text = s.LastStatus == 0 ? "err" : s.LastStatus.ToString();
            lgLat.Text = s.AvgLatencyMs.ToString("F0");
            txtLoginVerdict.Text = s.Verdict;
            txtLoginVerdict.Foreground = (System.Windows.Media.Brush)Application.Current.Resources[
                s.SuspiciousSuccess ? "CriticalBrush" : s.BlockingDetected ? "OkBrush" : "MediumBrush"];
            var sb = new StringBuilder();
            foreach (var kv in s.StatusDist.OrderByDescending(k => k.Value))
                sb.AppendLine($"HTTP {kv.Key,-6} {kv.Value,6:N0}");
            txtLoginDist.Text = sb.ToString();
        });

        try
        {
            var final = await _login.RunAsync(cfg, progress, _loginCts.Token);
            Log("로그인 방어 테스트 완료 — " + final.Verdict);
        }
        catch (OperationCanceledException) { Log("로그인 테스트 중지됨"); }
        catch (Exception ex) { Log("로그인 테스트 오류: " + ex.Message); }
        finally { btnLoginStart.IsEnabled = true; btnLoginStop.IsEnabled = false; }
    }

    private void OnLoginStop(object sender, RoutedEventArgs e)
    {
        _loginCts?.Cancel();
        Log("로그인 테스트 중지 요청…");
    }
}
