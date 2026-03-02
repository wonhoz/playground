namespace FileUnlocker.Services;

/// <summary>Windows Restart Manager API를 이용해 파일을 잠근 프로세스를 조회합니다.</summary>
public static class RestartManagerService
{
    [DllImport("Rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("Rstrtmgr.dll")]
    private static extern int RmEndSession(uint dwSessionHandle);

    [DllImport("Rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint dwSessionHandle,
        uint nFiles, string[]? rgsFilenames,
        uint nApplications, RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices, string[]? rgsServiceNames);

    [DllImport("Rstrtmgr.dll")]
    private static extern int RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[]? rgAffectedApps,
        ref uint lpdwRebootReasons);

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    private enum RM_APP_TYPE
    {
        RmUnknownApp = 0,
        RmMainWindow = 1,
        RmOtherWindow = 2,
        RmService = 3,
        RmExplorer = 4,
        RmConsole = 5,
        RmCritical = 1000
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strAppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string strServiceShortName;

        public RM_APP_TYPE ApplicationType;
        public uint AppStatus;
        public int TSSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    /// <summary>지정된 경로들을 잠근 프로세스 목록을 반환합니다.</summary>
    public static List<LockingProcess> GetLockingProcesses(IEnumerable<string> paths)
    {
        var result = new List<LockingProcess>();
        var pathList = paths.ToArray();
        if (pathList.Length == 0) return result;

        var sessionKey = Guid.NewGuid().ToString("N")[..20];
        uint handle = 0;

        try
        {
            if (RmStartSession(out handle, 0, sessionKey) != 0) return result;
            if (RmRegisterResources(handle, (uint)pathList.Length, pathList, 0, null, 0, null) != 0) return result;

            uint pnProcInfoNeeded = 0, pnProcInfo = 0, rebootReasons = 0;
            RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, null, ref rebootReasons);

            if (pnProcInfoNeeded == 0) return result;

            var processInfoArr = new RM_PROCESS_INFO[pnProcInfoNeeded];
            pnProcInfo = pnProcInfoNeeded;

            if (RmGetList(handle, out _, ref pnProcInfo, processInfoArr, ref rebootReasons) != 0) return result;

            // 중복 PID 방지
            var seenPids = new HashSet<int>();

            for (int i = 0; i < (int)pnProcInfo; i++)
            {
                var pi = processInfoArr[i];
                int pid = pi.Process.dwProcessId;
                if (!seenPids.Add(pid)) continue;

                string execPath = "";
                string displayName = pi.strAppName ?? "";

                try
                {
                    var proc = Process.GetProcessById(pid);
                    try { execPath = proc.MainModule?.FileName ?? ""; } catch { }
                    if (string.IsNullOrEmpty(displayName))
                        displayName = proc.ProcessName;
                }
                catch { }

                // 아이콘 추출 — 백그라운드 스레드이므로 반드시 Freeze 후 반환
                ImageSource? icon = null;
                if (!string.IsNullOrEmpty(execPath))
                {
                    try
                    {
                        using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(execPath);
                        if (sysIcon != null)
                        {
                            var bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                                sysIcon.Handle, Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            bmp.Freeze(); // UI 스레드에서 안전하게 사용하려면 필수
                            icon = bmp;
                        }
                    }
                    catch { }
                }

                result.Add(new LockingProcess
                {
                    Pid = pid,
                    Name = string.IsNullOrEmpty(displayName) ? $"PID {pid}" : displayName,
                    ExecutablePath = execPath,
                    AppType = pi.ApplicationType switch
                    {
                        RM_APP_TYPE.RmMainWindow  => "앱",
                        RM_APP_TYPE.RmOtherWindow => "앱",
                        RM_APP_TYPE.RmService     => "서비스",
                        RM_APP_TYPE.RmExplorer    => "탐색기",
                        RM_APP_TYPE.RmConsole     => "콘솔",
                        RM_APP_TYPE.RmCritical    => "시스템",
                        _                         => "알 수 없음"
                    },
                    IsRestartable = pi.bRestartable,
                    Icon = icon
                });
            }
        }
        finally
        {
            if (handle != 0) RmEndSession(handle);
        }

        return result;
    }

    /// <summary>폴더 경로 하위의 모든 파일 경로를 수집합니다 (재귀).</summary>
    public static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                yield return path;
            }
            else if (Directory.Exists(path))
            {
                // 폴더 자체도 등록 (탐색기가 폴더를 잠글 수 있음)
                yield return path;
                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    yield return f;
            }
        }
    }
}
