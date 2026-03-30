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
    /// - IconUri를 exe 경로로 등록 (ApplicationIcon으로 내장된 아이콘을 Windows가 추출)
    /// - SetCurrentProcessExplicitAppUserModelID는 UI 생성 전에 반드시 먼저 호출해야 함
    /// </summary>
    private static void RegisterAumid()
    {
        try
        {
            // UI 생성 전 AUMID 설정 (반드시 먼저 호출)
            SetCurrentProcessExplicitAppUserModelID(Aumid);

            // 레지스트리에 AUMID + 아이콘 경로 등록 (toast 알림 아이콘 소스)
            // exe 자체에 ApplicationIcon으로 내장되어 있으므로 exe 경로를 IconUri로 사용
            using var key = Registry.CurrentUser.CreateSubKey(
                $@"SOFTWARE\Classes\AppUserModelId\{Aumid}");
            key.SetValue("DisplayName", "Clipboard Stacker");
            key.SetValue("IconUri", Environment.ProcessPath!);
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
