using SysClean.Models;

namespace SysClean.Services;

public class RegistryService
{
    public async Task<List<RegistryIssue>> ScanAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var issues = new List<RegistryIssue>();

            ct.ThrowIfCancellationRequested();
            ScanAppPaths(issues, ct);

            ct.ThrowIfCancellationRequested();
            ScanUninstallEntries(issues, ct);

            ct.ThrowIfCacheFailed();
            ScanSharedDlls(issues, ct);

            ct.ThrowIfCancellationRequested();
            ScanStartupEntries(issues, ct);

            return issues;
        }, ct);
    }

    private static void ScanAppPaths(List<RegistryIssue> issues, CancellationToken ct)
    {
        const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
        using var key = Registry.LocalMachine.OpenSubKey(keyPath);
        if (key == null) return;

        foreach (var sub in key.GetSubKeyNames())
        {
            ct.ThrowIfCancellationRequested();
            using var subKey = key.OpenSubKey(sub);
            if (subKey == null) continue;

            var defaultVal = subKey.GetValue("")?.ToString();
            if (string.IsNullOrEmpty(defaultVal)) continue;

            var exePath = defaultVal.Trim('"');
            if (!File.Exists(exePath))
            {
                issues.Add(new RegistryIssue
                {
                    Category = "앱 경로",
                    KeyPath = $@"HKLM\{keyPath}\{sub}",
                    ValueName = "(기본값)",
                    Description = $"실행 파일 없음: {exePath}"
                });
            }
        }
    }

    private static void ScanUninstallEntries(List<RegistryIssue> issues, CancellationToken ct)
    {
        string[] basePaths =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        ];

        foreach (var basePath in basePaths)
        {
            using var key = Registry.LocalMachine.OpenSubKey(basePath);
            if (key == null) continue;

            foreach (var sub in key.GetSubKeyNames())
            {
                ct.ThrowIfCancellationRequested();
                using var subKey = key.OpenSubKey(sub);
                if (subKey == null) continue;

                var displayName = subKey.GetValue("DisplayName")?.ToString();
                if (string.IsNullOrEmpty(displayName)) continue;

                var uninstallStr = subKey.GetValue("UninstallString")?.ToString();
                var installLoc = subKey.GetValue("InstallLocation")?.ToString();

                if (!string.IsNullOrEmpty(installLoc) &&
                    !Directory.Exists(installLoc) &&
                    installLoc.Length > 3)
                {
                    issues.Add(new RegistryIssue
                    {
                        Category = "설치 경로",
                        KeyPath = $@"HKLM\{basePath}\{sub}",
                        ValueName = "InstallLocation",
                        Description = $"'{displayName}' — 설치 폴더가 존재하지 않음: {installLoc}"
                    });
                }

                if (!string.IsNullOrEmpty(uninstallStr))
                {
                    var exePath = ExtractExePath(uninstallStr);
                    if (!string.IsNullOrEmpty(exePath) && !File.Exists(exePath))
                    {
                        issues.Add(new RegistryIssue
                        {
                            Category = "제거 프로그램",
                            KeyPath = $@"HKLM\{basePath}\{sub}",
                            ValueName = "UninstallString",
                            Description = $"'{displayName}' — 제거 실행 파일 없음: {exePath}"
                        });
                    }
                }
            }
        }
    }

    private static void ScanSharedDlls(List<RegistryIssue> issues, CancellationToken ct)
    {
        const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs";
        using var key = Registry.LocalMachine.OpenSubKey(keyPath);
        if (key == null) return;

        int checked_ = 0;
        foreach (var valName in key.GetValueNames())
        {
            ct.ThrowIfCancellationRequested();
            if (++checked_ > 500) break; // 너무 많으면 제한

            var count = key.GetValue(valName);
            if (!File.Exists(valName))
            {
                issues.Add(new RegistryIssue
                {
                    Category = "공유 DLL",
                    KeyPath = $@"HKLM\{keyPath}",
                    ValueName = valName,
                    Description = $"DLL 파일이 존재하지 않음: {Path.GetFileName(valName)}"
                });
            }
        }
    }

    private static void ScanStartupEntries(List<RegistryIssue> issues, CancellationToken ct)
    {
        string[] runPaths =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
        ];

        foreach (var path in runPaths)
        {
            ct.ThrowIfCancellationRequested();
            using var key = Registry.LocalMachine.OpenSubKey(path);
            if (key == null) continue;

            foreach (var valName in key.GetValueNames())
            {
                ct.ThrowIfCancellationRequested();
                var val = key.GetValue(valName)?.ToString() ?? "";
                var exePath = ExtractExePath(val);
                if (!string.IsNullOrEmpty(exePath) && !File.Exists(exePath))
                {
                    issues.Add(new RegistryIssue
                    {
                        Category = "시작 프로그램",
                        KeyPath = $@"HKLM\{path}",
                        ValueName = valName,
                        Description = $"실행 파일 없음: {exePath}"
                    });
                }
            }
        }
    }

    private static string ExtractExePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return "";

        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end > 1)
                return command[1..end];
        }

        var spaceIdx = command.IndexOf(' ');
        var candidate = spaceIdx > 0 ? command[..spaceIdx] : command;
        return candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? candidate : "";
    }

    public async Task FixIssuesAsync(IEnumerable<RegistryIssue> issues, CancellationToken ct)
    {
        await Task.Run(() =>
        {
            foreach (var issue in issues)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    FixIssue(issue);
                    issue.IsFixed = true;
                }
                catch { /* 접근 거부 무시 */ }
            }
        }, ct);
    }

    private static void FixIssue(RegistryIssue issue)
    {
        // HKLM\... 형식에서 실제 키 경로 추출
        var path = issue.KeyPath;
        if (!path.StartsWith(@"HKLM\")) return;

        var subPath = path[@"HKLM\".Length..];
        using var key = Registry.LocalMachine.OpenSubKey(subPath, writable: true);
        if (key == null) return;

        if (issue.Category == "공유 DLL" || issue.Category == "앱 경로" || issue.Category == "시작 프로그램")
        {
            // 값 삭제
            key.DeleteValue(issue.ValueName, throwOnMissingValue: false);
        }
        else if (issue.Category is "설치 경로" or "제거 프로그램")
        {
            // 상위 키에서 서브키 삭제
            var parentPath = subPath[..subPath.LastIndexOf('\\')];
            var subKeyName = subPath[(subPath.LastIndexOf('\\') + 1)..];
            using var parentKey = Registry.LocalMachine.OpenSubKey(parentPath, writable: true);
            parentKey?.DeleteSubKeyTree(subKeyName, throwOnMissingSubKey: false);
        }
    }
}

internal static class CancellationTokenExtensions
{
    public static void ThrowIfCacheFailed(this CancellationToken ct) => ct.ThrowIfCancellationRequested();
}
