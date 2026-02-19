using System.Diagnostics;
using System.Windows;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public static class LaunchExecutor
{
    public static void Execute(LaunchItem item)
    {
        try
        {
            switch (item.Type)
            {
                case LaunchItemType.App:
                case LaunchItemType.Url:
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = item.Target,
                        UseShellExecute = true
                    })?.Dispose();
                    break;

                case LaunchItemType.Snippet:
                    Clipboard.SetText(item.Target);
                    break;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"실행 실패: {ex.Message}", "Quick.Launcher",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
