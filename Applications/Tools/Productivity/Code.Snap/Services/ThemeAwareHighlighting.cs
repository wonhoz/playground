using ICSharpCode.AvalonEdit.Highlighting;

namespace CodeSnap.Services;

/// <summary>
/// AvalonEdit HighlightingColor는 내부적으로 Freeze()되어 직접 수정 불가.
/// IHighlightingDefinition 래퍼로 GetNamedColor() 호출 시 테마 색상으로 교체 반환.
/// </summary>
internal sealed class ThemeAwareHighlightingDefinition : IHighlightingDefinition
{
    private readonly IHighlightingDefinition _base;
    private readonly Dictionary<string, HighlightingColor> _overrides;

    public ThemeAwareHighlightingDefinition(
        IHighlightingDefinition baseDefinition,
        Dictionary<string, HighlightingColor> overrides)
    {
        _base = baseDefinition;
        _overrides = overrides;
    }

    public string Name => _base.Name;

    public HighlightingRuleSet MainRuleSet => _base.MainRuleSet;

    public IEnumerable<HighlightingColor> NamedHighlightingColors => _base.NamedHighlightingColors;

    public IDictionary<string, string> Properties => _base.Properties;

    public HighlightingColor? GetNamedColor(string name) =>
        _overrides.TryGetValue(name, out var c) ? c : _base.GetNamedColor(name);

    public HighlightingRuleSet? GetNamedRuleSet(string name) => _base.GetNamedRuleSet(name);
}
