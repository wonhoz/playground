using SysClean.Models;

namespace SysClean.Services;

public class StartupService
{
    private const string HklmRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string HkcuRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public List<StartupEntry> GetEntries()
    {
        var entries = new List<StartupEntry>();

        // HKLM — 모든 사용자
        using (var key = Registry.LocalMachine.OpenSubKey(HklmRunPath))
        {
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    var cmd = key.GetValue(name)?.ToString() ?? "";
                    entries.Add(new StartupEntry
                    {
                        Name = name,
                        Command = cmd,
                        Location = StartupLocation.HklmRun,
                        IsEnabled = true,
                        ImpactLevel = EstimateImpact(cmd)
                    });
                }
            }
        }

        // HKCU — 현재 사용자
        using (var key = Registry.CurrentUser.OpenSubKey(HkcuRunPath))
        {
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    var cmd = key.GetValue(name)?.ToString() ?? "";
                    entries.Add(new StartupEntry
                    {
                        Name = name,
                        Command = cmd,
                        Location = StartupLocation.HkcuRun,
                        IsEnabled = true,
                        ImpactLevel = EstimateImpact(cmd)
                    });
                }
            }
        }

        // 비활성화된 항목 (Sysinternals Autoruns 방식)
        const string disabledBase = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        AddDisabledEntries(entries, Registry.LocalMachine, disabledBase, StartupLocation.HklmRun);
        AddDisabledEntries(entries, Registry.CurrentUser, disabledBase, StartupLocation.HkcuRun);

        return entries.DistinctBy(e => $"{e.Location}|{e.Name}").ToList();
    }

    private static void AddDisabledEntries(List<StartupEntry> entries, RegistryKey hive,
        string keyPath, StartupLocation loc)
    {
        using var key = hive.OpenSubKey(keyPath);
        if (key == null) return;

        foreach (var name in key.GetValueNames())
        {
            var data = key.GetValue(name) as byte[];
            if (data == null || data.Length < 4) continue;
            bool disabled = data[0] == 3 || data[0] == 1; // 3=disabled, 2=enabled
            if (!disabled) continue;

            var existing = entries.FirstOrDefault(e => e.Name == name && e.Location == loc);
            if (existing != null)
            {
                existing.IsEnabled = false;
            }
            else
            {
                // StartupApproved에만 있고 Run에는 없는 경우 스킵
            }
        }
    }

    private static string EstimateImpact(string command)
    {
        // 명령에서 실행 파일 경로 추출
        var path = command.Trim().TrimStart('"');
        var endQuote = path.IndexOf('"');
        if (endQuote > 0) path = path[..endQuote];
        path = path.Split(' ')[0];

        try
        {
            if (File.Exists(path))
            {
                long size = new FileInfo(path).Length;
                if (size > 50 * 1024 * 1024) return "높음";  // 50MB 초과
                if (size > 10 * 1024 * 1024) return "중간";  // 10MB 초과
                return "낮음";
            }
        }
        catch { /* 파일 접근 실패 무시 */ }
        return "—";
    }

    public bool SetEnabled(StartupEntry entry, bool enabled)
    {
        const string approvedBase = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        var hive = entry.Location == StartupLocation.HklmRun
            ? Registry.LocalMachine
            : Registry.CurrentUser;

        try
        {
            using var key = hive.CreateSubKey(approvedBase, writable: true);
            if (key == null) return false;

            // 2 = enabled, 3 = disabled (8바이트 BINARY)
            var data = new byte[12];
            data[0] = (byte)(enabled ? 2 : 3);
            key.SetValue(entry.Name, data, RegistryValueKind.Binary);

            entry.IsEnabled = enabled;
            return true;
        }
        catch { return false; }
    }

    public bool Add(string name, string command, StartupLocation location)
    {
        var runPath = location == StartupLocation.HklmRun ? HklmRunPath : HkcuRunPath;
        var hive = location == StartupLocation.HklmRun
            ? Registry.LocalMachine
            : Registry.CurrentUser;
        try
        {
            using var key = hive.OpenSubKey(runPath, writable: true);
            if (key == null) return false;
            key.SetValue(name, command, RegistryValueKind.String);
            return true;
        }
        catch { return false; }
    }

    public bool Delete(StartupEntry entry)
    {
        var runPath = entry.Location == StartupLocation.HklmRun ? HklmRunPath : HkcuRunPath;
        var hive = entry.Location == StartupLocation.HklmRun
            ? Registry.LocalMachine
            : Registry.CurrentUser;

        try
        {
            using var key = hive.OpenSubKey(runPath, writable: true);
            key?.DeleteValue(entry.Name, throwOnMissingValue: false);

            const string approvedBase = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
            using var approvedKey = hive.OpenSubKey(approvedBase, writable: true);
            approvedKey?.DeleteValue(entry.Name, throwOnMissingValue: false);

            return true;
        }
        catch { return false; }
    }
}
