namespace WinEvent.Services;

public sealed class AlertService
{
    private static readonly string _rulesPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "WinEvent", "alert_rules.json");

    private List<AlertRule> _rules = [];

    public IReadOnlyList<AlertRule> Rules => _rules;

    // ── 로드/저장 ────────────────────────────────────────────────────

    public void Load()
    {
        try
        {
            if (!File.Exists(_rulesPath)) return;
            var json = File.ReadAllText(_rulesPath);
            _rules = JsonSerializer.Deserialize<List<AlertRule>>(json) ?? [];
        }
        catch { _rules = []; }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_rulesPath)!);
            var json = JsonSerializer.Serialize(_rules,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_rulesPath, json);
        }
        catch { }
    }

    public void SetRules(List<AlertRule> rules)
    {
        _rules = rules;
        Save();
    }

    // ── 매칭 ─────────────────────────────────────────────────────────

    /// <summary>이벤트가 활성화된 규칙과 매칭되면 규칙 이름을 반환합니다.</summary>
    public string? GetMatchedRuleName(EventItem item)
    {
        foreach (var rule in _rules)
            if (rule.Matches(item)) return rule.Name;
        return null;
    }
}
