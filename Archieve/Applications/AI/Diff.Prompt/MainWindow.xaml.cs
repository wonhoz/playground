using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using DiffPrompt.Models;
using DiffPrompt.Services;

namespace DiffPrompt;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    private DbService? _db;
    private ClaudeService? _claude;
    private CancellationTokenSource? _cts;

    // 현재 실험
    private Experiment _current = new();
    private string _apiKey = "";

    private const string ApiKeyPath = "diffprompt_key.txt";

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        _db = new DbService();

        // 모델 목록
        foreach (var m in ClaudeModels.All)
        {
            ModelA.Items.Add(m);
            ModelB.Items.Add(m);
        }
        ModelA.SelectedIndex = 1;  // sonnet
        ModelB.SelectedIndex = 1;

        // API 키 로드
        if (File.Exists(ApiKeyPath))
        {
            _apiKey = File.ReadAllText(ApiKeyPath).Trim();
            InitClaude();
            StatusBar.Text = "API 키가 로드되었습니다. 프롬프트를 입력하고 실행하세요.";
        }
    }

    void Window_Closed(object sender, EventArgs e)
    {
        _cts?.Cancel();
        _db?.Dispose();
    }

    void InitClaude()
    {
        if (!string.IsNullOrEmpty(_apiKey))
            _claude = new ClaudeService(_apiKey);
    }

    // ─── 실행 ─────────────────────────────────────────────────────────────
    async void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckReady()) return;
        _current = NewExperiment();
        await RunBothAsync();
    }

    async void BtnRunA_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckReady()) return;
        _current = NewExperiment();
        await RunOneAsync(isA: true);
    }

    async void BtnRunB_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckReady()) return;
        _current = NewExperiment();
        await RunOneAsync(isA: false);
    }

    bool CheckReady()
    {
        if (_claude == null)
        {
            MessageBox.Show("API 키를 먼저 설정해주세요. ⚙️ API 키 버튼을 클릭하세요.", "API 키 없음",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (string.IsNullOrWhiteSpace(UserMessageBox.Text))
        {
            StatusBar.Text = "User Message를 입력해주세요.";
            return false;
        }
        return true;
    }

    Experiment NewExperiment() => new()
    {
        PromptA = PromptABox.Text,
        PromptB = PromptBBox.Text,
        UserMessage = UserMessageBox.Text,
        ModelA = ModelA.SelectedItem?.ToString() ?? "claude-sonnet-4-6",
        ModelB = ModelB.SelectedItem?.ToString() ?? "claude-sonnet-4-6",
    };

    async Task RunBothAsync()
    {
        _cts = new CancellationTokenSource();
        SetRunningState(true);
        OutputA.Text = "";
        OutputB.Text = "";
        StatsA.Text = "실행 중...";
        StatsB.Text = "실행 중...";
        VoteResult.Text = "";

        var taskA = RunOneInternalAsync(true, _cts.Token);
        var taskB = RunOneInternalAsync(false, _cts.Token);
        await Task.WhenAll(taskA, taskB);

        SetRunningState(false);
        BtnDiff.IsEnabled = true;
        SaveCurrentExperiment();
    }

    async Task RunOneAsync(bool isA)
    {
        _cts = new CancellationTokenSource();
        SetRunningState(true);
        if (isA) { OutputA.Text = ""; StatsA.Text = "실행 중..."; }
        else { OutputB.Text = ""; StatsB.Text = "실행 중..."; }

        await RunOneInternalAsync(isA, _cts.Token);
        SetRunningState(false);
        bool bothDone = !string.IsNullOrEmpty(OutputA.Text) && !string.IsNullOrEmpty(OutputB.Text);
        BtnDiff.IsEnabled = bothDone;
        if (bothDone) SaveCurrentExperiment();
    }

    async Task RunOneInternalAsync(bool isA, CancellationToken ct)
    {
        try
        {
            string prompt = isA ? _current.PromptA : _current.PromptB;
            string model = isA ? _current.ModelA : _current.ModelB;
            var outputBox = isA ? OutputA : OutputB;
            var statsLabel = isA ? StatsA : StatsB;

            var (output, inputTokens, outputTokens, latencyMs) = await _claude!.RunAsync(
                prompt, _current.UserMessage, model,
                chunk => Dispatcher.Invoke(() => outputBox.Text += chunk),
                ct);

            double cost = ClaudeModels.CalcCost(model, inputTokens, outputTokens);

            if (isA)
            {
                _current.OutputA = output;
                _current.TokensA = inputTokens + outputTokens;
                _current.CostA = cost;
                _current.LatencyAMs = latencyMs;
            }
            else
            {
                _current.OutputB = output;
                _current.TokensB = inputTokens + outputTokens;
                _current.CostB = cost;
                _current.LatencyBMs = latencyMs;
            }

            Dispatcher.Invoke(() =>
            {
                statsLabel.Text = $"토큰: {inputTokens + outputTokens} | 비용: ${cost:F4} | {latencyMs / 1000:F1}s";
                UpdateTotalCost();
            });
        }
        catch (OperationCanceledException)
        {
            Dispatcher.Invoke(() => StatusBar.Text = "실행이 중단되었습니다.");
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => StatusBar.Text = $"오류: {ex.Message}");
        }
    }

    void UpdateTotalCost()
    {
        double total = _current.CostA + _current.CostB;
        TotalCost.Text = $"이번 실험 비용: ${total:F4}";
    }

    void SetRunningState(bool running)
    {
        BtnRun.IsEnabled = !running;
        BtnRunA.IsEnabled = !running;
        BtnRunB.IsEnabled = !running;
        BtnStop.IsEnabled = running;
        StatusBar.Text = running ? "Claude API에 요청 중..." : "완료";
    }

    void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        SetRunningState(false);
    }

    // ─── Diff 보기 ────────────────────────────────────────────────────────
    void BtnDiff_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new DiffWindow(OutputA.Text, OutputB.Text) { Owner = this };
        dlg.ShowDialog();
    }

    // ─── 투표 ─────────────────────────────────────────────────────────────
    void BtnVoteA_Click(object sender, RoutedEventArgs e) => SetVote(1, "🔵 A 승!");
    void BtnVoteTie_Click(object sender, RoutedEventArgs e) => SetVote(0, "🤝 무승부");
    void BtnVoteB_Click(object sender, RoutedEventArgs e) => SetVote(2, "🟠 B 승!");

    void SetVote(int winner, string text)
    {
        _current.WinnerVote = winner;
        VoteResult.Text = text;
        SaveCurrentExperiment();
    }

    // ─── 이력 ─────────────────────────────────────────────────────────────
    void BtnHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_db == null) return;
        var dlg = new HistoryWindow(_db) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedExperiment != null)
            LoadExperiment(dlg.SelectedExperiment);
    }

    void LoadExperiment(Experiment e)
    {
        PromptABox.Text = e.PromptA;
        PromptBBox.Text = e.PromptB;
        UserMessageBox.Text = e.UserMessage;
        OutputA.Text = e.OutputA;
        OutputB.Text = e.OutputB;
        ModelA.SelectedItem = e.ModelA;
        ModelB.SelectedItem = e.ModelB;
        StatsA.Text = $"토큰: {e.TokensA} | 비용: ${e.CostA:F4} | {e.LatencyAMs / 1000:F1}s";
        StatsB.Text = $"토큰: {e.TokensB} | 비용: ${e.CostB:F4} | {e.LatencyBMs / 1000:F1}s";
        VoteResult.Text = e.WinnerVote switch { 1 => "🔵 A 승!", 2 => "🟠 B 승!", 0 => "🤝 무승부", _ => "" };
        _current = e;
        BtnDiff.IsEnabled = !string.IsNullOrEmpty(e.OutputA) && !string.IsNullOrEmpty(e.OutputB);
    }

    // ─── API 키 ───────────────────────────────────────────────────────────
    void BtnApiKey_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ApiKeyDialog(_apiKey) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _apiKey = dlg.ApiKey;
            File.WriteAllText(ApiKeyPath, _apiKey);
            InitClaude();
            StatusBar.Text = "API 키가 저장되었습니다.";
        }
    }

    // ─── 내보내기 ─────────────────────────────────────────────────────────
    void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_current.OutputA) && string.IsNullOrEmpty(_current.OutputB))
        {
            StatusBar.Text = "내보낼 결과가 없습니다.";
            return;
        }
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "텍스트 파일|*.txt|Markdown|*.md", DefaultExt = ".md" };
        if (dlg.ShowDialog() == true)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# Diff.Prompt 실험 결과");
            sb.AppendLine($"생성: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"## System Prompt A ({_current.ModelA})");
            sb.AppendLine(_current.PromptA);
            sb.AppendLine();
            sb.AppendLine($"## System Prompt B ({_current.ModelB})");
            sb.AppendLine(_current.PromptB);
            sb.AppendLine();
            sb.AppendLine("## User Message");
            sb.AppendLine(_current.UserMessage);
            sb.AppendLine();
            sb.AppendLine($"## Output A (토큰: {_current.TokensA}, 비용: ${_current.CostA:F4})");
            sb.AppendLine(_current.OutputA);
            sb.AppendLine();
            sb.AppendLine($"## Output B (토큰: {_current.TokensB}, 비용: ${_current.CostB:F4})");
            sb.AppendLine(_current.OutputB);
            sb.AppendLine();
            sb.AppendLine($"## 평가: {_current.WinnerVote switch { 1 => "A 승", 2 => "B 승", 0 => "무승부", _ => "미평가" }}");
            File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
            StatusBar.Text = $"내보내기 완료: {dlg.FileName}";
        }
    }

    void ModelA_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    void ModelB_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    void SaveCurrentExperiment()
    {
        if (_db == null) return;
        _current.Id = _db.SaveExperiment(_current);
    }
}
