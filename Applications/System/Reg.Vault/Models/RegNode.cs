using System.Collections.ObjectModel;
using Microsoft.Win32;

namespace RegVault.Models;

public class RegNode
{
    public string Name        { get; }
    public string FullPath    { get; }
    public RegistryHive Hive  { get; }
    public bool IsRoot        { get; }
    public bool HasChildren   { get; private set; } = true;
    public bool IsLoaded      { get; private set; } = false;

    public ObservableCollection<RegNode> Children { get; } = new();

    // TreeView에서 노드 확장 여부 감지용 더미 자식
    private static readonly RegNode DummyChild = new("...", "", RegistryHive.LocalMachine, false);

    public RegNode(string name, string fullPath, RegistryHive hive, bool isRoot = false)
    {
        Name     = name;
        FullPath = fullPath;
        Hive     = hive;
        IsRoot   = isRoot;

        if (!isRoot)
            Children.Add(DummyChild); // lazy load placeholder
    }

    public void LoadChildren()
    {
        if (IsLoaded) return;
        IsLoaded = true;

        Children.Clear();
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(Hive, RegistryView.Default);
            using var key     = string.IsNullOrEmpty(FullPath)
                ? baseKey
                : baseKey.OpenSubKey(FullPath);

            if (key == null) { HasChildren = false; return; }

            var subKeys = key.GetSubKeyNames();
            foreach (var sub in subKeys.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                var childPath = string.IsNullOrEmpty(FullPath) ? sub : $"{FullPath}\\{sub}";
                Children.Add(new RegNode(sub, childPath, Hive));
            }
            HasChildren = Children.Count > 0;
        }
        catch
        {
            HasChildren = false;
        }
    }

    // 루트 5개 하이브 생성 헬퍼
    public static IEnumerable<RegNode> CreateHiveRoots() =>
    [
        new("HKEY_LOCAL_MACHINE",  "", RegistryHive.LocalMachine,  true),
        new("HKEY_CURRENT_USER",   "", RegistryHive.CurrentUser,   true),
        new("HKEY_CLASSES_ROOT",   "", RegistryHive.ClassesRoot,   true),
        new("HKEY_USERS",          "", RegistryHive.Users,         true),
        new("HKEY_CURRENT_CONFIG", "", RegistryHive.CurrentConfig, true),
    ];
}
