using System.IO;
using System.Text;
using System.Text.Json;
using LocaleForge.Models;

namespace LocaleForge.Parsers;

public class JsonParser : ILocaleParser
{
    public LocaleFileFormat Format => LocaleFileFormat.Json;
    public string FileFilter => "JSON 파일 (*.json)|*.json";

    public Dictionary<string, string> Parse(string filePath)
    {
        var result = new Dictionary<string, string>();
        var json = File.ReadAllText(filePath, Encoding.UTF8);
        using var doc = JsonDocument.Parse(json);
        FlattenJson(doc.RootElement, string.Empty, result);
        return result;
    }

    private static void FlattenJson(JsonElement element, string prefix, Dictionary<string, string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    FlattenJson(prop.Value, key, result);
                }
                break;
            case JsonValueKind.String:
                result[prefix] = element.GetString() ?? string.Empty;
                break;
            default:
                result[prefix] = element.ToString();
                break;
        }
    }

    public void Save(string filePath, Dictionary<string, string> entries)
    {
        // 플랫 키를 중첩 구조로 재조립
        var root = new Dictionary<string, object>();
        foreach (var (key, value) in entries.OrderBy(e => e.Key))
        {
            SetNested(root, key.Split('.'), value);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(root, options);
        File.WriteAllText(filePath, json, new UTF8Encoding(true));
    }

    private static void SetNested(Dictionary<string, object> node, string[] parts, string value)
    {
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (!node.ContainsKey(parts[i]))
                node[parts[i]] = new Dictionary<string, object>();
            if (node[parts[i]] is Dictionary<string, object> child)
                node = child;
            else
                return;
        }
        node[parts[^1]] = value;
    }
}
