using System.Diagnostics;

namespace StayAwake
{
    /// <summary>
    /// PowerShell + SendKeys 방식으로 Slack 앱의 슬래시 커맨드를 자동 실행
    /// - Slack API/토큰 불필요
    /// - Slack 데스크탑 앱이 실행 중이어야 함
    /// - /active, /away 슬래시 커맨드로 presence 변경
    /// </summary>
    public class SlackUiAutomation
    {
        private DateTime _lastActiveSet = DateTime.MinValue;
        private DateTime _lastAwaySet = DateTime.MinValue;

        public bool IsEnabled { get; set; }
        public int WorkStartHour { get; set; } = 8;
        public int WorkStartMinute { get; set; } = 55;
        public int WorkEndHour { get; set; } = 18;
        public int WorkEndMinute { get; set; } = 55;

        /// <summary>
        /// 매 분 호출: 출근/퇴근 시간이면 Slack 상태 자동 변경
        /// </summary>
        public async Task<SlackUiResult?> CheckAndSetPresenceAsync()
        {
            if (!IsEnabled) return null;

            var now = DateTime.Now;

            // 아침 WorkStartHour:WorkStartMinute → 활성
            if (now.Hour == WorkStartHour && now.Minute == WorkStartMinute && _lastActiveSet.Date != now.Date)
            {
                var result = await SetPresenceAsync("active");
                if (result.Success) _lastActiveSet = now;
                return result;
            }

            // 저녁 WorkEndHour:WorkEndMinute → 자리 비움
            if (now.Hour == WorkEndHour && now.Minute == WorkEndMinute && _lastAwaySet.Date != now.Date)
            {
                var result = await SetPresenceAsync("away");
                if (result.Success) _lastAwaySet = now;
                return result;
            }

            return null;
        }

        public Task<SlackUiResult> SetActiveAsync() => SetPresenceAsync("active");
        public Task<SlackUiResult> SetAwayAsync() => SetPresenceAsync("away");

        /// <summary>
        /// PowerShell을 통해 Slack 앱에 슬래시 커맨드 전송
        /// </summary>
        public async Task<SlackUiResult> SetPresenceAsync(string status)
        {
            // SendKeys: {ENTER}처럼 특수키는 중괄호 표기
            // /away, /active 는 일반 문자열이므로 그대로 사용 가능
            var slashCommand = status == "away" ? "/away" : "/active";

            try
            {
                var output = await RunPowerShellScriptAsync(slashCommand);

                if (output.Contains("SLACK_NOT_RUNNING"))
                    return new SlackUiResult(status, false, "Slack이 실행 중이 아닙니다.");

                if (output.Contains("SUCCESS"))
                    return new SlackUiResult(status, true, null);

                return new SlackUiResult(status, false, $"알 수 없는 오류: {output.Trim()}");
            }
            catch (Exception ex)
            {
                return new SlackUiResult(status, false, ex.Message);
            }
        }

        private static async Task<string> RunPowerShellScriptAsync(string slashCommand)
        {
            // PowerShell 스크립트: {{ }} 는 C# 보간 이스케이프, PowerShell 중괄호 표현
            var script = $@"
$ErrorActionPreference = 'Stop'

# Slack 앱 찾기 (메인 윈도우가 있는 프로세스만)
$slack = Get-Process slack -ErrorAction SilentlyContinue |
    Where-Object {{ $_.MainWindowHandle -ne [IntPtr]::Zero }} |
    Select-Object -First 1

if (-not $slack) {{
    Write-Output 'SLACK_NOT_RUNNING'
    exit 1
}}

Add-Type -Namespace 'SA' -Name 'Win32' -MemberDefinition @'
    [DllImport(""user32.dll"")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport(""user32.dll"")] public static extern bool ShowWindow(IntPtr h, int n);
    [DllImport(""user32.dll"")] public static extern IntPtr GetForegroundWindow();
'@

# 현재 포커스 창 저장 후 Slack 활성화
$prev = [SA.Win32]::GetForegroundWindow()
[SA.Win32]::ShowWindow($slack.MainWindowHandle, 9) | Out-Null
[SA.Win32]::SetForegroundWindow($slack.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 600

Add-Type -AssemblyName System.Windows.Forms

# ESC로 현재 열린 메뉴/팝업 닫기
[System.Windows.Forms.SendKeys]::SendWait('{{ESC}}')
Start-Sleep -Milliseconds 200

# 클립보드로 붙여넣기 (SendKeys는 한글 IME 영향을 받으므로 클립보드 방식 사용)
$prevClipboard = Get-Clipboard -Raw
Set-Clipboard -Value '{slashCommand}'
Start-Sleep -Milliseconds 100
[System.Windows.Forms.SendKeys]::SendWait('^v')
Start-Sleep -Milliseconds 200
[System.Windows.Forms.SendKeys]::SendWait('{{ENTER}}')

# 클립보드 원래 내용 복원
if ($prevClipboard) {{ Set-Clipboard -Value $prevClipboard }} else {{ [System.Windows.Forms.Clipboard]::Clear() }}
Start-Sleep -Milliseconds 400

# 이전 창으로 포커스 복귀
[SA.Win32]::SetForegroundWindow($prev) | Out-Null

Write-Output 'SUCCESS'
";

            var tempFile = Path.Combine(Path.GetTempPath(), "StayAwake_slack.ps1");
            await File.WriteAllTextAsync(tempFile, script, System.Text.Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -NonInteractive -WindowStyle Hidden -File \"{tempFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi)!;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }
    }

    public record SlackUiResult(string Status, bool Success, string? ErrorMessage);
}
