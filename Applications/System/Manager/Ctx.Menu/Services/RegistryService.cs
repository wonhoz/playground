namespace CtxMenu.Services;

public static class RegistryService
{
    // (레지스트리 경로, 대상 타입, 범위)
    private static readonly (string Path, TargetType Type, RegistryScope Scope)[] _roots =
    [
        (@"SOFTWARE\Classes\*\shell",                    TargetType.AllFiles,   RegistryScope.System),
        (@"SOFTWARE\Classes\Directory\shell",            TargetType.Folder,     RegistryScope.System),
        (@"SOFTWARE\Classes\Directory\Background\shell", TargetType.Background, RegistryScope.System),
        (@"SOFTWARE\Classes\Drive\shell",                TargetType.Drive,      RegistryScope.System),
        (@"Software\Classes\*\shell",                    TargetType.AllFiles,   RegistryScope.User),
        (@"Software\Classes\Directory\shell",            TargetType.Folder,     RegistryScope.User),
        (@"Software\Classes\Directory\Background\shell", TargetType.Background, RegistryScope.User),
        (@"Software\Classes\Drive\shell",                TargetType.Drive,      RegistryScope.User),
    ];

    // ── 전체 로드 ─────────────────────────────────────────────────────
    public static List<ShellEntry> LoadAll(IProgress<string>? progress = null)
    {
        var entries = new List<ShellEntry>();
        int loaded = 0;

        foreach (var (path, type, scope) in _roots)
        {
            var hive = scope == RegistryScope.System ? Registry.LocalMachine : Registry.CurrentUser;
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key == null) continue;

                foreach (var subName in key.GetSubKeyNames())
                {
                    var entry = ReadEntry(key, subName, path, type, scope);
                    if (entry == null) continue;
                    entries.Add(entry);
                    progress?.Report($"{++loaded}개 로드됨");
                }
            }
            catch { /* 접근 거부 키 무시 */ }
        }

        // 확장자별 항목 추가
        entries.AddRange(LoadExtensionEntries(ref loaded, progress));

        return entries;
    }

    // ── 확장자별 .ext\shell 항목 ──────────────────────────────────────
    private static List<ShellEntry> LoadExtensionEntries(ref int loaded, IProgress<string>? progress)
    {
        var entries = new List<ShellEntry>();

        foreach (var (hive, scope, basePrefix) in new[]
        {
            (Registry.LocalMachine, RegistryScope.System, @"SOFTWARE\Classes"),
            (Registry.CurrentUser,  RegistryScope.User,   @"Software\Classes"),
        })
        {
            try
            {
                using var classesKey = hive.OpenSubKey(basePrefix);
                if (classesKey == null) continue;

                foreach (var name in classesKey.GetSubKeyNames().Where(n => n.StartsWith('.')))
                {
                    try
                    {
                        var shellPath = $@"{basePrefix}\{name}\shell";
                        using var shellKey = hive.OpenSubKey(shellPath);
                        if (shellKey == null) continue;

                        foreach (var subName in shellKey.GetSubKeyNames())
                        {
                            var entry = ReadEntry(shellKey, subName, shellPath, TargetType.Extension, scope);
                            if (entry == null) continue;
                            entry.ExtFilter = name;
                            entries.Add(entry);
                            progress?.Report($"{++loaded}개 로드됨");
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        return entries;
    }

    // ── 단일 항목 읽기 ────────────────────────────────────────────────
    private static ShellEntry? ReadEntry(RegistryKey parentKey, string subName,
        string basePath, TargetType type, RegistryScope scope)
    {
        try
        {
            using var key = parentKey.OpenSubKey(subName);
            if (key == null) return null;

            var displayName = key.GetValue("MUIVerb") as string
                           ?? key.GetValue(null) as string
                           ?? subName;

            var command = "";
            using var cmdKey = key.OpenSubKey("command");
            if (cmdKey != null)
                command = cmdKey.GetValue(null) as string ?? "";

            var icon      = key.GetValue("Icon") as string ?? "";
            var isEnabled = key.GetValue("LegacyDisable") == null;
            var hive      = scope == RegistryScope.System ? "HKLM" : "HKCU";

            return new ShellEntry
            {
                KeyName      = subName,
                DisplayName  = displayName == subName ? "" : displayName,
                Command      = command,
                IconPath     = icon,
                TargetType   = type,
                Scope        = scope,
                IsEnabled    = isEnabled,
                RegistryPath = $@"{hive}\{basePath}\{subName}",
            };
        }
        catch { return null; }
    }

    // ── 활성화/비활성화 ───────────────────────────────────────────────
    public static void SetEnabled(ShellEntry entry, bool enabled)
    {
        var hive = entry.Scope == RegistryScope.System ? Registry.LocalMachine : Registry.CurrentUser;
        var path = StripHive(entry.RegistryPath);

        using var key = hive.OpenSubKey(path, writable: true);
        if (key == null) return;

        if (enabled)
            key.DeleteValue("LegacyDisable", throwOnMissingValue: false);
        else
            key.SetValue("LegacyDisable", "", RegistryValueKind.String);
    }

    // ── 항목 추가/편집 ────────────────────────────────────────────────
    public static void SaveEntry(ShellEntry entry, string? oldKeyName = null)
    {
        var hive       = entry.Scope == RegistryScope.System ? Registry.LocalMachine : Registry.CurrentUser;
        var parentPath = GetParentPath(entry);

        // 키 이름 변경 시 구키 삭제
        if (oldKeyName != null && oldKeyName != entry.KeyName)
        {
            using var parentKey = hive.OpenSubKey(parentPath, writable: true);
            parentKey?.DeleteSubKeyTree(oldKeyName, throwOnMissingSubKey: false);
        }

        using var parent = hive.OpenSubKey(parentPath, writable: true)
                        ?? hive.CreateSubKey(parentPath);
        using var entryKey = parent.CreateSubKey(entry.KeyName);

        entryKey.SetValue(null, string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.KeyName : entry.DisplayName);

        if (!string.IsNullOrWhiteSpace(entry.IconPath))
            entryKey.SetValue("Icon", entry.IconPath);
        else
            entryKey.DeleteValue("Icon", throwOnMissingValue: false);

        if (!string.IsNullOrWhiteSpace(entry.Command))
        {
            using var cmdKey = entryKey.CreateSubKey("command");
            cmdKey.SetValue(null, entry.Command);
        }
        else
        {
            entryKey.DeleteSubKeyTree("command", throwOnMissingSubKey: false);
        }

        var hiveStr = entry.Scope == RegistryScope.System ? "HKLM" : "HKCU";
        entry.RegistryPath = $@"{hiveStr}\{parentPath}\{entry.KeyName}";
    }

    // ── 항목 삭제 ─────────────────────────────────────────────────────
    public static void DeleteEntry(ShellEntry entry)
    {
        var hive       = entry.Scope == RegistryScope.System ? Registry.LocalMachine : Registry.CurrentUser;
        var path       = StripHive(entry.RegistryPath);
        var parentPath = path[..path.LastIndexOf('\\')];
        var keyName    = path[(path.LastIndexOf('\\') + 1)..];

        using var parentKey = hive.OpenSubKey(parentPath, writable: true);
        parentKey?.DeleteSubKeyTree(keyName, throwOnMissingSubKey: false);
    }

    // ── 백업/복원 ─────────────────────────────────────────────────────
    public static string ExportJson(IEnumerable<ShellEntry> entries) =>
        JsonSerializer.Serialize(entries.ToList(), new JsonSerializerOptions { WriteIndented = true });

    public static List<ShellEntry>? ImportJson(string json) =>
        JsonSerializer.Deserialize<List<ShellEntry>>(json);

    // ── 유틸 ─────────────────────────────────────────────────────────
    private static string StripHive(string path)
    {
        var idx = path.IndexOf('\\');
        return idx >= 0 ? path[(idx + 1)..] : path;
    }

    private static string GetParentPath(ShellEntry entry)
    {
        var prefix = entry.Scope == RegistryScope.System ? "SOFTWARE" : "Software";
        return entry.TargetType switch
        {
            TargetType.AllFiles   => $@"{prefix}\Classes\*\shell",
            TargetType.Folder     => $@"{prefix}\Classes\Directory\shell",
            TargetType.Background => $@"{prefix}\Classes\Directory\Background\shell",
            TargetType.Drive      => $@"{prefix}\Classes\Drive\shell",
            TargetType.Extension  => $@"{prefix}\Classes\{entry.ExtFilter}\shell",
            _                     => throw new ArgumentException("알 수 없는 대상 타입"),
        };
    }
}
