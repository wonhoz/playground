using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using CsvHelper;
using CsvHelper.Configuration;

namespace DataMap;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private enum DataKind { None, GeoJson, TopoJson, Csv }
    private DataKind _dataKind = DataKind.None;
    private string _dataContent = "";
    private List<string> _fields = [];

    public MainWindow()
    {
        InitializeComponent();
        var handle = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
        int v = 1;
        DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _ = InitWebViewAsync();
    }

    private async Task InitWebViewAsync()
    {
        await WebMap.EnsureCoreWebView2Async();
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("DataMap.Resources.map.html")!;
        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();
        WebMap.NavigateToString(html);
        SetStatus("지도 로드 완료. 데이터 파일을 선택하세요.");
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "지리 데이터|*.geojson;*.json;*.topojson;*.csv|GeoJSON|*.geojson;*.json|TopoJSON|*.topojson|CSV|*.csv|모든 파일|*.*",
            Title = "데이터 파일 선택"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            TxtFilePath.Text = dlg.FileName;
            LoadFile(dlg.FileName);
        }
        catch (Exception ex)
        {
            SetStatus($"파일 로드 실패: {ex.Message}");
        }
    }

    private void LoadFile(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        _dataContent = File.ReadAllText(path, Encoding.UTF8);
        _fields.Clear();

        if (ext == ".csv")
        {
            _dataKind = DataKind.Csv;
            LblFileType.Text = "형식: CSV";
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            using var reader = new StringReader(_dataContent);
            using var csv = new CsvReader(reader, config);
            csv.Read();
            csv.ReadHeader();
            _fields = csv.HeaderRecord?.ToList() ?? [];

            PanelCsvOptions.Visibility = Visibility.Visible;
            PanelGeoOptions.Visibility = Visibility.Collapsed;
            RefreshFieldCombos();

            // 레코드 수 카운트
            int rowCount = 0;
            while (csv.Read()) rowCount++;
            LblDataInfo.Text = $"CSV: {rowCount}행, {_fields.Count}열";
        }
        else if (ext == ".topojson")
        {
            _dataKind = DataKind.TopoJson;
            LblFileType.Text = "형식: TopoJSON";
            // TopoJSON → GeoJSON 변환은 JS에서 처리
            var json = JsonNode.Parse(_dataContent);
            ExtractGeoJsonProperties(json);
            PanelCsvOptions.Visibility = Visibility.Collapsed;
            PanelGeoOptions.Visibility = Visibility.Visible;
            RefreshGeoFieldCombos();
            LblDataInfo.Text = $"TopoJSON 로드됨";
        }
        else
        {
            _dataKind = DataKind.GeoJson;
            LblFileType.Text = "형식: GeoJSON";
            var json = JsonNode.Parse(_dataContent);
            ExtractGeoJsonProperties(json);
            PanelCsvOptions.Visibility = Visibility.Collapsed;
            PanelGeoOptions.Visibility = Visibility.Visible;
            RefreshGeoFieldCombos();

            int featureCount = json?["features"]?.AsArray().Count ?? 0;
            LblDataInfo.Text = $"GeoJSON: {featureCount}개 Feature";
        }

        LstFields.ItemsSource = _fields;
        SetStatus($"파일 로드 완료: {Path.GetFileName(path)}");
    }

    private void ExtractGeoJsonProperties(JsonNode? json)
    {
        var seen = new HashSet<string>();
        var features = json?["features"]?.AsArray();
        if (features == null) return;
        foreach (var feature in features.Take(20))
        {
            var props = feature?["properties"]?.AsObject();
            if (props == null) continue;
            foreach (var kv in props) seen.Add(kv.Key);
        }
        _fields = seen.ToList();
    }

    private void RefreshFieldCombos()
    {
        var none = new List<string> { "(없음)" };
        var allFields = none.Concat(_fields).ToList();

        CmbColorField.ItemsSource = allFields;
        CmbColorField.SelectedIndex = 0;
        CmbSizeField.ItemsSource = allFields;
        CmbSizeField.SelectedIndex = 0;
    }

    private void RefreshGeoFieldCombos()
    {
        var none = new List<string> { "(없음)" };
        var allFields = none.Concat(_fields).ToList();

        CmbGeoColorField.ItemsSource = allFields;
        CmbGeoColorField.SelectedIndex = 0;
        CmbGeoValueField.ItemsSource = allFields;
        CmbGeoValueField.SelectedIndex = 0;
    }

    private async void BtnRender_Click(object sender, RoutedEventArgs e)
    {
        if (_dataKind == DataKind.None || string.IsNullOrEmpty(_dataContent))
        {
            SetStatus("먼저 데이터 파일을 선택하세요.");
            return;
        }

        BtnRender.IsEnabled = false;
        SetStatus("렌더링 중...");
        try
        {
            if (_dataKind == DataKind.Csv)
            {
                await RenderCsvAsync();
            }
            else
            {
                await RenderGeoJsonAsync();
            }
        }
        catch (Exception ex)
        {
            SetStatus($"렌더링 실패: {ex.Message}");
        }
        finally
        {
            BtnRender.IsEnabled = true;
        }
    }

    private async Task RenderGeoJsonAsync()
    {
        var colorField = CmbGeoColorField.SelectedItem as string ?? "";
        var valueField = CmbGeoValueField.SelectedItem as string ?? "";
        if (colorField == "(없음)") colorField = "";
        if (valueField == "(없음)") valueField = "";

        // 데이터 이스케이프
        var escapedData = JsonSerializer.Serialize(_dataContent);
        var options = JsonSerializer.Serialize(new
        {
            colorField,
            valueField,
            legendTitle = colorField != "" ? colorField : valueField
        });

        var script = $"window.renderGeoJson({escapedData}, {options});";
        var result = await WebMap.CoreWebView2.ExecuteScriptAsync(script);
        SetStatus($"GeoJSON 렌더링 완료 — {Path.GetFileName(TxtFilePath.Text)}");
    }

    private async Task RenderCsvAsync()
    {
        var colorField = CmbColorField.SelectedItem as string ?? "";
        var sizeField = CmbSizeField.SelectedItem as string ?? "";
        if (colorField == "(없음)") colorField = "";
        if (sizeField == "(없음)") sizeField = "";

        // CSV → JSON 배열 변환
        var records = new List<Dictionary<string, string>>();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
        using var reader = new StringReader(_dataContent);
        using var csv = new CsvReader(reader, config);
        await foreach (var row in csv.GetRecordsAsync<dynamic>())
        {
            var dict = (IDictionary<string, object>)row;
            records.Add(dict.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? ""));
        }

        var jsonPoints = JsonSerializer.Serialize(records);
        var options = JsonSerializer.Serialize(new
        {
            colorField,
            sizeField,
            legendTitle = colorField != "" ? colorField : "점"
        });

        var escapedPoints = JsonSerializer.Serialize(jsonPoints);
        var script = $"window.renderCsvPoints({escapedPoints}, {options});";
        await WebMap.CoreWebView2.ExecuteScriptAsync(script);
        SetStatus($"CSV 렌더링 완료 — {records.Count}개 점 ({Path.GetFileName(TxtFilePath.Text)})");
    }

    private async void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        TxtFilePath.Text = "";
        LblFileType.Text = "형식: —";
        LblDataInfo.Text = "데이터 없음";
        LstFields.ItemsSource = null;
        _dataKind = DataKind.None;
        _dataContent = "";
        _fields.Clear();
        PanelCsvOptions.Visibility = Visibility.Collapsed;
        PanelGeoOptions.Visibility = Visibility.Collapsed;

        await WebMap.CoreWebView2.ExecuteScriptAsync("window.clearMap();");
        SetStatus("지도 초기화됨");
    }

    private void SetStatus(string msg) => Dispatcher.Invoke(() => StatusBar.Text = msg);
}
