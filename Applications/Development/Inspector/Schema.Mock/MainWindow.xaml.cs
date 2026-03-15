using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bogus;
using Microsoft.Win32;

namespace SchemaMock;

// ── 데이터 모델 ───────────────────────────────────────────────────────────────

public class FieldRow : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string TypeDisplay { get; set; } = "";
    public string Format { get; set; } = "";
    public string[] EnumValues { get; set; } = [];
    public double? Min { get; set; }
    public double? Max { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string RawType { get; set; } = "string";

    private string _strategy = "Auto";
    public string Strategy
    {
        get => _strategy;
        set { _strategy = value; OnPropertyChanged(); RefreshPreview(); }
    }

    private string _preview = "";
    public string Preview
    {
        get => _preview;
        set { _preview = value; OnPropertyChanged(); }
    }

    public void RefreshPreview()
    {
        try { Preview = FakerEngine.GenerateSampleValue(this); }
        catch { Preview = "—"; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// ── 파서 형식 ─────────────────────────────────────────────────────────────────

public enum SchemaFormat { Unknown, JsonSchema, OpenApi }

// ── 메인 윈도우 ───────────────────────────────────────────────────────────────

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly ObservableCollection<FieldRow> _fields = [];
    private SchemaFormat _detectedFormat = SchemaFormat.Unknown;
    private Dictionary<string, JsonElement> _oapiSchemas = [];

    public static readonly string[] StrategyList =
    [
        "Auto",
        "──── 이름/신원 ────",
        "FullName", "FirstName", "LastName",
        "Email", "UserName", "Phone",
        "──── 주소 ────",
        "StreetAddress", "City", "Country", "ZipCode",
        "──── 회사/웹 ────",
        "CompanyName", "URL", "IPAddress",
        "──── 텍스트 ────",
        "Word", "Sentence", "Paragraph",
        "──── 식별자/날짜 ────",
        "UUID", "Date", "DateTime",
        "──── 숫자/논리 ────",
        "RandomInt", "RandomFloat", "Boolean",
        "──── 기타 ────",
        "HexColor", "Enum", "NullValue",
    ];

    public MainWindow() => InitializeComponent();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));

        GridFields.ItemsSource = _fields;

        // Strategy 컬럼 추가 (DataGridTemplateColumn)
        var strategyCol = new DataGridTemplateColumn { Header = "Faker 전략", Width = new DataGridLength(150) };
        var cellTemplate = new DataTemplate();
        var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
        tbFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Strategy"));
        tbFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xFF, 0xC0, 0x50)));
        cellTemplate.VisualTree = tbFactory;

        var editTemplate = new DataTemplate();
        var cbFactory = new FrameworkElementFactory(typeof(ComboBox));
        cbFactory.SetBinding(ComboBox.SelectedItemProperty,
            new System.Windows.Data.Binding("Strategy") { UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged });
        cbFactory.SetValue(ComboBox.ItemsSourceProperty, StrategyList);
        cbFactory.SetValue(ComboBox.FontSizeProperty, 11.0);
        editTemplate.VisualTree = cbFactory;

        strategyCol.CellTemplate = cellTemplate;
        strategyCol.CellEditingTemplate = editTemplate;

        // Preview 컬럼 바로 앞에 삽입
        GridFields.Columns.Insert(GridFields.Columns.Count - 1, strategyCol);

        SetStatus("JSON Schema 또는 OpenAPI 3.x YAML/JSON을 왼쪽에 붙여넣고 분석 버튼을 누르세요.");
    }

    // ── 입력 이벤트 ───────────────────────────────────────────────────────────

    private void TxtSchema_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var text = TxtSchema.Text.Trim();
        if (string.IsNullOrEmpty(text)) { LblDetected.Text = "형식: —"; return; }

        _detectedFormat = DetectFormat(text);
        LblDetected.Text = _detectedFormat switch
        {
            SchemaFormat.JsonSchema => "형식: JSON Schema ✓",
            SchemaFormat.OpenApi => "형식: OpenAPI 3.x ✓",
            _ => "형식: 인식 불가"
        };
        LblDetected.Foreground = _detectedFormat == SchemaFormat.Unknown
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x60, 0x60))
            : new SolidColorBrush(Color.FromRgb(0x50, 0xE0, 0x80));
    }

    private async void BtnFetchUrl_Click(object sender, RoutedEventArgs e)
    {
        var url = TxtUrl.Text.Trim();
        if (string.IsNullOrEmpty(url)) { SetStatus("URL을 입력하세요."); return; }

        BtnFetchUrl.IsEnabled = false;
        try
        {
            var content = await _http.GetStringAsync(url);
            TxtSchema.Text = content;
            SetStatus($"URL 가져오기 완료 ({content.Length:N0} 문자)");
        }
        catch (Exception ex)
        {
            SetStatus($"URL 가져오기 실패: {ex.Message}");
        }
        finally { BtnFetchUrl.IsEnabled = true; }
    }

    private void CmbOapiSchema_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || CmbOapiSchema.SelectedItem is not string schemaName) return;
        if (_oapiSchemas.TryGetValue(schemaName, out var schemaElem))
            LoadFieldsFromJsonElement(schemaElem);
    }

    private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtSchema.Text.Trim();
        if (string.IsNullOrEmpty(text)) { SetStatus("스키마를 입력하세요."); return; }

        try
        {
            _detectedFormat = DetectFormat(text);
            switch (_detectedFormat)
            {
                case SchemaFormat.JsonSchema:
                    AnalyzeJsonSchema(text);
                    break;
                case SchemaFormat.OpenApi:
                    AnalyzeOpenApi(text);
                    break;
                default:
                    SetStatus("스키마 형식을 인식할 수 없습니다. JSON Schema 또는 OpenAPI 3.x 형식인지 확인하세요.");
                    return;
            }
            ResultTabs.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            SetStatus($"분석 오류: {ex.Message}");
            MessageBox.Show($"스키마 분석 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClearInput_Click(object sender, RoutedEventArgs e)
    {
        TxtSchema.Clear();
        _fields.Clear();
        TxtOutput.Clear();
        PanelOapiSelector.Visibility = Visibility.Collapsed;
        LblDetected.Text = "형식: —";
        SetStatus("지워졌습니다.");
    }

    // ── 생성 / 복사 / 저장 ────────────────────────────────────────────────────

    private void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (_fields.Count == 0) { SetStatus("먼저 스키마를 분석하세요."); return; }

        if (!int.TryParse(TxtCount.Text, out int count) || count < 1 || count > 100_000)
        {
            SetStatus("건수는 1~100,000 범위로 입력하세요."); return;
        }

        try
        {
            BtnGenerate.IsEnabled = false;
            var format = (CmbFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "JSON Array";
            var output = FakerEngine.Generate(_fields.ToList(), count, format);
            TxtOutput.Text = output;
            LblOutputInfo.Text = $"{count:N0}건 · {output.Length:N0}자";
            ResultTabs.SelectedIndex = 1;
            SetStatus($"생성 완료: {count:N0}건 ({format})");
        }
        catch (Exception ex)
        {
            SetStatus($"생성 오류: {ex.Message}");
        }
        finally { BtnGenerate.IsEnabled = true; }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtOutput.Text)) { SetStatus("먼저 데이터를 생성하세요."); return; }
        try { Clipboard.SetText(TxtOutput.Text); SetStatus("클립보드에 복사됐습니다."); }
        catch { SetStatus("클립보드 복사 실패."); }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtOutput.Text)) { SetStatus("먼저 데이터를 생성하세요."); return; }

        var format = (CmbFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "JSON Array";
        var ext = format == "CSV" ? "csv" : "json";

        var dlg = new SaveFileDialog
        {
            Filter = format == "CSV"
                ? "CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*"
                : "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
            FileName = $"mock_data_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}"
        };

        if (dlg.ShowDialog() == true)
        {
            File.WriteAllText(dlg.FileName, TxtOutput.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            SetStatus($"저장 완료: {dlg.FileName}");
        }
    }

    // ── 스키마 분석 ───────────────────────────────────────────────────────────

    private void AnalyzeJsonSchema(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
        var root = doc.RootElement.Clone();
        _fields.Clear();
        LoadFieldsFromJsonElement(root);
        PanelOapiSelector.Visibility = Visibility.Collapsed;
        SetStatus($"JSON Schema 분석 완료: {_fields.Count}개 필드");
    }

    private void AnalyzeOpenApi(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
        var root = doc.RootElement;
        _oapiSchemas.Clear();

        if (root.TryGetProperty("components", out var components) &&
            components.TryGetProperty("schemas", out var schemas))
        {
            foreach (var schema in schemas.EnumerateObject())
                _oapiSchemas[schema.Name] = schema.Value.Clone();
        }

        if (_oapiSchemas.Count == 0)
        {
            SetStatus("OpenAPI에서 schemas를 찾지 못했습니다."); return;
        }

        CmbOapiSchema.ItemsSource = _oapiSchemas.Keys.ToList();
        CmbOapiSchema.SelectedIndex = 0;
        PanelOapiSelector.Visibility = Visibility.Visible;

        var firstName = _oapiSchemas.Keys.First();
        LoadFieldsFromJsonElement(_oapiSchemas[firstName]);
        SetStatus($"OpenAPI 분석 완료: {_oapiSchemas.Count}개 스키마, 현재 '{firstName}'");
    }

    private void LoadFieldsFromJsonElement(JsonElement schema)
    {
        _fields.Clear();
        var resolved = ResolveRef(schema, schema);
        ExtractProperties(resolved, "", schema);

        // 샘플 미리보기 생성
        foreach (var f in _fields) f.RefreshPreview();
    }

    private void ExtractProperties(JsonElement elem, string prefix, JsonElement root)
    {
        if (elem.TryGetProperty("properties", out var props))
        {
            foreach (var prop in props.EnumerateObject())
            {
                var fieldName = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                var propElem = ResolveRef(prop.Value, root);
                var field = BuildFieldRow(fieldName, propElem);
                _fields.Add(field);

                // 중첩 객체 1레벨까지만 (너무 깊으면 DataGrid 너무 많아짐)
                if (field.RawType == "object" && string.IsNullOrEmpty(prefix))
                    ExtractProperties(propElem, fieldName, root);
            }
        }
        else if (elem.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "object")
        {
            // properties 없는 순수 object → 건너뜀
        }
    }

    private static FieldRow BuildFieldRow(string name, JsonElement elem)
    {
        var rawType = elem.TryGetProperty("type", out var t) ? t.GetString() ?? "string" : "string";
        // nullable: ["string", "null"] 처리
        if (rawType == "array" && elem.TryGetProperty("type", out var tArr) && tArr.ValueKind == JsonValueKind.Array)
        {
            rawType = tArr.EnumerateArray().Select(x => x.GetString()).FirstOrDefault(x => x != "null") ?? "string";
        }

        var format = elem.TryGetProperty("format", out var f) ? f.GetString() ?? "" : "";
        var enumVals = elem.TryGetProperty("enum", out var e)
            ? e.EnumerateArray().Select(x => x.ToString()).ToArray()
            : [];
        var min = elem.TryGetProperty("minimum", out var mn) ? (double?)mn.GetDouble() : null;
        var max = elem.TryGetProperty("maximum", out var mx) ? (double?)mx.GetDouble() : null;
        var minLen = elem.TryGetProperty("minLength", out var ml) ? (int?)ml.GetInt32() : null;
        var maxLen = elem.TryGetProperty("maxLength", out var mxl) ? (int?)mxl.GetInt32() : null;

        var field = new FieldRow
        {
            Name = name,
            RawType = rawType,
            TypeDisplay = rawType + (string.IsNullOrEmpty(format) ? "" : $"/{format}"),
            Format = format,
            EnumValues = enumVals,
            Min = min,
            Max = max,
            MinLength = minLen,
            MaxLength = maxLen,
        };
        field.Strategy = FakerEngine.AutoDetectStrategy(field);
        return field;
    }

    // ── $ref 해결 ─────────────────────────────────────────────────────────────

    private static JsonElement ResolveRef(JsonElement elem, JsonElement root)
    {
        if (!elem.TryGetProperty("$ref", out var refProp)) return elem;
        var path = refProp.GetString() ?? "";
        if (!path.StartsWith('#')) return elem;

        var parts = path.TrimStart('#', '/').Split('/');
        var current = root;
        foreach (var part in parts)
        {
            if (!current.TryGetProperty(part, out current)) return elem;
        }
        return ResolveRef(current, root);
    }

    // ── 형식 감지 ─────────────────────────────────────────────────────────────

    private static SchemaFormat DetectFormat(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true });
            var root = doc.RootElement;

            if (root.TryGetProperty("openapi", out _) || root.TryGetProperty("swagger", out _))
                return SchemaFormat.OpenApi;

            if (root.TryGetProperty("$schema", out _) ||
                root.TryGetProperty("properties", out _) ||
                root.TryGetProperty("type", out _) ||
                root.TryGetProperty("definitions", out _) ||
                root.TryGetProperty("$defs", out _))
                return SchemaFormat.JsonSchema;
        }
        catch { /* YAML 또는 잘못된 JSON */ }
        return SchemaFormat.Unknown;
    }

    private void SetStatus(string msg)
        => StatusBar.Text = $"[{DateTime.Now:HH:mm:ss}] {msg}";
}

