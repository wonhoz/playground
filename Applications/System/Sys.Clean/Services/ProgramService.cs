using SysClean.Models;

namespace SysClean.Services;

public class ProgramService
{
    public List<InstalledProgram> GetInstalledPrograms()
    {
        var programs = new List<InstalledProgram>();

        string[] paths =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        ];

        foreach (var path in paths)
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            if (key == null) continue;
            ReadUninstallKey(key, path, programs);
        }

        // 현재 사용자
        using var userKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        if (userKey != null)
            ReadUninstallKey(userKey, "HKCU\\Uninstall", programs);

        return programs
            .Where(p => !string.IsNullOrEmpty(p.Name))
            .DistinctBy(p => p.Name)
            .OrderBy(p => p.Name)
            .ToList();
    }

    private static void ReadUninstallKey(RegistryKey key, string basePath, List<InstalledProgram> programs)
    {
        foreach (var subName in key.GetSubKeyNames())
        {
            using var sub = key.OpenSubKey(subName);
            if (sub == null) continue;

            var displayName = sub.GetValue("DisplayName")?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(displayName)) continue;

            // 시스템 업데이트, 핫픽스 제외
            var releaseType = sub.GetValue("ReleaseType")?.ToString() ?? "";
            var parentKeyName = sub.GetValue("ParentKeyName")?.ToString() ?? "";
            if (releaseType.Contains("Update") || parentKeyName.Contains("Update")) continue;
            if (displayName.StartsWith("KB") && displayName.Length < 15) continue;

            var systemComponent = sub.GetValue("SystemComponent");
            if (systemComponent != null && (int)systemComponent == 1) continue;

            long sizeKb = 0;
            var sizeVal = sub.GetValue("EstimatedSize");
            if (sizeVal is int sizeInt) sizeKb = sizeInt;

            var rawDate = sub.GetValue("InstallDate")?.ToString() ?? "";
            var dateStr = ParseInstallDate(rawDate);

            programs.Add(new InstalledProgram
            {
                Name = displayName,
                Publisher = sub.GetValue("Publisher")?.ToString() ?? "",
                Version = sub.GetValue("DisplayVersion")?.ToString() ?? "",
                InstallDate = dateStr,
                SizeBytes = sizeKb * 1024,
                UninstallString = sub.GetValue("UninstallString")?.ToString() ?? "",
                RegistryKey = $@"{basePath}\{subName}"
            });
        }
    }

    private static string ParseInstallDate(string raw)
    {
        if (raw.Length == 8 &&
            int.TryParse(raw[..4], out var y) &&
            int.TryParse(raw[4..6], out var m) &&
            int.TryParse(raw[6..8], out var d))
        {
            return $"{y}-{m:D2}-{d:D2}";
        }
        return raw;
    }

    public bool Uninstall(InstalledProgram program)
    {
        if (string.IsNullOrEmpty(program.UninstallString)) return false;
        try
        {
            var cmd = program.UninstallString.Trim();
            string fileName, args;

            if (cmd.StartsWith('"'))
            {
                var endQuote = cmd.IndexOf('"', 1);
                fileName = cmd[1..endQuote];
                args = endQuote + 1 < cmd.Length ? cmd[(endQuote + 1)..].Trim() : "";
            }
            else
            {
                var spaceIdx = cmd.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    fileName = cmd[..spaceIdx];
                    args = cmd[(spaceIdx + 1)..];
                }
                else
                {
                    fileName = cmd;
                    args = "";
                }
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = true
            });
            return true;
        }
        catch { return false; }
    }
}
