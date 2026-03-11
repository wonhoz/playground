namespace PathGuard.Services;

public static class SnapshotService
{
    private static readonly List<PathSnapshot> _snapshots = [];

    public static IReadOnlyList<PathSnapshot> Snapshots => _snapshots;

    /// <summary>현재 PATH 상태를 스냅샷으로 저장합니다.</summary>
    public static PathSnapshot Save(string label = "")
    {
        var sysPath  = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
        var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User)    ?? "";
        var snap = new PathSnapshot(DateTime.Now, sysPath, userPath,
                                    string.IsNullOrWhiteSpace(label) ? $"스냅샷 {_snapshots.Count + 1}" : label);
        _snapshots.Insert(0, snap);
        return snap;
    }

    /// <summary>스냅샷을 복원합니다.</summary>
    public static void Restore(PathSnapshot snap)
    {
        Environment.SetEnvironmentVariable("PATH", snap.SystemPath, EnvironmentVariableTarget.Machine);
        Environment.SetEnvironmentVariable("PATH", snap.UserPath,   EnvironmentVariableTarget.User);
    }

    public static void Remove(PathSnapshot snap) => _snapshots.Remove(snap);
}
