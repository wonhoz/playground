using System.Text;
using System.Xml.Linq;
using LocaleForge.Models;

namespace LocaleForge.Parsers;

public class ResxParser : ILocaleParser
{
    public LocaleFileFormat Format => LocaleFileFormat.Resx;
    public string FileFilter => "RESX 파일 (*.resx)|*.resx";

    public Dictionary<string, string> Parse(string filePath)
    {
        var result = new Dictionary<string, string>();
        var doc = XDocument.Load(filePath);

        foreach (var data in doc.Root?.Elements("data") ?? Enumerable.Empty<XElement>())
        {
            var name = data.Attribute("name")?.Value;
            var value = data.Element("value")?.Value;
            if (name != null)
                result[name] = value ?? string.Empty;
        }

        return result;
    }

    public void Save(string filePath, Dictionary<string, string> entries)
    {
        var doc = new XDocument(
            new XElement("root",
                new XElement("resheader",
                    new XAttribute("name", "resmimetype"),
                    new XElement("value", "text/microsoft-resx")),
                new XElement("resheader",
                    new XAttribute("name", "version"),
                    new XElement("value", "2.0")),
                entries.OrderBy(e => e.Key).Select(e =>
                    new XElement("data",
                        new XAttribute("name", e.Key),
                        new XAttribute(XNamespace.Xml + "space", "preserve"),
                        new XElement("value", e.Value)))
            )
        );

        doc.Save(filePath);
    }
}
