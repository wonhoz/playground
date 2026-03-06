namespace LocaleForge.Models;

public enum LocaleFileFormat
{
    Json,
    Yaml,
    Resx,
    Po,
    Properties
}

public class LocaleFile
{
    public string FilePath { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public LocaleFileFormat Format { get; set; }
    public Dictionary<string, string> Entries { get; set; } = new();

    public string DisplayName => $"{LanguageCode} ({System.IO.Path.GetFileName(FilePath)})";

    public static LocaleFileFormat DetectFormat(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".json" => LocaleFileFormat.Json,
            ".yaml" or ".yml" => LocaleFileFormat.Yaml,
            ".resx" => LocaleFileFormat.Resx,
            ".po" => LocaleFileFormat.Po,
            ".properties" => LocaleFileFormat.Properties,
            _ => LocaleFileFormat.Json
        };
    }
}