// ══════════════════════════════════════════════════════════════════════════════
// FakerEngine — Bogus 기반 가짜 데이터 생성
// ══════════════════════════════════════════════════════════════════════════════

public static class FakerEngine
{
    private static readonly Faker _faker = new("en");

    public static string AutoDetectStrategy(FieldRow field)
    {
        var n = field.Name.ToLower();
        var fmt = field.Format;
        var typ = field.RawType;

        if (field.EnumValues.Length > 0) return "Enum";

        return (fmt, typ, n) switch
        {
            ("uuid", _, _) or ("guid", _, _) => "UUID",
            ("email", _, _) => "Email",
            ("uri", _, _) or ("url", _, _) => "URL",
            ("date-time", _, _) or ("datetime", _, _) => "DateTime",
            ("date", _, _) => "Date",
            ("ipv4", _, _) or ("ipv6", _, _) => "IPAddress",
            ("color", _, _) => "HexColor",
            _ when n.Contains("email") => "Email",
            _ when n is "id" or "uuid" or "guid" || n.EndsWith("_id") || n.EndsWith("id") => "UUID",
            _ when n.Contains("firstname") || n.Contains("first_name") => "FirstName",
            _ when n.Contains("lastname") || n.Contains("last_name") => "LastName",
            _ when n.Contains("fullname") || n.Contains("full_name") || n == "name" => "FullName",
            _ when n.Contains("phone") || n.Contains("tel") || n.Contains("mobile") => "Phone",
            _ when n.Contains("address") || n.Contains("street") => "StreetAddress",
            _ when n.Contains("city") => "City",
            _ when n.Contains("country") => "Country",
            _ when n.Contains("zip") || n.Contains("postal") => "ZipCode",
            _ when n.Contains("company") || n.Contains("organization") || n.Contains("org") => "CompanyName",
            _ when n.Contains("user") || n.Contains("login") || n.Contains("account") => "UserName",
            _ when n.Contains("url") || n.Contains("link") || n.Contains("website") => "URL",
            _ when n.Contains("ip") || n == "host" => "IPAddress",
            _ when n.Contains("color") || n.Contains("colour") => "HexColor",
            _ when n.Contains("title") || n.Contains("subject") || n.Contains("topic") => "Sentence",
            _ when n.Contains("description") || n.Contains("content") || n.Contains("body") || n.Contains("comment") || n.Contains("note") => "Paragraph",
            (_, "integer", _) or (_, "int", _) => "RandomInt",
            (_, "number", _) or (_, "float", _) or (_, "double", _) => "RandomFloat",
            (_, "boolean", _) or (_, "bool", _) => "Boolean",
            _ => "Sentence"
        };
    }

