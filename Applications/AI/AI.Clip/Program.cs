using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AiClip
{
    internal static class Program
    {
        private const string Aumid = "Playground.AiClip";

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
                // exe 자체에 ApplicationIcon으로 내장되어 있으므로 exe 경로를 IconUri로 사용
                using var key = Registry.CurrentUser.CreateSubKey(
                    $@"SOFTWARE\Classes\AppUserModelId\{Aumid}");
                key.SetValue("DisplayName", "AI.Clip");
                key.SetValue("IconUri", Environment.ProcessPath!);
            }
            catch { /* 실패해도 앱 실행은 계속 */ }
        }

        [STAThread]
        static void Main()
        {
            // toast 알림 아이콘 등록 — UI 생성 전 최우선 실행
            RegisterAumid();

            using var mutex = new Mutex(true, "AiClip_SingleInstance", out bool isNew);
            if (!isNew)
            {
                MessageBox.Show("AI.Clip이 이미 실행 중입니다.", "AI.Clip",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
    }
}
