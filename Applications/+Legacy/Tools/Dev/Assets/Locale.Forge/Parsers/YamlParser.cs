using System.IO;
using System.Text;
using LocaleForge.Models;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace LocaleForge.Parsers;

public class YamlParser : ILocaleParser
{
    public LocaleFileFormat Format => LocaleFileFormat.Yaml;
    public string FileFilter => "YAML 파일 (*.yaml;*.yml)|*.yaml;*.yml";

    public Dictionary<string, string> Parse(string filePath)
    {
        var result = new Dictionary<string, string>();
        var yaml = new YamlStream();
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        yaml.Load(reader);

        if (yaml.Documents.Count == 0) return result;
        if (yaml.Documents[0].RootNode is YamlMappingNode root)
            FlattenYaml(root, string.Empty, result);

        return result;
    }

    private static void FlattenYaml(YamlNode node, string prefix, Dictionary<string, string> result)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var entry in mapping.Children)
                {
                    var key = string.IsNullOrEmpty(prefix)
                        ? entry.Key.ToString()
                        : $"{prefix}.{entry.Key}";
                    FlattenYaml(entry.Value, key, result);
                }
                break;
            case YamlScalarNode scalar:
                result[prefix] = scalar.Value ?? string.Empty;
                break;
        }
    }

    public void Save(string filePath, Dictionary<string, string> entries)
    {
        var root = new Dictionary<object, object>();
        foreach (var (key, value) in entries.OrderBy(e => e.Key))
        {
            SetNested(root, key.Split('.'), value);
        }

        var serializer = new SerializerBuilder().Build();
        var yaml = serializer.Serialize(root);
        File.WriteAllText(filePath, yaml, new UTF8Encoding(true));
    }

    private static void SetNested(Dictionary<object, object> node, string[] parts, string value)
    {
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (!node.ContainsKey(parts[i]))
                node[parts[i]] = new Dictionary<object, object>();
            if (node[parts[i]] is Dictionary<object, object> child)
                node = child;
            else
                return;
        }
        node[parts[^1]] = value;
    }
}
