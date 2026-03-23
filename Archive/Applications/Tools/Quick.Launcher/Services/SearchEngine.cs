using QuickLauncher.Models;

namespace QuickLauncher.Services;

public class SearchEngine
{
    private readonly AppSearchProvider  _apps;
    private readonly BuiltinProvider    _builtins;

    public SearchEngine(AppSearchProvider apps, BuiltinProvider builtins)
    {
        _apps     = apps;
        _builtins = builtins;
    }

    /// <summary>query로 최대 maxResults개의 결과를 점수 내림차순으로 반환</summary>
    public List<LaunchItem> Search(string query, int maxResults = 8)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var q = query.Trim();

        return _apps.GetAll()
            .Concat(_builtins.GetAll())
            .Select(item => { item.Score = Score(item.Name, q); return item; })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Name.Length)
            .Take(maxResults)
            .ToList();
    }

    private static int Score(string name, string query)
    {
        var n = name.ToLowerInvariant();
        var q = query.ToLowerInvariant();

        if (n == q)            return 100;
        if (n.StartsWith(q))   return 90;
        if (n.Contains(q))     return 70;

        // 문자 순서 포함 여부 (fuzzy)
        int pos = 0;
        foreach (char c in q)
        {
            int idx = n.IndexOf(c, pos);
            if (idx < 0) return 0;
            pos = idx + 1;
        }
        return 50;
    }
}
