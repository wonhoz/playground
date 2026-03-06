using LocaleForge.Models;

namespace LocaleForge.Parsers;

public interface ILocaleParser
{
    LocaleFileFormat Format { get; }
    Dictionary<string, string> Parse(string filePath);
    void Save(string filePath, Dictionary<string, string> entries);
    string FileFilter { get; }
}
