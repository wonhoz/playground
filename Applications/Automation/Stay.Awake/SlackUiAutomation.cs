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
        /// <summary>마지막 활성 상태 변경 시각 (재시작 시 AppSettings에서 복원)</summary>
        public DateTime LastActiveSet { get; set; } = DateTime.MinValue;
        /// <summary>마지막 자리비움 상태 변경 시각 (재시작 시 AppSettings에서 복원)</summary>
        public DateTime LastAwaySet { get; set; } = DateTime.MinValue;

        public bool IsEnabled { get; set; }
        public int WorkStartHour { get; set; } = 8;
        public int WorkStartMinute { get; set; } = 55;
        public int WorkEndHour { get; set; } = 18;
        public int WorkEndMinute { get; set; } = 55;

        /// <summary>
        /// 매 분 호출: 출근/퇴근 시간이면 Slack 상태 자동 변경
        /// - 정각 분 매치뿐 아니라 '그 시각 이후 당일 처음 실행되는 경우'도 보정
        ///   (PC 꺼짐·앱 미실행 등으로 정확한 분을 놓쳐도 당일 자동 전환 보장)
        /// </summary>
        public async Task<SlackUiResult?> CheckAndSetPresenceAsync()
        {
            if (!IsEnabled) return null;

            var now = DateTime.Now;
            var startMinutes = WorkStartHour * 60 + WorkStartMinute;
            var endMinutes = WorkEndHour * 60 + WorkEndMinute;
            var nowMinutes = now.Hour * 60 + now.Minute;

            // 퇴근 시각을 이미 지난 경우 — 자리비움 먼저 처리 (퇴근 후 재가동 시 활성 재전송 방지)
            if (nowMinutes >= endMinutes && LastAwaySet.Date != now.Date)
            {
                var result = await SetPresenceAsync("away");
                if (result.Success) LastAwaySet = now;
                return result;
            }

            // 출근 시각~퇴근 시각 사이 — 활성 (정각 분 놓쳐도 당일 내 첫 체크에서 자동 보정)
            if (nowMinutes >= startMinutes && nowMinutes < endMinutes && LastActiveSet.Date != now.Date)
            {
                var result = await SetPresenceAsync("active");
                if (result.Success) LastActiveSet = now;
                return result;
            }

            return null;
        }

        public Task<SlackUiResult> SetActiveAsync() => SetPresenceAsync("active");
        public Task<SlackUiResult> SetAwayAsync() => SetPresenceAsync("away");

        /// <summary>방해 금지(DND) 설정. minutes=0이면 해제.</summary>
        public Task<SlackUiResult> SetDndAsync(int minutes)
        {
            var slashCommand = minutes == 0 ? "/dnd off" : $"/dnd {minutes}";
            var statusKey    = minutes == 0 ? "dnd-end" : $"dnd-{minutes}";
            return SendCommandAsync(slashCommand, statusKey);
        }

        /// <summary>
        /// PowerShell을 통해 Slack 앱에 슬래시 커맨드 전송
        /// </summary>
        public async Task<SlackUiResult> SetPresenceAsync(string status)
        {
            var slashCommand = status == "away" ? "/away" : "/active";
            return await SendCommandAsync(slashCommand, status);
        }

        private async Task<SlackUiResult> SendCommandAsync(string slashCommand, string statusKey)
        {
            try
            {
                var output = await RunPowerShellScriptAsync(slashCommand);

                if (output.Contains("SLACK_NOT_RUNNING"))
                    return new SlackUiResult(statusKey, false, "Slack이 실행 중이 아닙니다.");

                if (output.Contains("SUCCESS"))
                    return new SlackUiResult(statusKey, true, null);

                return new SlackUiResult(statusKey, false, $"알 수 없는 오류: {output.Trim()}");
            }
            catch (Exception ex)
            {
                return new SlackUiResult(statusKey, false, ex.Message);
            }
        }

        private static async Task<string> RunPowerShellScriptAsync(string slashCommand)
        {
            // PowerShell 스크립트: {{ }} 는 C# 보간 이스케이프, PowerShell 중괄호 표현
            // -Sta 모드 필수 — Clipboard.GetDataObject()/SetDataObject()는 STA 스레드에서만 동작
            var script = $@"
$ErrorActionPreference = 'Stop'

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

$prev = [SA.Win32]::GetForegroundWindow()
[SA.Win32]::ShowWindow($slack.MainWindowHandle, 9) | Out-Null
[SA.Win32]::SetForegroundWindow($slack.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 600

Add-Type -AssemblyName System.Windows.Forms

[System.Windows.Forms.SendKeys]::SendWait('{{ESC}}')
Start-Sleep -Milliseconds 200

# 클립보드 모든 포맷 백업 (텍스트뿐 아니라 이미지·파일·HTML 등 IDataObject 전체)
$prevDataObject = [System.Windows.Forms.Clipboard]::GetDataObject()
$savedData = @{{}}
if ($prevDataObject) {{
    foreach ($fmt in $prevDataObject.GetFormats($false)) {{
        try {{
            $value = $prevDataObject.GetData($fmt, $false)
            if ($null -ne $value) {{ $savedData[$fmt] = $value }}
        }} catch {{}}
    }}
}}

[System.Windows.Forms.Clipboard]::SetText('{slashCommand}')
Start-Sleep -Milliseconds 100
[System.Windows.Forms.SendKeys]::SendWait('^v')
Start-Sleep -Milliseconds 200
[System.Windows.Forms.SendKeys]::SendWait('{{ENTER}}')

# 클립보드 원복 — 백업한 모든 포맷을 새 DataObject에 set 후 SetDataObject(copy=true)
if ($savedData.Count -gt 0) {{
    $newDataObject = New-Object System.Windows.Forms.DataObject
    foreach ($fmt in $savedData.Keys) {{
        try {{ $newDataObject.SetData($fmt, $savedData[$fmt]) }} catch {{}}
    }}
    try {{ [System.Windows.Forms.Clipboard]::SetDataObject($newDataObject, $true) }} catch {{ [System.Windows.Forms.Clipboard]::Clear() }}
}} else {{
    [System.Windows.Forms.Clipboard]::Clear()
}}
Start-Sleep -Milliseconds 400

[SA.Win32]::SetForegroundWindow($prev) | Out-Null

Write-Output 'SUCCESS'
";

            var tempFile = Path.Combine(Path.GetTempPath(), $"StayAwake_slack_{Guid.NewGuid():N}.ps1");
            await File.WriteAllTextAsync(tempFile, script, System.Text.Encoding.UTF8);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    // -Sta 필수: Clipboard API는 STA 스레드에서만 동작 (PowerShell 5.1 기본은 MTA)
                    Arguments = $"-ExecutionPolicy Bypass -NonInteractive -Sta -WindowStyle Hidden -File \"{tempFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi) ?? throw new InvalidOperationException("PowerShell 프로세스를 시작할 수 없습니다.");
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                return output;
            }
            finally
            {
                try { File.Delete(tempFile); } catch (Exception ex) { Logger.LogException("SlackUiAutomation.DeleteTemp", ex); }
            }
        }
    }

    public record SlackUiResult(string Status, bool Success, string? ErrorMessage);
}
