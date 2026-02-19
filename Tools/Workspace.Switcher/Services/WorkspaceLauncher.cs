using System.Diagnostics;
using System.Windows;
using WorkspaceSwitcher.Models;

namespace WorkspaceSwitcher.Services;

public static class WorkspaceLauncher
{
    /// <summary>워크스페이스의 모든 앱을 순서대로 실행</summary>
    public static async Task LaunchAsync(Workspace workspace)
    {
        var errors = new List<string>();

        foreach (var app in workspace.Apps)
        {
            try
            {
                await Task.Delay(300); // 앱 간 실행 간격
                Process.Start(new ProcessStartInfo
                {
                    FileName        = app.Path,
                    Arguments       = app.Args,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                errors.Add($"• {app.Name}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            MessageBox.Show(
                $"일부 앱 실행 실패:\n{string.Join("\n", errors)}",
                workspace.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
