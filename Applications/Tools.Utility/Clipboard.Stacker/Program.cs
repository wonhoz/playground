using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ClipboardStacker;

internal static class Program
{
    private const string Aumid = "Playground.ClipboardStacker";

    [DllImport("shell32.dll")]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string AppID);

    /// <summary>
    /// AUMID를 레지스트리에 등록하고 프로세스에 적용한다.
    /// - IconUri를 현재 app.ico 경로로 갱신하여 toast 알림 아이콘을 항상 최신으로 유지
    /// - SetCurrentProcessExplicitAppUserModelID는 UI 생성 전에 반드시 먼저 호출해야 함
    /// </summary>
    private static void RegisterAumid()
    {
        try
        {
            // UI 생성 전 AUMID 설정 (반드시 먼저 호출)
            SetCurrentProcessExplicitAppUserModelID(Aumid);

            // 레지스트리에 AUMID + 아이콘 경로 등록 (toast 알림 아이콘 소스)
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
            using var key = Registry.CurrentUser.CreateSubKey(
                $@"SOFTWARE\Classes\AppUserModelId\{Aumid}");
            key.SetValue("DisplayName", "Clipboard Stacker");
            if (File.Exists(iconPath))
                key.SetValue("IconUri", iconPath);
        }
        catch { /* 실패해도 앱 실행은 계속 */ }
    }

    [STAThread]
    static void Main()
    {
        // toast 알림 아이콘 등록 — UI 생성 전 최우선 실행
        RegisterAumid();
        new App().Run();
    }
}
