using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;

namespace TextForge.Views;

public partial class JsonXmlView : UserControl
{
    public JsonXmlView() => InitializeComponent();

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        StatusText.Text = string.Empty;
    }

    private void PrettyPrint_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        if (TryFormatJson(input, out var jsonResult))
        {
            OutputBox.Text = jsonResult;
            ShowStatus("✓ JSON 포맷팅 완료", "#4FC3F7");
            return;
        }

        if (TryFormatXml(input, out var xmlResult))
        {
            OutputBox.Text = xmlResult;
            ShowStatus("✓ XML 포맷팅 완료", "#81C784");
            return;
        }

        ShowStatus("✕ 유효하지 않은 JSON / XML", "#EF9A9A");
    }

    private void Minify_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        if (TryMinifyJson(input, out var jsonMin))
        {
            OutputBox.Text = jsonMin;
            ShowStatus("✓ JSON 압축 완료", "#4FC3F7");
            return;
        }

        if (TryMinifyXml(input, out var xmlMin))
        {
            OutputBox.Text = xmlMin;
            ShowStatus("✓ XML 압축 완료", "#81C784");
            return;
        }

        ShowStatus("✕ 유효하지 않은 JSON / XML", "#EF9A9A");
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        if (TryFormatJson(input, out _))
        {
            ShowStatus("✓ 유효한 JSON", "#81C784");
            return;
        }

        if (TryFormatXml(input, out _))
        {
            ShowStatus("✓ 유효한 XML", "#81C784");
            return;
        }

        ShowStatus("✕ 유효하지 않은 형식", "#EF9A9A");
    }

    private void XmlToJson_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        try
        {
            var xml = XDocument.Parse(input);
            var json = XmlToJsonSimple(xml.Root!);
            OutputBox.Text = json;
            ShowStatus("✓ XML → JSON 변환 완료", "#81C784");
        }
        catch (Exception ex)
        {
            ShowStatus($"✕ XML 파싱 오류: {ex.Message}", "#EF9A9A");
        }
    }

    private void JsonToXml_Click(object sender, RoutedEventArgs e)
    {
        ShowStatus("JSON → XML 변환은 추후 지원 예정", "#888888");
    }

    private void Swap_Click(object sender, RoutedEventArgs e)
    {
        (InputBox.Text, OutputBox.Text) = (OutputBox.Text, InputBox.Text);
    }

    private void CopyOutput_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(OutputBox.Text))
        {
            Clipboard.SetText(OutputBox.Text);
            ShowStatus("✓ 클립보드에 복사됨", "#81C784");
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        InputBox.Text   = string.Empty;
        OutputBox.Text  = string.Empty;
        StatusText.Text = string.Empty;
    }

    // ── 헬퍼 ─────────────────────────────────────────────────

    private static bool TryFormatJson(string input, out string result)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            result = JsonSerializer.Serialize(doc.RootElement,
                new JsonSerializerOptions { WriteIndented = true });
            return true;
        }
        catch { result = string.Empty; return false; }
    }

    private static bool TryMinifyJson(string input, out string result)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            result = JsonSerializer.Serialize(doc.RootElement);
            return true;
        }
        catch { result = string.Empty; return false; }
    }

    private static bool TryFormatXml(string input, out string result)
    {
        try
        {
            var xml = XDocument.Parse(input);
            var sb  = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent             = true,
                IndentChars        = "  ",
                OmitXmlDeclaration = false
            };
            using (var writer = XmlWriter.Create(sb, settings))
                xml.Save(writer);
            result = sb.ToString();
            return true;
        }
        catch { result = string.Empty; return false; }
    }

    private static bool TryMinifyXml(string input, out string result)
    {
        try
        {
            var xml = XDocument.Parse(input);
            result = xml.ToString(SaveOptions.DisableFormatting);
            return true;
        }
        catch { result = string.Empty; return false; }
    }

    // 단순 XML → JSON 변환 (재귀적 구조)
    private static string XmlToJsonSimple(XElement element)
    {
        var sb = new StringBuilder();
        BuildJsonFromXml(element, sb, 0);
        return sb.ToString();
    }

    private static void BuildJsonFromXml(XElement el, StringBuilder sb, int depth)
    {
        var indent  = new string(' ', depth * 2);
        var indent2 = new string(' ', (depth + 1) * 2);
        var children = el.Elements().ToList();

        if (!children.Any())
        {
            var val = el.Value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            sb.Append($"\"{val}\"");
            return;
        }

        var grouped = children.GroupBy(c => c.Name.LocalName).ToList();
        sb.AppendLine("{");

        for (int i = 0; i < grouped.Count; i++)
        {
            var group = grouped[i];
            sb.Append($"{indent2}\"{group.Key}\": ");

            if (group.Count() > 1)
            {
                sb.AppendLine("[");
                var list = group.ToList();
                for (int j = 0; j < list.Count; j++)
                {
                    sb.Append($"{indent2}  ");
                    BuildJsonFromXml(list[j], sb, depth + 2);
                    if (j < list.Count - 1) sb.AppendLine(",");
                    else sb.AppendLine();
                }
                sb.Append($"{indent2}]");
            }
            else
            {
                BuildJsonFromXml(group.First(), sb, depth + 1);
            }

            if (i < grouped.Count - 1) sb.AppendLine(",");
            else sb.AppendLine();
        }

        sb.Append($"{indent}}}");
    }

    private void ShowStatus(string msg, string colorHex)
    {
        StatusText.Text = msg;
        StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex)!);
    }
}
