using PadForge.Models;

namespace PadForge.Services;

/// <summary>
/// 포그라운드 프로세스 감지 → 앱별 프로파일 자동 전환
/// </summary>
public class ProcessWatchService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private readonly ProfileService _profileService;
    private CancellationTokenSource? _cts;
    private string _lastProcess = "";

    public ProcessWatchService(ProfileService profileService)
    {
        _profileService = profileService;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                CheckForeground();
                await Task.Delay(1000, _cts.Token).ConfigureAwait(false);
            }
        }, _cts.Token);
    }

    public void Stop() => _cts?.Cancel();

    private void CheckForeground()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            GetWindowThreadProcessId(hwnd, out uint pid);
            var proc = System.Diagnostics.Process.GetProcessById((int)pid);
            var name = proc.ProcessName.ToLowerInvariant();

            if (name == _lastProcess) return;
            _lastProcess = name;

            // 활성 프로파일에서 앱 매핑 탐색
            foreach (var profile in _profileService.Profiles)
            {
                var match = profile.AppProfiles.FirstOrDefault(a =>
                    a.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    a.ProcessName.Equals(name + ".exe", StringComparison.OrdinalIgnoreCase));

                if (match is not null)
                {
                    // 매칭된 프로파일로 전환
                    var target = _profileService.Profiles
                        .FirstOrDefault(p => p.Id == match.ProfileId);
                    if (target is not null)
                    {
                        _profileService.Activate(target);
                        return;
                    }
                }
            }
        }
        catch { /* 프로세스 접근 실패 무시 */ }
    }
}
