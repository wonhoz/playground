using System.Runtime.InteropServices;

namespace CopyPath.Services;

public record ExplorerResult(string? SelectedPath, string[] AllSelectedPaths, string? FolderPath);

public static class ExplorerHelper
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// 탐색기에서 경로를 가져옵니다.
    /// 포그라운드(활성) 탐색기 창을 우선하며, 단일 Shell 인스턴스로 선택 경로와 폴더 경로를 함께 반환합니다.
    /// 복수 선택 시 AllSelectedPaths에 모든 경로가 포함됩니다.
    /// </summary>
    public static ExplorerResult GetPaths()
    {
        try
        {
            var fgHwnd = GetForegroundWindow();
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return new(null, [], null);

            dynamic shell   = Activator.CreateInstance(shellType)!;
            dynamic windows = shell.Windows();
            int count = windows.Count;

            // Pass 0: 포그라운드 창 우선 / Pass 1: 나머지 창
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        dynamic win = windows.Item(i);
                        if (win == null) continue;

                        string? loc = win.LocationURL as string;
                        if (string.IsNullOrEmpty(loc)) continue;

                        if (pass == 0 && (IntPtr)(int)win.HWND != fgHwnd) continue;

                        // 폴더 경로
                        string? folderPath = null;
                        try
                        {
                            var uri = new Uri(loc);
                            folderPath = Uri.UnescapeDataString(uri.LocalPath);
                        }
                        catch { }

                        // 선택 항목 (복수 포함)
                        var selectedPaths = new List<string>();
                        try
                        {
                            dynamic doc      = win.Document;
                            dynamic selected = doc.SelectedItems();
                            int selCount     = selected.Count;
                            for (int j = 0; j < selCount; j++)
                            {
                                try { selectedPaths.Add((string)selected.Item(j).Path); } catch { }
                            }
                        }
                        catch { }

                        string? firstSelected = selectedPaths.Count > 0 ? selectedPaths[0] : null;
                        return new(firstSelected, [.. selectedPaths], folderPath);
                    }
                    catch { }
                }
            }
        }
        catch { }
        return new(null, [], null);
    }
}
