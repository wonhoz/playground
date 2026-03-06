using LocaleForge.Models;

namespace LocaleForge.Parsers;

public static class ParserRegistry
{
    private static readonly Dictionary<LocaleFileFormat, ILocaleParser> _parsers = new()
    {
        [LocaleFileFormat.Json] = new JsonParser(),
        [LocaleFileFormat.Yaml] = new YamlParser(),
        [LocaleFileFormat.Resx] = new ResxParser(),
        [LocaleFileFormat.Po] = new PoParser(),
        [LocaleFileFormat.Properties] = new PropertiesParser(),
    };

    public static ILocaleParser Get(LocaleFileFormat format) => _parsers[format];

    public static ILocaleParser GetForFile(string filePath) =>
        _parsers[LocaleFile.DetectFormat(filePath)];

    public static string AllFilesFilter =>
        "i18n 파일 (*.json;*.yaml;*.yml;*.resx;*.po;*.properties)|*.json;*.yaml;*.yml;*.resx;*.po;*.properties|" +
        string.Join("|", _parsers.Values.Select(p => p.FileFilter));
}
