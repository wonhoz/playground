using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace AIPForge.Models;

/// <summary>
/// Advanced Installer 프로젝트(.aip) 문서.
/// 원본과 동일한 바이트로 다시 저장되도록 선언부 · 줄바꿈 · 들여쓰기 · BOM 여부를 기억한다.
/// </summary>
public sealed partial class AipDocument
{
    public const string CidPrefix = "caphyon.advinst.msicomp.";

    public string? FilePath { get; set; }

    /// <summary>디스크에서 읽은 원문 (BOM 제외) — 변경 사항 비교 기준</summary>
    public string LoadedText { get; private set; } = "";
    public AipElement Root { get; private set; } = new("DOCUMENT");

    private string _declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";
    private string _newLine = "\r\n";
    private string _indentUnit = "  ";
    private bool _trailingNewLine = true;
    private bool _spaceBeforeSelfClose;
    private bool _hasBom;

    public string? Directory => FilePath == null ? null : Path.GetDirectoryName(FilePath);

    // ── 로드 / 파싱 ─────────────────────────────────────────────────

    public static AipDocument Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var text = new UTF8Encoding(false).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
        var doc = Parse(text);
        doc.FilePath = path;
        doc._hasBom = hasBom;
        doc.LoadedText = text;
        return doc;
    }

    public static AipDocument Parse(string text)
    {
        var doc = new AipDocument();
        doc.DetectFormatting(text);

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = true,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
        };

        AipElement? root = null;
        var stack = new Stack<AipElement>();
        using var reader = XmlReader.Create(new StringReader(text), settings);
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.XmlDeclaration:
                    doc._declaration = $"<?xml {reader.Value}?>";
                    break;

                case XmlNodeType.Element:
                    var el = new AipElement(reader.Name);
                    if (reader.MoveToFirstAttribute())
                    {
                        do el.Attributes.Add(new AipAttr(reader.Name, reader.Value));
                        while (reader.MoveToNextAttribute());
                        reader.MoveToElement();
                    }
                    if (stack.Count > 0) stack.Peek().AddChild(el);
                    else root = el;
                    if (!reader.IsEmptyElement) stack.Push(el);
                    break;

                case XmlNodeType.EndElement:
                    stack.Pop();
                    break;

                case XmlNodeType.Text:
                case XmlNodeType.CDATA:
                    if (stack.Count > 0) stack.Peek().Text = (stack.Peek().Text ?? "") + reader.Value;
                    break;

                case XmlNodeType.Comment:
                    if (stack.Count > 0) stack.Peek().AddChild(new AipElement("#comment") { Text = reader.Value });
                    break;
            }
        }

        if (root == null) throw new InvalidDataException("XML 루트 요소가 없습니다.");
        if (root.Name != "DOCUMENT" || root.Get("Type") != "Advanced Installer")
            throw new InvalidDataException("Advanced Installer 프로젝트(.aip) 파일이 아닙니다.");
        doc.Root = root;
        return doc;
    }

    /// <summary>XML 원문 편집 결과 반영 — 파일 경로·BOM 여부는 유지</summary>
    public void ReplaceContent(string text)
    {
        var parsed = Parse(text);
        Root = parsed.Root;
        _declaration = parsed._declaration;
        _newLine = parsed._newLine;
        _indentUnit = parsed._indentUnit;
        _trailingNewLine = parsed._trailingNewLine;
        _spaceBeforeSelfClose = parsed._spaceBeforeSelfClose;
    }

    private void DetectFormatting(string text)
    {
        _newLine = text.Contains("\r\n") ? "\r\n" : "\n";
        _trailingNewLine = text.EndsWith('\n');
        // 다른 도구로 저장된 파일은 "<ROW ... />" 형식일 수 있음 — 다수 형식을 따른다
        _spaceBeforeSelfClose = CountOf(text, "\" />") > CountOf(text, "\"/>");

        // 루트 다음 첫 자식 줄의 들여쓰기를 단위로 사용
        var m = IndentRegex().Match(text);
        if (m.Success && m.Groups[1].Value.Length > 0) _indentUnit = m.Groups[1].Value;
    }

    private static int CountOf(string text, string token)
    {
        int count = 0, i = 0;
        while ((i = text.IndexOf(token, i, StringComparison.Ordinal)) >= 0) { count++; i += token.Length; }
        return count;
    }

    [GeneratedRegex(@"<DOCUMENT[^>]*>\r?\n([ \t]+)<")]
    private static partial Regex IndentRegex();

    // ── 직렬화 ──────────────────────────────────────────────────────

    public string Serialize()
    {
        var sb = new StringBuilder(256 * 1024);
        sb.Append(_declaration).Append(_newLine);
        WriteElement(sb, Root, 0);
        if (_trailingNewLine) sb.Append(_newLine);
        return sb.ToString();
    }

    public void Save(string? path = null)
    {
        path ??= FilePath ?? throw new InvalidOperationException("저장 경로가 없습니다.");
        File.WriteAllText(path, Serialize(), new UTF8Encoding(_hasBom));
        FilePath = path;
    }

    private void WriteElement(StringBuilder sb, AipElement el, int level)
    {
        for (var i = 0; i < level; i++) sb.Append(_indentUnit);

        if (el.Name == "#comment")
        {
            sb.Append("<!--").Append(el.Text).Append("-->");
            return;
        }

        sb.Append('<').Append(el.Name);
        foreach (var a in el.Attributes)
        {
            sb.Append(' ').Append(a.Name).Append("=\"");
            EscapeAttr(sb, a.Value);
            sb.Append('"');
        }

        if (el.Children.Count == 0 && el.Text == null)
        {
            sb.Append(_spaceBeforeSelfClose ? " />" : "/>");
            return;
        }

        sb.Append('>');
        if (el.Text != null) EscapeText(sb, el.Text);
        if (el.Children.Count > 0)
        {
            foreach (var c in el.Children)
            {
                sb.Append(_newLine);
                WriteElement(sb, c, level + 1);
            }
            sb.Append(_newLine);
            for (var i = 0; i < level; i++) sb.Append(_indentUnit);
        }
        sb.Append("</").Append(el.Name).Append('>');
    }

    /// <summary>AI 형식 이스케이프: &amp; &lt; &gt; &quot; 와 제어문자는 문자 참조</summary>
    private static void EscapeAttr(StringBuilder sb, string value)
    {
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\n': sb.Append("&#10;"); break;
                case '\r': sb.Append("&#13;"); break;
                case '\t': sb.Append("&#9;"); break;
                default: sb.Append(ch); break;
            }
        }
    }

    private static void EscapeText(StringBuilder sb, string value)
    {
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                default: sb.Append(ch); break;
            }
        }
    }

    // ── 문서 탐색 ───────────────────────────────────────────────────

    public string AiVersion => Root.Get("version") ?? Root.Get("CreateVersion") ?? "?";
    public string Modules => Root.Get("Modules") ?? "";

    public IEnumerable<AipElement> Components => Root.Children.Where(c => c.Name == "COMPONENT");

    public static string ShortCid(AipElement component)
    {
        var cid = component.Get("cid") ?? "";
        return cid.StartsWith(CidPrefix, StringComparison.Ordinal) ? cid[CidPrefix.Length..] : cid;
    }

    public AipElement? FindComponent(string shortCid)
        => Components.FirstOrDefault(c => ShortCid(c) == shortCid);

    public static IEnumerable<AipElement> Rows(AipElement? component)
        => component?.Children.Where(c => c.Name == "ROW") ?? [];

    public int RowCount(string shortCid) => Rows(FindComponent(shortCid)).Count();

    // ── 속성(Property) 테이블 ───────────────────────────────────────

    private AipElement? PropertyRow(string name)
        => Rows(FindComponent("MsiPropsComponent")).FirstOrDefault(r => r.Get("Property") == name);

    /// <summary>Property 값 — Value가 없으면 MultiBuildValue("DefaultBuild:값")의 첫 값</summary>
    public string? GetProperty(string name)
    {
        var row = PropertyRow(name);
        if (row == null) return null;
        var value = row.Get("Value");
        if (value != null) return value;
        var multi = row.Get("MultiBuildValue");
        if (multi == null) return null;
        var colon = multi.IndexOf(':');
        var first = colon >= 0 ? multi[(colon + 1)..] : multi;
        var sep = first.IndexOf('#');
        return sep >= 0 ? first[..sep] : first;
    }

    /// <summary>Property Value 설정 — 행이 없으면 false</summary>
    public bool SetProperty(string name, string value)
    {
        var row = PropertyRow(name);
        if (row == null) return false;
        row.Set("Value", value, ["Property", "Value", "MultiBuildValue", "ValueLocId", "Type", "MsiKey"]);
        return true;
    }

    public string ProductName => GetProperty("ProductName") ?? "";
    public string ProductVersion => GetProperty("ProductVersion") ?? "";

    /// <summary><c>[|ProductName]</c> 형식 참조를 Property 값으로 치환</summary>
    public string ExpandFormatted(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return FormattedRefRegex().Replace(text, m => GetProperty(m.Groups[1].Value) ?? m.Value);
    }

    [GeneratedRegex(@"\[\|([A-Za-z0-9_.]+)\]")]
    private static partial Regex FormattedRefRegex();

    /// <summary>프로젝트 기준 상대 경로 → 절대 경로 (경로 변수 &lt;AI_*&gt;가 들어있으면 null)</summary>
    public string? ResolvePath(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains('<')) return null;
        try
        {
            if (Path.IsPathRooted(relative)) return Path.GetFullPath(relative);
            var baseDir = Directory;
            if (baseDir == null) return null;
            var rootPath = Root.Get("RootPath");
            if (!string.IsNullOrEmpty(rootPath) && rootPath != ".") baseDir = Path.Combine(baseDir, rootPath);
            return Path.GetFullPath(Path.Combine(baseDir, relative));
        }
        catch
        {
            return null;
        }
    }

    // ── 버전 변경 ───────────────────────────────────────────────────

    /// <summary>MSI ProductVersion 형식 검사 (major ≤ 255, minor ≤ 255, build ≤ 65535)</summary>
    public static bool IsValidVersion(string version, out string error)
    {
        error = "";
        var parts = version.Split('.');
        if (parts.Length is < 2 or > 4)
        {
            error = "버전은 2~4개의 숫자를 점으로 구분해야 합니다. (예: 2.4.17)";
            return false;
        }
        int[] limits = [255, 255, 65535, 65535];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var n) || n < 0 || parts[i].Length == 0)
            {
                error = $"'{parts[i]}'은(는) 숫자가 아닙니다.";
                return false;
            }
            if (n > limits[i])
            {
                error = $"{i + 1}번째 자리는 {limits[i]} 이하여야 합니다. (MSI 규칙)";
                return false;
            }
        }
        return true;
    }

    /// <summary>마지막 자리 +1 (2.4.16 → 2.4.17)</summary>
    public static string IncrementVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length == 0 || !int.TryParse(parts[^1], out var last)) return version;
        parts[^1] = (last + 1).ToString();
        return string.Join('.', parts);
    }

    /// <summary>
    /// ProductVersion 변경. <paramref name="newProductCode"/>이면 ProductCode의 모든 GUID를 새로 발급
    /// (AI의 /SetVersion 기본 동작과 같음 — Major Upgrade가 동작하려면 필요).
    /// </summary>
    public bool SetVersion(string version, bool newProductCode)
    {
        if (!SetProperty("ProductVersion", version)) return false;
        if (newProductCode) RegenerateProductCode();
        return true;
    }

    /// <summary>ProductCode 값("1033:{GUID} 1042:{GUID} ...")의 GUID를 같은 대응 관계로 새로 발급</summary>
    public string? RegenerateProductCode()
    {
        var row = PropertyRow("ProductCode");
        var value = row?.Get("Value");
        if (row == null || value == null) return null;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var updated = GuidRegex().Replace(value, m =>
        {
            if (!map.TryGetValue(m.Value, out var fresh))
            {
                fresh = "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}";
                map[m.Value] = fresh;
            }
            return fresh;
        });
        row.Set("Value", updated);
        return map.Values.FirstOrDefault();
    }

    public string? FirstProductCode()
    {
        var value = PropertyRow("ProductCode")?.Get("Value");
        if (value == null) return null;
        var m = GuidRegex().Match(value);
        return m.Success ? m.Value : value;
    }

    [GeneratedRegex(@"\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}")]
    private static partial Regex GuidRegex();

    // ── 빌드 설정 ───────────────────────────────────────────────────

    public IEnumerable<BuildInfo> Builds()
    {
        foreach (var row in Rows(FindComponent("BuildComponent")))
        {
            var name = row.Get("BuildKey") ?? row.Get("BuildName") ?? "DefaultBuild";
            // PackageFolder가 없으면 AI 기본값 "<프로젝트명>-SetupFiles"
            var folder = row.Get("PackageFolder")
                         ?? (FilePath != null ? Path.GetFileNameWithoutExtension(FilePath) + "-SetupFiles" : null);
            var exeName = row.Get("ExeName");
            var output = exeName != null
                ? ExpandFormatted(exeName) + ".exe"
                : ExpandFormatted(row.Get("PackageFileName") ?? Path.GetFileNameWithoutExtension(FilePath ?? name)) + ".msi";
            yield return new BuildInfo(
                name,
                folder ?? "",
                ResolvePath(ExpandFormatted(folder)),
                output,
                row.Get("MsiPackageType") ?? "x86",
                row.Get("Languages") ?? "");
        }
    }

    public IEnumerable<SyncFolderInfo> SyncFolders()
    {
        foreach (var row in Rows(FindComponent("SynchronizedFolderComponent")))
        {
            var src = row.Get("SourcePath") ?? "";
            yield return new SyncFolderInfo(row.Get("Directory_") ?? "", src, ResolvePath(src), row.Get("Feature") ?? "");
        }
    }
}

public sealed record BuildInfo(string Name, string PackageFolder, string? ResolvedFolder, string OutputFileName,
    string Platform, string Languages)
{
    public string? OutputPath => ResolvedFolder == null ? null : Path.Combine(ResolvedFolder, OutputFileName);
}

public sealed record SyncFolderInfo(string Directory, string SourcePath, string? ResolvedPath, string Feature);
