using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SharpToken;

namespace TokenCalc;

// ──────────────────────────────────────────────────────────────────────────────
// 모델 정의 (기본 단가)
// ──────────────────────────────────────────────────────────────────────────────
public record ModelDef(
    string Provider,
    string Name,
    string Encoding,          // "o200k_base" | "cl100k_base" | "claude"
    decimal DefaultInputPerM,
    decimal DefaultOutputPerM
);

// ──────────────────────────────────────────────────────────────────────────────
// DataGrid 바인딩 행 (단가 편집 가능)
// ──────────────────────────────────────────────────────────────────────────────
public class ModelRow : INotifyPropertyChanged
{
    private decimal _inputCostPerM;
    private decimal _outputCostPerM;
    private int _inputTokens;
    private int _outputTokens;

    public ModelDef Def { get; }
    public string Provider => Def.Provider;
    public string Name     => Def.Name;

    public decimal InputCostPerM
    {
        get => _inputCostPerM;
        set { _inputCostPerM = value; OnPropertyChanged(); Recalc(); }
    }

    public decimal OutputCostPerM
    {
        get => _outputCostPerM;
        set { _outputCostPerM = value; OnPropertyChanged(); Recalc(); }
    }

    public int InputTokens
    {
        get => _inputTokens;
        set { _inputTokens = value; OnPropertyChanged(); OnPropertyChanged(nameof(InputTokensDisplay)); Recalc(); }
    }

    public int OutputTokens
    {
        get => _outputTokens;
        set { _outputTokens = value; OnPropertyChanged(); OnPropertyChanged(nameof(OutputTokensDisplay)); Recalc(); }
    }

    public string InputTokensDisplay  => _inputTokens.ToString("N0");
    public string OutputTokensDisplay => _outputTokens.ToString("N0");

    public decimal InputCost  { get; private set; }
    public decimal OutputCost { get; private set; }
    public decimal TotalCost  { get; private set; }

    public string InputCostDisplay  => InputCost  < 0.001m ? $"${InputCost:F6}"  : $"${InputCost:F4}";
    public string OutputCostDisplay => OutputCost < 0.001m ? $"${OutputCost:F6}" : $"${OutputCost:F4}";
    public string TotalCostDisplay  => TotalCost  < 0.001m ? $"${TotalCost:F6}"  : $"${TotalCost:F4}";

    public ModelRow(ModelDef def)
    {
        Def = def;
        _inputCostPerM  = def.DefaultInputPerM;
        _outputCostPerM = def.DefaultOutputPerM;
    }

    private void Recalc()
    {
        InputCost  = _inputTokens  * _inputCostPerM  / 1_000_000m;
        OutputCost = _outputTokens * _outputCostPerM / 1_000_000m;
        TotalCost  = InputCost + OutputCost;
        OnPropertyChanged(nameof(InputCost));
        OnPropertyChanged(nameof(OutputCost));
        OnPropertyChanged(nameof(TotalCost));
        OnPropertyChanged(nameof(InputCostDisplay));
        OnPropertyChanged(nameof(OutputCostDisplay));
        OnPropertyChanged(nameof(TotalCostDisplay));
    }

    public void ResetPrices()
    {
        _inputCostPerM  = Def.DefaultInputPerM;
        _outputCostPerM = Def.DefaultOutputPerM;
        OnPropertyChanged(nameof(InputCostPerM));
        OnPropertyChanged(nameof(OutputCostPerM));
        Recalc();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// ──────────────────────────────────────────────────────────────────────────────
// 메인 윈도우
// ──────────────────────────────────────────────────────────────────────────────
public partial class MainWindow : Window
{
    private readonly ObservableCollection<ModelRow> _models = new();
    private int _expectedOutputTokens = 500;
    private GptEncoding? _cl100kEnc;
    private GptEncoding? _o200kEnc;

    private static readonly ModelDef[] ModelDefs =
    [
        new("OpenAI",     "GPT-4o",             "o200k_base",    2.500m,  10.000m),
        new("OpenAI",     "GPT-4o mini",         "o200k_base",    0.150m,   0.600m),
        new("OpenAI",     "o1",                  "o200k_base",   15.000m,  60.000m),
        new("OpenAI",     "o1-mini",             "o200k_base",    3.000m,  12.000m),
        new("OpenAI",     "o3-mini",             "o200k_base",    1.100m,   4.400m),
        new("OpenAI",     "GPT-3.5 Turbo",       "cl100k_base",   0.500m,   1.500m),
        new("Anthropic",  "Claude 3.7 Sonnet",   "claude",        3.000m,  15.000m),
        new("Anthropic",  "Claude 3.5 Sonnet",   "claude",        3.000m,  15.000m),
        new("Anthropic",  "Claude 3.5 Haiku",    "claude",        0.800m,   4.000m),
        new("Anthropic",  "Claude 3 Opus",       "claude",       15.000m,  75.000m),
        new("Google",     "Gemini 2.0 Flash",    "claude",        0.075m,   0.300m),
        new("Google",     "Gemini 1.5 Pro",      "claude",        1.250m,   5.000m),
        new("Google",     "Gemini 1.5 Flash",    "claude",        0.075m,   0.300m),
        new("Meta",       "Llama 3.1 405B",      "cl100k_base",   3.000m,   3.000m),
        new("Meta",       "Llama 3.1 70B",       "cl100k_base",   0.590m,   0.790m),
        new("Meta",       "Llama 3.1 8B",        "cl100k_base",   0.180m,   0.180m),
        new("Mistral",    "Mistral Large",        "cl100k_base",   2.000m,   6.000m),
        new("Mistral",    "Mistral Small",        "cl100k_base",   0.200m,   0.600m),
        new("DeepSeek",   "DeepSeek V3",          "cl100k_base",   0.270m,   1.100m),
        new("DeepSeek",   "DeepSeek R1",          "cl100k_base",   0.550m,   2.190m),
    ];

    public MainWindow()
    {
        InitializeComponent();
        LoadEncodings();
        foreach (var def in ModelDefs)
            _models.Add(new ModelRow(def));
        ModelGrid.ItemsSource = _models;
        StatusBar.Text = "준비됨. 텍스트를 입력하면 실시간으로 토큰 수와 비용이 계산됩니다.";
    }

    // DWM 다크 타이틀바
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int val = 1;
        NativeMethods.DwmSetWindowAttribute(hwnd, 20, ref val, sizeof(int));
    }

    private void LoadEncodings()
    {
        try { _cl100kEnc = GptEncoding.GetEncoding("cl100k_base"); } catch { }
        try { _o200kEnc  = GptEncoding.GetEncoding("o200k_base");  } catch { }
    }

    private int CountTokens(string text, string enc)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return enc switch
        {
            "cl100k_base" => _cl100kEnc?.Encode(text).Count ?? EstimateTokens(text),
            "o200k_base"  => _o200kEnc?.Encode(text).Count  ?? EstimateTokens(text),
            _             => EstimateTokens(text),   // claude / google: 문자수 ÷ 3.5
        };
    }

