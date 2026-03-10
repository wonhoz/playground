namespace AppTemp.Services;

/// <summary>레지스트리 스냅샷 비교기</summary>
public class RegistryTracker
{
    private static readonly (RegistryHive Hive, string SubKey)[] SnapshotAreas =
    [
        (RegistryHive.CurrentUser,  @"Software"),
        (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHive.CurrentUser,  @"Software\Microsoft\Windows\CurrentVersion\Run"),
    ];

    private const int MaxDepth = 4; // HKCU\Software 재귀 최대 깊이

    // ── 스냅샷 ─────────────────────────────────────────────────────
    public RegistrySnapshot TakeSnapshot()
    {
        var snap = new RegistrySnapshot();
        foreach (var (hive, sub) in SnapshotAreas)
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key  = root.OpenSubKey(sub);
                if (key != null)
                    SnapshotKey(snap, key, $@"{HiveName(hive)}\{sub}", 0);
            }
            catch { }
        }
        return snap;
    }

    private static void SnapshotKey(RegistrySnapshot snap, RegistryKey key, string path, int depth)
    {
        // 값 수집
        var values = new Dictionary<string, string>();
        foreach (var name in key.GetValueNames())
        {
            try { values[name] = key.GetValue(name)?.ToString() ?? ""; }
            catch { }
        }
        snap.Keys[path] = values;

        if (depth >= MaxDepth) return;

        foreach (var subName in key.GetSubKeyNames())
        {
            try
            {
                using var sub = key.OpenSubKey(subName);
                if (sub != null)
                    SnapshotKey(snap, sub, $@"{path}\{subName}", depth + 1);
            }
            catch { }
        }
    }

    // ── Diff ──────────────────────────────────────────────────────
    public IReadOnlyList<ChangeRecord> Diff(RegistrySnapshot before, RegistrySnapshot after)
    {
        var changes = new List<ChangeRecord>();
        var now = DateTime.Now;

        // 새로 생긴 키 / 값
        foreach (var (path, afterVals) in after.Keys)
        {
            if (!before.Keys.TryGetValue(path, out var beforeVals))
            {
                // 새 키 전체
                changes.Add(new ChangeRecord
                {
                    Timestamp = now, Type = ChangeType.Created,
                    Category = ChangeCategory.Registry, Path = path
                });
                continue;
            }

            // 값 변경 비교
            foreach (var (name, afterVal) in afterVals)
            {
                if (!beforeVals.TryGetValue(name, out var beforeVal))
                {
                    changes.Add(new ChangeRecord
                    {
                        Timestamp = now, Type = ChangeType.Created,
                        Category = ChangeCategory.Registry, Path = path,
                        ValueName = name, NewValue = afterVal
                    });
                }
                else if (beforeVal != afterVal)
                {
                    changes.Add(new ChangeRecord
                    {
                        Timestamp = now, Type = ChangeType.Modified,
                        Category = ChangeCategory.Registry, Path = path,
                        ValueName = name, OldValue = beforeVal, NewValue = afterVal
                    });
                }
            }

            // 삭제된 값
            foreach (var name in beforeVals.Keys.Where(n => !afterVals.ContainsKey(n)))
            {
                changes.Add(new ChangeRecord
                {
                    Timestamp = now, Type = ChangeType.Deleted,
                    Category = ChangeCategory.Registry, Path = path,
                    ValueName = name, OldValue = beforeVals[name]
                });
            }
        }

        // 삭제된 키
        foreach (var path in before.Keys.Keys.Where(p => !after.Keys.ContainsKey(p)))
        {
            changes.Add(new ChangeRecord
            {
                Timestamp = now, Type = ChangeType.Deleted,
                Category = ChangeCategory.Registry, Path = path
            });
        }

        return changes;
    }

    // ── 롤백 (생성된 키/값 삭제, 수정된 값 복원) ──────────────────
    public void Rollback(IReadOnlyList<ChangeRecord> changes)
    {
        foreach (var change in changes.Where(c => c.Category == ChangeCategory.Registry))
        {
            try
            {
                if (change.Type == ChangeType.Created)
                {
                    if (change.ValueName == null)
                        DeleteRegistryKey(change.Path);     // 전체 키 삭제
                    else
                        DeleteRegistryValue(change.Path, change.ValueName);
                }
                else if (change.Type == ChangeType.Modified && change.ValueName != null)
                {
                    SetRegistryValue(change.Path, change.ValueName, change.OldValue ?? "");
                }
                else if (change.Type == ChangeType.Deleted && change.ValueName != null)
                {
                    SetRegistryValue(change.Path, change.ValueName, change.OldValue ?? "");
                }
            }
            catch { }
        }
    }

    private static void DeleteRegistryKey(string fullPath)
    {
        var (hive, sub) = SplitPath(fullPath);
        using var key = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        try { key.DeleteSubKeyTree(sub, false); } catch { }
    }

    private static void DeleteRegistryValue(string fullPath, string valueName)
    {
        var (hive, sub) = SplitPath(fullPath);
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key     = baseKey.OpenSubKey(sub, writable: true);
        key?.DeleteValue(valueName, false);
    }

    private static void SetRegistryValue(string fullPath, string valueName, string value)
    {
        var (hive, sub) = SplitPath(fullPath);
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key     = baseKey.OpenSubKey(sub, writable: true);
        key?.SetValue(valueName, value);
    }

    private static (RegistryHive Hive, string Sub) SplitPath(string fullPath)
    {
        var idx = fullPath.IndexOf('\\');
        if (idx < 0) throw new ArgumentException("Invalid registry path");
        var hiveStr = fullPath[..idx];
        var sub     = fullPath[(idx + 1)..];
        var hive    = hiveStr switch
        {
            "HKEY_CURRENT_USER"   or "HKCU" => RegistryHive.CurrentUser,
            "HKEY_LOCAL_MACHINE"  or "HKLM" => RegistryHive.LocalMachine,
            _ => RegistryHive.CurrentUser
        };
        return (hive, sub);
    }

    private static string HiveName(RegistryHive h) => h switch
    {
        RegistryHive.CurrentUser  => "HKCU",
        RegistryHive.LocalMachine => "HKLM",
        _ => "HKCR"
    };
}
