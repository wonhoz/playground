namespace CopyPath.Services;

public static class ExplorerHelper
{
    /// <summary>탐색기에서 현재 선택된 파일/폴더 경로를 반환합니다.</summary>
    public static string? GetSelectedPath()
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return null;

            dynamic shell   = Activator.CreateInstance(shellType)!;
            dynamic windows = shell.Windows();
            int count = windows.Count;

            for (int i = 0; i < count; i++)
            {
                try
                {
                    dynamic win = windows.Item(i);
                    if (win == null) continue;

                    string? loc = win.LocationURL as string;
                    if (string.IsNullOrEmpty(loc)) continue;

                    dynamic doc      = win.Document;
                    dynamic selected = doc.SelectedItems();
                    int selCount     = selected.Count;
                    if (selCount == 0) continue;

                    dynamic item = selected.Item(0);
                    string path  = item.Path;
                    return path;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    /// <summary>탐색기 현재 폴더(선택 없을 때)를 반환합니다.</summary>
    public static string? GetCurrentFolderPath()
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return null;

            dynamic shell   = Activator.CreateInstance(shellType)!;
            dynamic windows = shell.Windows();
            int count = windows.Count;

            for (int i = 0; i < count; i++)
            {
                try
                {
                    dynamic win = windows.Item(i);
                    if (win == null) continue;

                    string? loc = win.LocationURL as string;
                    if (string.IsNullOrEmpty(loc)) continue;

                    // file:///C:/... → C:\...
                    var uri = new Uri(loc);
                    return Uri.UnescapeDataString(uri.LocalPath);
                }
                catch { }
            }
        }
        catch { }
        return null;
    }
}
