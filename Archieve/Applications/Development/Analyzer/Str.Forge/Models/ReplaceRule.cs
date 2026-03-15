using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace StrForge.Models;

public class ReplaceRule
{
    public string Pattern { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public bool IsRegex { get; set; } = false;
    public bool IgnoreCase { get; set; } = false;
    public bool WholeWord { get; set; } = false;
    public bool IsEnabled { get; set; } = true;
    public string Label { get; set; } = string.Empty;

    [JsonIgnore]
    public string? CompileError { get; private set; }

    [JsonIgnore]
    public Regex? CompiledRegex { get; private set; }

    public bool TryCompile()
    {
        CompileError = null;
        CompiledRegex = null;
        if (!IsEnabled || string.IsNullOrEmpty(Pattern)) return true;
        try
        {
            var opts = RegexOptions.None;
            if (IgnoreCase) opts |= RegexOptions.IgnoreCase;
            var pattern = IsRegex ? Pattern : Regex.Escape(Pattern);
            if (WholeWord) pattern = $@"\b{pattern}\b";
            CompiledRegex = new Regex(pattern, opts, TimeSpan.FromSeconds(2));
            return true;
        }
        catch (Exception ex)
        {
            CompileError = ex.Message;
            return false;
        }
    }

    public string Apply(string input)
    {
        if (CompiledRegex == null || !IsEnabled) return input;
        return CompiledRegex.Replace(input, Replacement);
    }
}

public class RuleSet
{
    public string Name { get; set; } = "새 규칙 세트";
    public string GlobPattern { get; set; } = "**/*.cs";
    public List<ReplaceRule> Rules { get; set; } = [];
}
