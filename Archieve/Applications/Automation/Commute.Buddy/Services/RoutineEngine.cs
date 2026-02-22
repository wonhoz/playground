using CommuteBuddy.Models;

namespace CommuteBuddy.Services;

public class RoutineEngine
{
    private AppSettings _settings;

    public RoutineEngine(AppSettings settings) => _settings = settings;

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    public void Execute(Routine routine)
    {
        if (routine.StartStayAwake) StartStayAwake();
        if (routine.StopStayAwake)  StopStayAwake();
        foreach (var path in routine.AppsToLaunch) LaunchApp(path);
        foreach (var name in routine.AppsToClose)  CloseApp(name);
    }

    private void StartStayAwake()
    {
        if (Process.GetProcessesByName("StayAwake").Length > 0) return;
        var path = _settings.StayAwakePath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static void StopStayAwake()
    {
        foreach (var p in Process.GetProcessesByName("StayAwake"))
            try { p.Kill(); } catch { }
    }

    private static void LaunchApp(string path)
    {
        if (!File.Exists(path)) return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
    }

    private static void CloseApp(string processName)
    {
        // .exe 확장자 제거 후 프로세스 이름으로 검색
        var name = Path.GetFileNameWithoutExtension(processName);
        foreach (var p in Process.GetProcessesByName(name))
            try { p.Kill(); } catch { }
    }
}
