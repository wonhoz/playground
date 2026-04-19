using Microsoft.Win32;

namespace StayAwake
{
    /// <summary>
    /// Windows 시작 시 자동 실행 레지스트리 등록/해제
    /// HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run 키를 사용 — 관리자 권한 불필요
    /// </summary>
    public static class AutoStart
    {
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "StayAwake";

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                var value = key?.GetValue(ValueName) as string;
                if (string.IsNullOrEmpty(value)) return false;

                var expected = GetExecutablePath();
                return string.Equals(
                    value.Trim('"'),
                    expected,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        public static bool Enable()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
                key.SetValue(ValueName, $"\"{GetExecutablePath()}\"", RegistryValueKind.String);
                return true;
            }
            catch { return false; }
        }

        public static bool Disable()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                if (key?.GetValue(ValueName) != null)
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }
            catch { return false; }
        }

        private static string GetExecutablePath()
        {
            // 단일 파일 배포 시 Environment.ProcessPath가 정확한 실행 경로 반환
            // (Assembly.Location은 single-file app에서 빈 문자열 — IL3000)
            return Environment.ProcessPath ?? string.Empty;
        }
    }
}
