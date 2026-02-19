using System.IO;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

/// <summary>시작 메뉴 .lnk 파일을 스캔하여 앱 목록 제공</summary>
public class AppSearchProvider
{
    private List<LaunchItem> _cache = [];

    public void BuildIndex()
    {
        var items = new List<LaunchItem>();
        var folders = new[]
        {
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),       "Programs"),
        };

        foreach (var folder in folders.Where(System.IO.Directory.Exists))
        {
            foreach (var lnk in System.IO.Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories))
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(lnk);
                var dir  = System.IO.Path.GetDirectoryName(lnk)!;
                var sub  = dir.Replace(folder, "").TrimStart('\\', '/');

                items.Add(new LaunchItem
                {
                    Name     = name,
                    Subtitle = string.IsNullOrEmpty(sub) ? "앱" : sub,
                    Icon     = "🖥️",
                    Type     = LaunchItemType.App,
                    Target   = lnk,
                });
            }
        }

        _cache = items;
    }

    public IEnumerable<LaunchItem> GetAll() => _cache;
}
