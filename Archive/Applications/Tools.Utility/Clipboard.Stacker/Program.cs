using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ClipboardStacker;

internal static class Program
{
    private const string Aumid = "Playground.ClipboardStacker";

    [DllImport("shell32.dll")]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string AppID);

    // EmbeddedResource로 내장된 원본 ICO를 LocalAppData에 복사 → toast 알림 IconUri용 실제 파일 생성
    // Icon.ExtractAssociatedIcon() 대신 사용 — 멀티해상도 원본 품질 유지
    private static string? ExtractIconToAppData()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Playground", "ClipboardStacker");
            Directory.CreateDirectory(dir);
            var icoPath = Path.Combine(dir, "app.ico");
            using var src = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico");
            if (src != null)
            {
                using var fs = File.Create(icoPath);
                src.CopyTo(fs);
                return icoPath;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// AUMID를 레지스트리에 등록하고 프로세스에 적용한다.
    /// - exe에서 아이콘을 추출해 LocalAppData에 저장 후 IconUri로 등록
    /// - SetCurrentProcessExplicitAppUserModelID는 UI 생성 전에 반드시 먼저 호출해야 함
    /// </summary>
    private static void RegisterAumid()
    {
        try
        {
            // UI 생성 전 AUMID 설정 (반드시 먼저 호출)
            SetCurrentProcessExplicitAppUserModelID(Aumid);

            using var key = Registry.CurrentUser.CreateSubKey(
                $@"SOFTWARE\Classes\AppUserModelId\{Aumid}");
            key.SetValue("DisplayName", "Clipboard Stacker");
            var iconPath = ExtractIconToAppData();
            if (iconPath != null)
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
