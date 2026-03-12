using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using RegVault.Models;

namespace RegVault.Services;

public record SearchResult(string KeyPath, string? ValueName, string? DataDisplay, string HiveName);

public static class RegistryService
{
    // ── 값 읽기 ────────────────────────────────────────────────────────
    public static List<RegValue> GetValues(RegistryHive hive, string keyPath)
    {
        var result = new List<RegValue>();
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key     = string.IsNullOrEmpty(keyPath)
                ? baseKey
                : baseKey.OpenSubKey(keyPath);
            if (key == null) return result;

            // 기본값 먼저
            var defaultVal = key.GetValue("", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            var defaultKind = key.GetValueKind("");
            result.Add(new RegValue("", defaultKind, defaultVal));

            foreach (var name in key.GetValueNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var val  = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    var kind = key.GetValueKind(name);
                    result.Add(new RegValue(name, kind, val));
                }
                catch { /* 권한 없는 값 스킵 */ }
            }
        }
        catch { }
        return result;
    }

    // ── 정규식 검색 ────────────────────────────────────────────────────
    public static async Task<List<SearchResult>> SearchAsync(
        RegistryHive hive,
        string rootPath,
        Regex pattern,
        bool searchKeys,
        bool searchValueNames,
        bool searchValueData,
        int maxDepth,
        CancellationToken ct,
        IProgress<string>? progress = null)
    {
        var results  = new List<SearchResult>();
        var hiveName = HiveDisplayName(hive);

        await Task.Run(() =>
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var root    = string.IsNullOrEmpty(rootPath) ? baseKey : baseKey.OpenSubKey(rootPath);
                if (root == null) return;

                SearchRecursive(root, rootPath, hive, hiveName,
                    pattern, searchKeys, searchValueNames, searchValueData,
                    0, maxDepth, results, ct, progress);
            }
            catch { }
        }, ct);

        return results;
    }

    private static void SearchRecursive(
        RegistryKey key,
        string keyPath,
        RegistryHive hive,
        string hiveName,
        Regex pattern,
        bool searchKeys,
        bool searchValueNames,
        bool searchValueData,
        int depth,
        int maxDepth,
        List<SearchResult> results,
        CancellationToken ct,
        IProgress<string>? progress)
    {
        if (ct.IsCancellationRequested) return;
        if (depth > maxDepth) return;

        progress?.Report(keyPath);

        // 키 이름 검색
        if (searchKeys && pattern.IsMatch(key.Name.Split('\\').Last()))
            results.Add(new SearchResult(keyPath, null, null, hiveName));

        // 값 검색
        if (searchValueNames || searchValueData)
        {
            try
            {
                foreach (var name in key.GetValueNames())
                {
                    if (ct.IsCancellationRequested) return;
                    bool nameMatch = searchValueNames && pattern.IsMatch(name);
                    string? dataDisplay = null;

                    if (searchValueData)
                    {
                        try
                        {
                            var val  = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                            var kind = key.GetValueKind(name);
                            var rv   = new RegValue(name, kind, val);
                            if (pattern.IsMatch(rv.DataDisplay)) dataDisplay = rv.DataDisplay;
                        }
                        catch { }
                    }

                    if (nameMatch || dataDisplay != null)
                        results.Add(new SearchResult(keyPath, name.Length == 0 ? "(기본값)" : name, dataDisplay, hiveName));
                }
            }
            catch { }
        }

        // 하위 키 재귀
        try
        {
            foreach (var subName in key.GetSubKeyNames())
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    using var sub     = key.OpenSubKey(subName);
                    if (sub == null) continue;
                    var subPath = string.IsNullOrEmpty(keyPath) ? subName : $"{keyPath}\\{subName}";
                    SearchRecursive(sub, subPath, hive, hiveName,
                        pattern, searchKeys, searchValueNames, searchValueData,
                        depth + 1, maxDepth, results, ct, progress);
                }
                catch { }
            }
        }
        catch { }
    }

    // ── REG 내보내기 ───────────────────────────────────────────────────
    public static void ExportToReg(RegistryHive hive, string keyPath, string filePath)
    {
        var sb  = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();

        var hiveName = HiveDisplayName(hive);
        var fullRoot = $"{hiveName}\\{keyPath}";

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key     = string.IsNullOrEmpty(keyPath) ? baseKey : baseKey.OpenSubKey(keyPath);
            if (key != null)
                ExportKeyToReg(key, fullRoot, sb);
        }
        catch { }

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
    }

    private static void ExportKeyToReg(RegistryKey key, string fullPath, StringBuilder sb)
    {
        sb.AppendLine($"[{fullPath}]");

        try
        {
            // 기본값
            var defaultVal = key.GetValue("", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (defaultVal != null)
            {
                var kind = key.GetValueKind("");
                sb.AppendLine($"@={FormatRegValue(kind, defaultVal)}");
            }

            foreach (var name in key.GetValueNames())
            {
                try
                {
                    var val  = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    var kind = key.GetValueKind(name);
                    sb.AppendLine($"\"{EscapeRegString(name)}\"={FormatRegValue(kind, val)}");
                }
                catch { }
            }
        }
        catch { }

        sb.AppendLine();

        try
        {
            foreach (var sub in key.GetSubKeyNames())
            {
                try
                {
                    using var subKey  = key.OpenSubKey(sub);
                    if (subKey == null) continue;
                    ExportKeyToReg(subKey, $"{fullPath}\\{sub}", sb);
                }
                catch { }
            }
        }
        catch { }
    }

    private static string FormatRegValue(RegistryValueKind kind, object? val) => kind switch
    {
        RegistryValueKind.String       => $"\"{EscapeRegString(val?.ToString() ?? "")}\"",
        RegistryValueKind.ExpandString => $"hex(2):{BytesToHex(Encoding.Unicode.GetBytes((val?.ToString() ?? "") + "\0"))}",
        RegistryValueKind.MultiString  when val is string[] arr =>
            $"hex(7):{BytesToHex(Encoding.Unicode.GetBytes(string.Join("\0", arr) + "\0\0"))}",
        RegistryValueKind.DWord        when val is int i   => $"dword:{(uint)i:X8}",
        RegistryValueKind.QWord        when val is long l  => $"hex(b):{BytesToHex(BitConverter.GetBytes(l))}",
        RegistryValueKind.Binary       when val is byte[] b => $"hex:{BytesToHex(b)}",
        _                              => "\"\""
    };

    private static string BytesToHex(byte[] bytes) =>
        string.Join(",", bytes.Select(b => b.ToString("x2")));

    private static string EscapeRegString(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ── JSON 내보내기 ──────────────────────────────────────────────────
    public static void ExportToJson(RegistryHive hive, string keyPath, string filePath)
    {
        var root = BuildJsonNode(hive, keyPath);
        var json = System.Text.Json.JsonSerializer.Serialize(root,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json, new UTF8Encoding(true));
    }

    private static Dictionary<string, object?> BuildJsonNode(RegistryHive hive, string keyPath)
    {
        var node = new Dictionary<string, object?>();
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key     = string.IsNullOrEmpty(keyPath) ? baseKey : baseKey.OpenSubKey(keyPath);
            if (key == null) return node;

            var values = new Dictionary<string, string?>();
            foreach (var name in key.GetValueNames())
            {
                try
                {
                    var val  = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    var kind = key.GetValueKind(name);
                    values[name.Length == 0 ? "(기본값)" : name] = new RegValue(name, kind, val).DataDisplay;
                }
                catch { }
            }
            if (values.Count > 0) node["_values"] = values;

            foreach (var sub in key.GetSubKeyNames())
            {
                var subPath = string.IsNullOrEmpty(keyPath) ? sub : $"{keyPath}\\{sub}";
                node[sub]   = BuildJsonNode(hive, subPath);
            }
        }
        catch { }
        return node;
    }

    // ── 스냅샷 ────────────────────────────────────────────────────────
    public static RegSnapshot TakeSnapshot(RegistryHive hive, string rootPath, string label)
    {
        var snap = new RegSnapshot
        {
            Label    = label,
            TakenAt  = DateTime.Now,
            RootPath = $"{HiveDisplayName(hive)}\\{rootPath}"
        };

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var root    = string.IsNullOrEmpty(rootPath) ? baseKey : baseKey.OpenSubKey(rootPath);
            if (root != null)
                SnapshotRecursive(root, rootPath, snap.Data);
        }
        catch { }

        return snap;
    }

    private static void SnapshotRecursive(RegistryKey key, string keyPath, Dictionary<string, List<RegValueEntry>> data)
    {
        var entries = new List<RegValueEntry>();
        try
        {
            foreach (var name in key.GetValueNames())
            {
                try
                {
                    var val  = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    var kind = key.GetValueKind(name);
                    var rv   = new RegValue(name, kind, val);
                    entries.Add(new RegValueEntry(name, kind, rv.DataDisplay));
                }
                catch { }
            }
        }
        catch { }

        data[keyPath] = entries;

        try
        {
            foreach (var sub in key.GetSubKeyNames())
            {
                try
                {
                    using var subKey  = key.OpenSubKey(sub);
                    if (subKey == null) continue;
                    var subPath = string.IsNullOrEmpty(keyPath) ? sub : $"{keyPath}\\{sub}";
                    SnapshotRecursive(subKey, subPath, data);
                }
                catch { }
            }
        }
        catch { }
    }

    public static List<DiffEntry> CompareSnapshots(RegSnapshot older, RegSnapshot newer)
    {
        var diffs = new List<DiffEntry>();

        // 삭제된 키
        foreach (var key in older.Data.Keys.Except(newer.Data.Keys))
            diffs.Add(new DiffEntry { Diff = DiffType.Removed, KeyPath = key, ValueName = null });

        // 추가된 키
        foreach (var key in newer.Data.Keys.Except(older.Data.Keys))
            diffs.Add(new DiffEntry { Diff = DiffType.Added, KeyPath = key, ValueName = null });

        // 공통 키 내 값 비교
        foreach (var key in older.Data.Keys.Intersect(newer.Data.Keys))
        {
            var oldVals = older.Data[key].ToDictionary(v => v.Name, v => v.DataDisplay);
            var newVals = newer.Data[key].ToDictionary(v => v.Name, v => v.DataDisplay);

            foreach (var name in oldVals.Keys.Except(newVals.Keys))
                diffs.Add(new DiffEntry { Diff = DiffType.Removed, KeyPath = key,
                    ValueName = name.Length == 0 ? "(기본값)" : name, OldData = oldVals[name] });

            foreach (var name in newVals.Keys.Except(oldVals.Keys))
                diffs.Add(new DiffEntry { Diff = DiffType.Added, KeyPath = key,
                    ValueName = name.Length == 0 ? "(기본값)" : name, NewData = newVals[name] });

            foreach (var name in oldVals.Keys.Intersect(newVals.Keys))
            {
                if (oldVals[name] != newVals[name])
                    diffs.Add(new DiffEntry { Diff = DiffType.Modified, KeyPath = key,
                        ValueName = name.Length == 0 ? "(기본값)" : name,
                        OldData = oldVals[name], NewData = newVals[name] });
            }
        }

        return diffs.OrderBy(d => d.KeyPath).ThenBy(d => d.ValueName).ToList();
    }

    // ── .reg 백업 ─────────────────────────────────────────────────────
    public static string BackupKeyToReg(RegistryHive hive, string keyPath)
    {
        var dir  = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RegVault", "backups");
        Directory.CreateDirectory(dir);
        var safe = keyPath.Replace('\\', '_').Replace('/', '_');
        var file = Path.Combine(dir, $"{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.reg");
        ExportToReg(hive, keyPath, file);
        return file;
    }

    // ── 하이브 이름 ───────────────────────────────────────────────────
    public static string HiveDisplayName(RegistryHive hive) => hive switch
    {
        RegistryHive.LocalMachine  => "HKEY_LOCAL_MACHINE",
        RegistryHive.CurrentUser   => "HKEY_CURRENT_USER",
        RegistryHive.ClassesRoot   => "HKEY_CLASSES_ROOT",
        RegistryHive.Users         => "HKEY_USERS",
        RegistryHive.CurrentConfig => "HKEY_CURRENT_CONFIG",
        _                          => hive.ToString()
    };

    public static RegistryHive ParseHive(string fullPath)
    {
        var prefix = fullPath.Split('\\')[0].ToUpperInvariant();
        return prefix switch
        {
            "HKEY_LOCAL_MACHINE"  or "HKLM" => RegistryHive.LocalMachine,
            "HKEY_CURRENT_USER"   or "HKCU" => RegistryHive.CurrentUser,
            "HKEY_CLASSES_ROOT"   or "HKCR" => RegistryHive.ClassesRoot,
            "HKEY_USERS"          or "HKU"  => RegistryHive.Users,
            "HKEY_CURRENT_CONFIG" or "HKCC" => RegistryHive.CurrentConfig,
            _ => RegistryHive.CurrentUser
        };
    }

    public static string StripHive(string fullPath)
    {
        var idx = fullPath.IndexOf('\\');
        return idx < 0 ? "" : fullPath[(idx + 1)..];
    }
}
