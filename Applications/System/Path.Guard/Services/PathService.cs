namespace PathGuard.Services;

public static class PathService
{
    // ── 읽기 ──────────────────────────────────────────────────────────
    public static List<PathEntry> Load()
    {
        var result = new List<PathEntry>();

        var sysPaths  = GetRaw(EnvironmentVariableTarget.Machine);
        var userPaths = GetRaw(EnvironmentVariableTarget.User);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (raw, scope) in sysPaths.Select(p => (p, PathScope.System))
                                   .Concat(userPaths.Select(p => (p, PathScope.User))))
        {
            var expanded = Environment.ExpandEnvironmentVariables(raw);
            var isDisabled = raw.StartsWith('#');
            var cleanRaw   = isDisabled ? raw[1..] : raw;
            var cleanExp   = isDisabled ? Environment.ExpandEnvironmentVariables(cleanRaw) : expanded;

            var status = PathStatus.Ok;
            if (isDisabled)
                status = PathStatus.Disabled;
            else if (!Directory.Exists(expanded))
                status = PathStatus.Broken;
            else if (!seen.Add(expanded.TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant()))
                status = PathStatus.Duplicate;

            result.Add(new PathEntry
            {
                RawValue      = cleanRaw,
                ExpandedValue = cleanExp,
                Scope         = scope,
                Status        = status,
                IsEnabled     = !isDisabled
            });
        }
        return result;
    }

    private static List<string> GetRaw(EnvironmentVariableTarget target)
    {
        var raw = Environment.GetEnvironmentVariable("PATH", target) ?? "";
        return [.. raw.Split(';', StringSplitOptions.RemoveEmptyEntries)];
    }

    // ── 쓰기 ──────────────────────────────────────────────────────────
    public static void Save(IEnumerable<PathEntry> entries)
    {
        var sysEntries  = entries.Where(e => e.Scope == PathScope.System);
        var userEntries = entries.Where(e => e.Scope == PathScope.User);

        SetPath(EnvironmentVariableTarget.Machine, sysEntries);
        SetPath(EnvironmentVariableTarget.User,    userEntries);

        BroadcastSettingChange();
    }

    private static void SetPath(EnvironmentVariableTarget target, IEnumerable<PathEntry> entries)
    {
        var parts = entries.Select(e => e.IsEnabled ? e.RawValue : "#" + e.RawValue);
        Environment.SetEnvironmentVariable("PATH", string.Join(";", parts), target);
    }

    // ── 진단 재계산 ────────────────────────────────────────────────────
    public static void Diagnose(IList<PathEntry> entries)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            if (!e.IsEnabled) { e.Status = PathStatus.Disabled; continue; }
            var exp = Environment.ExpandEnvironmentVariables(e.RawValue)
                                  .TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(exp))
            { e.Status = PathStatus.Broken; continue; }
            if (!seen.Add(exp.ToUpperInvariant()))
            { e.Status = PathStatus.Duplicate; continue; }
            e.Status = PathStatus.Ok;
        }
    }

    // ── 실행파일 검색 ──────────────────────────────────────────────────
    public static void SearchExecutable(IList<PathEntry> entries, string fileName)
    {
        foreach (var e in entries)
        {
            e.HitFiles.Clear();
            if (!e.IsEnabled) continue;
            var dir = Environment.ExpandEnvironmentVariables(e.RawValue);
            if (!Directory.Exists(dir)) continue;
            try
            {
                var hits = Directory.GetFiles(dir, fileName, SearchOption.TopDirectoryOnly);
                e.HitFiles.AddRange(hits.Select(Path.GetFileName)!);
            }
            catch { /* 접근 불가 디렉토리 */ }
        }
    }

    // ── 환경변수 변경 브로드캐스트 ───────────────────────────────────
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam,
        string lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    private static void BroadcastSettingChange()
    {
        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero,
            "Environment", SMTO_ABORTIFHUNG, 5000, out _);
    }
}
