using QuickLauncher.Models;

namespace QuickLauncher.Services;

/// <summary>URL / 스니펫 커스텀 항목 제공</summary>
public class BuiltinProvider
{
    private readonly List<LaunchItem> _customs = [];

    public void Reload(IEnumerable<Models.CustomItem> items)
    {
        _customs.Clear();
        foreach (var item in items)
        {
            _customs.Add(new LaunchItem
            {
                Name     = item.Name,
                Subtitle = item.IsSnippet ? "클립보드 복사" : item.Target,
                Icon     = item.IsSnippet ? "📋" : "🌐",
                Type     = item.IsSnippet ? LaunchItemType.Snippet : LaunchItemType.Url,
                Target   = item.Target,
            });
        }
    }

    public IEnumerable<LaunchItem> GetAll() => _customs;
}