    private static int EstimateTokens(string text) => (int)Math.Ceiling(text.Length / 3.5);

    private void RecalcAll()
    {
        if (!IsLoaded) return;

        var sysText  = TxtSystem.Text;
        var userText = TxtUser.Text;
        var combined = sysText + "\n" + userText;
        int outTokens = _expectedOutputTokens;

        // 헤더 카운트 업데이트 (cl100k_base 기준으로 표시)
        int sysTokens  = CountTokens(sysText,  "cl100k_base");
        int userTokens = CountTokens(userText, "cl100k_base");
        TxtSystemTokens.Text  = $"{sysTokens:N0} 토큰";
        TxtUserTokens.Text    = $"{userTokens:N0} 토큰";
        TxtOutputTokens.Text  = $"{outTokens:N0} 토큰";
        TxtTotalInputTokens.Text = $"총 입력 {sysTokens + userTokens:N0} 토큰";

        // 각 모델별 토큰 계산
        foreach (var row in _models)
        {
            int inTok = CountTokens(combined, row.Def.Encoding);
            row.InputTokens  = inTok;
            row.OutputTokens = outTokens;
        }

        // 가장 저렴한 모델 표시
        var cheapest = _models.OrderBy(r => r.TotalCost).FirstOrDefault();
        if (cheapest != null && cheapest.TotalCost > 0)
            TxtCheapestModel.Text = $"최저 비용: {cheapest.Name} ({cheapest.TotalCostDisplay})";
        else
            TxtCheapestModel.Text = "";
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => RecalcAll();

    private void TextInput_Changed(object sender, TextChangedEventArgs e) => RecalcAll();

    private void TxtOutputCount_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (int.TryParse(TxtOutputCount.Text, out int n) && n >= 0)
        {
            _expectedOutputTokens = n;
            TxtOutputTokens.Text  = $"{n:N0} 토큰";
            RecalcAll();
        }
    }

    private void QuickOutput_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            TxtOutputCount.Text = tag;
        }
    }

    private void BtnResetPrices_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _models) row.ResetPrices();
        RecalcAll();
        StatusBar.Text = "단가를 기본값으로 초기화했습니다.";
    }

    private void BtnSortCost_Click(object sender, RoutedEventArgs e)
    {
        var sorted = _models.OrderBy(r => r.TotalCost).ToList();
        _models.Clear();
        foreach (var r in sorted) _models.Add(r);
        StatusBar.Text = "총 비용 기준으로 정렬했습니다 (낮은 순).";
    }

    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "CSV 파일|*.csv", FileName = "token_calc_result" };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder("제공사,모델,인코딩,입력토큰,출력토큰,입력$/1M,출력$/1M,입력비용,출력비용,합계비용\n");
        foreach (var row in _models)
            sb.AppendLine($"{row.Provider},{row.Name},{row.Def.Encoding},{row.InputTokens},{row.OutputTokens},{row.InputCostPerM:F3},{row.OutputCostPerM:F3},{row.InputCost:F6},{row.OutputCost:F6},{row.TotalCost:F6}");

        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
        StatusBar.Text = $"CSV 내보내기 완료: {dlg.FileName}";
    }

    private void BtnExportJson_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "JSON 파일|*.json", FileName = "token_calc_result" };
        if (dlg.ShowDialog() != true) return;

        var data = _models.Select(r => new
        {
            provider     = r.Provider,
            model        = r.Name,
            encoding     = r.Def.Encoding,
            inputTokens  = r.InputTokens,
            outputTokens = r.OutputTokens,
            inputCostPerM  = r.InputCostPerM,
            outputCostPerM = r.OutputCostPerM,
            inputCost    = r.InputCost,
            outputCost   = r.OutputCost,
            totalCost    = r.TotalCost,
        });

        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(data, opts), new UTF8Encoding(true));
        StatusBar.Text = $"JSON 내보내기 완료: {dlg.FileName}";
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// DWM (다크 타이틀바)
// ──────────────────────────────────────────────────────────────────────────────
internal static partial class NativeMethods
{
    [System.Runtime.InteropServices.LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(
        nint hwnd, int attr, ref int attrValue, int attrSize);
}
