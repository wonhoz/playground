namespace CaseForge.Services;

public static class CaseConverter
{
    // ── 단어 파싱 ────────────────────────────────────────────────────────
    public static List<string> ParseWords(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];

        // camelCase / PascalCase 분리: 소문자→대문자 경계 삽입
        var s = Regex.Replace(input.Trim(), @"([a-z\d])([A-Z])", "$1 $2");
        // 연속 대문자 처리: HTMLParser → HTML Parser
        s = Regex.Replace(s, @"([A-Z]+)([A-Z][a-z])", "$1 $2");
        // 숫자↔문자 경계 분리: abc123def → abc 123 def
        s = Regex.Replace(s, @"([a-zA-Z])(\d)", "$1 $2");
        s = Regex.Replace(s, @"(\d)([a-zA-Z])", "$1 $2");

        return [.. Regex.Split(s, @"[\s\-_./\\|,;:]+")
                        .Select(w => w.Trim())
                        .Where(w => !string.IsNullOrEmpty(w))];
    }

    // ── 10가지 케이스 변환 ───────────────────────────────────────────────
    public static string ToCamelCase(string input)
    {
        var words = ParseWords(input);
        if (words.Count == 0) return string.Empty;
        return words[0].ToLower() +
               string.Concat(words.Skip(1).Select(Capitalize));
    }

    public static string ToPascalCase(string input)
        => string.Concat(ParseWords(input).Select(Capitalize));

    public static string ToSnakeCase(string input)
        => string.Join("_", ParseWords(input).Select(w => w.ToLower()));

    public static string ToScreamingSnakeCase(string input)
        => string.Join("_", ParseWords(input).Select(w => w.ToUpper()));

    public static string ToKebabCase(string input)
        => string.Join("-", ParseWords(input).Select(w => w.ToLower()));

    public static string ToTrainCase(string input)
        => string.Join("-", ParseWords(input).Select(Capitalize));

    public static string ToDotCase(string input)
        => string.Join(".", ParseWords(input).Select(w => w.ToLower()));

    public static string ToTitleCase(string input)
        => string.Join(" ", ParseWords(input).Select(Capitalize));

    public static string ToUpperCase(string input)
        => string.Join(" ", ParseWords(input).Select(w => w.ToUpper()));

    public static string ToLowerCase(string input)
        => string.Join(" ", ParseWords(input).Select(w => w.ToLower()));

    public static string ToPathCase(string input)
        => string.Join("/", ParseWords(input).Select(w => w.ToLower()));

    // ── 전체 변환 목록 ───────────────────────────────────────────────────
    public static (string Label, string Key, string Value)[] ConvertAll(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Definitions.Select(d => (d.Label, d.Key, string.Empty)).ToArray();

        return Definitions.Select(d => (d.Label, d.Key, d.Convert(input))).ToArray();
    }

    public static readonly (string Label, string Key, Func<string, string> Convert)[] Definitions =
    [
        ("camelCase",           "camel",   ToCamelCase),
        ("PascalCase",          "pascal",  ToPascalCase),
        ("snake_case",          "snake",   ToSnakeCase),
        ("SCREAMING_SNAKE",     "scream",  ToScreamingSnakeCase),
        ("kebab-case",          "kebab",   ToKebabCase),
        ("Train-Case",          "train",   ToTrainCase),
        ("dot.case",            "dot",     ToDotCase),
        ("Title Case",          "title",   ToTitleCase),
        ("UPPER CASE",          "upper",   ToUpperCase),
        ("lower case",          "lower",   ToLowerCase),
        ("path/case",           "path",    ToPathCase),
    ];

    private static string Capitalize(string w)
        => w.Length == 0 ? w : char.ToUpper(w[0]) + w[1..].ToLower();
}