    public static string GenerateSampleValue(FieldRow field)
    {
        var strategy = field.Strategy == "Auto" ? AutoDetectStrategy(field) : field.Strategy;
        var val = GenerateValue(strategy, field, _faker);
        return val?.ToString() ?? "null";
    }

    public static string Generate(List<FieldRow> fields, int count, string format)
    {
        var records = new List<Dictionary<string, object?>>();
        var faker = new Faker("en");

        for (int i = 0; i < count; i++)
        {
            var record = new Dictionary<string, object?>();
            foreach (var field in fields)
            {
                var strategy = field.Strategy == "Auto" ? AutoDetectStrategy(field) : field.Strategy;
                var key = field.Name.Contains('.') ? field.Name.Split('.').Last() : field.Name;
                record[key] = GenerateValue(strategy, field, faker);
            }
            // 중첩 객체 처리
            var flat = BuildNested(record);
            records.Add(flat);
        }

        return format switch
        {
            "NDJSON" => string.Join('\n', records.Select(r => JsonSerializer.Serialize(r))),
            "CSV" => BuildCsv(fields, records),
            _ => JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = count <= 100 })
        };
    }

    private static Dictionary<string, object?> BuildNested(Dictionary<string, object?> flat)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kv in flat)
        {
            if (!kv.Key.Contains('.'))
            {
                result[kv.Key] = kv.Value;
            }
            else
            {
                // 단순화: 중첩 키는 마지막 부분만 사용
                var lastKey = kv.Key.Split('.').Last();
                result.TryAdd(lastKey, kv.Value);
            }
        }
        return result;
    }

    private static string BuildCsv(List<FieldRow> fields, List<Dictionary<string, object?>> records)
    {
        var sb = new StringBuilder();
        var headers = fields.Select(f => f.Name.Contains('.') ? f.Name.Split('.').Last() : f.Name).ToList();
        sb.AppendLine(string.Join(',', headers.Select(h => $"\"{h}\"")));
        foreach (var record in records)
        {
            var values = headers.Select(h =>
            {
                record.TryGetValue(h, out var v);
                var str = v?.ToString() ?? "";
                return $"\"{str.Replace("\"", "\"\"")}\"";
            });
            sb.AppendLine(string.Join(',', values));
        }
        return sb.ToString();
    }

    private static object? GenerateValue(string strategy, FieldRow field, Faker f) => strategy switch
    {
        "UUID" => f.Random.Uuid().ToString(),
        "FullName" => f.Name.FullName(),
        "FirstName" => f.Name.FirstName(),
        "LastName" => f.Name.LastName(),
        "Email" => f.Internet.Email(),
        "Phone" => f.Phone.PhoneNumber(),
        "StreetAddress" => f.Address.StreetAddress(),
        "City" => f.Address.City(),
        "Country" => f.Address.Country(),
        "ZipCode" => f.Address.ZipCode(),
        "CompanyName" => f.Company.CompanyName(),
        "UserName" => f.Internet.UserName(),
        "URL" => f.Internet.Url(),
        "IPAddress" => f.Internet.Ip(),
        "HexColor" => f.Internet.Color(),
        "Word" => f.Lorem.Word(),
        "Sentence" => f.Lorem.Sentence(),
        "Paragraph" => f.Lorem.Paragraph(1),
        "RandomInt" => f.Random.Int((int)(field.Min ?? 0), (int)(field.Max ?? 1000)),
        "RandomFloat" => Math.Round(f.Random.Double(field.Min ?? 0.0, field.Max ?? 100.0), 2),
        "Boolean" => f.Random.Bool(),
        "Date" => f.Date.Past(5).ToString("yyyy-MM-dd"),
        "DateTime" => f.Date.Past(5).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
        "Enum" => field.EnumValues.Length > 0
            ? field.EnumValues[f.Random.Int(0, field.EnumValues.Length - 1)]
            : f.Lorem.Word(),
        "NullValue" => null,
        _ => f.Lorem.Word()
    };
}
