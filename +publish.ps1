#Requires -Version 5.1
param([switch]$All)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$ROOT = Split-Path -Parent $MyInvocation.MyCommand.Definition
$BIN  = Join-Path $ROOT "bin"

# ── 프로젝트 목록 ────────────────────────────────────────────────────────────
$AllProjects = @(
    # Applications / AI
    [PSCustomObject]@{ Category="Applications / AI";                   Name="AI.Clip";               Src="Applications\AI\AI.Clip";                                 Out="Applications\AI" },
    # Applications / Audio
    [PSCustomObject]@{ Category="Applications / Audio";                Name="Music.Player";          Src="Applications\Audio\Music.Player";                         Out="Applications\Audio" },
    # Applications / Automation
    [PSCustomObject]@{ Category="Applications / Automation";           Name="Stay.Awake";            Src="Applications\Automation\Stay.Awake";                      Out="Applications\Automation\StayAwake" },
    # Applications / Files
    [PSCustomObject]@{ Category="Applications / Files";                Name="Batch.Rename";          Src="Applications\Files\Batch.Rename";                         Out="Applications\Files" },
    [PSCustomObject]@{ Category="Applications / Files";                Name="File.Duplicates";       Src="Applications\Files\File.Duplicates";                      Out="Applications\Files" },
    # Applications / Health
    [PSCustomObject]@{ Category="Applications / Health";               Name="Toast.Cast";            Src="Applications\Health\Toast.Cast";                          Out="Applications\Health" },
    # Applications / Media
    [PSCustomObject]@{ Category="Applications / Media";                Name="Photo.Video.Organizer"; Src="Applications\Media\Photo.Video.Organizer";                Out="Applications\Media" },
    # Applications / Tools / Dev
    [PSCustomObject]@{ Category="Applications / Tools / Dev";          Name="Api.Probe";             Src="Applications\Tools\Dev\Api.Probe";                        Out="Applications\Tools\Dev\Api.Probe" },
    [PSCustomObject]@{ Category="Applications / Tools / Dev";          Name="Hash.Forge";            Src="Applications\Tools\Dev\Hash.Forge";                       Out="Applications\Tools\Dev" },
    [PSCustomObject]@{ Category="Applications / Tools / Dev";          Name="Log.Lens";              Src="Applications\Tools\Dev\Log.Lens";                         Out="Applications\Tools\Dev" },
    [PSCustomObject]@{ Category="Applications / Tools / Dev";          Name="Mock.Desk";             Src="Applications\Tools\Dev\Mock.Desk";                        Out="Applications\Tools\Dev\Mock.Desk" },
    # Applications / Tools / Network
    [PSCustomObject]@{ Category="Applications / Tools / Network";      Name="DNS.Flip";              Src="Applications\Tools\Network\DNS.Flip";                     Out="Applications\Tools\Network" },
    [PSCustomObject]@{ Category="Applications / Tools / Network";      Name="Port.Watch";            Src="Applications\Tools\Network\Port.Watch";                   Out="Applications\Tools\Network" },
    # Applications / Tools / Productivity
    [PSCustomObject]@{ Category="Applications / Tools / Productivity"; Name="Clipboard.Stacker";    Src="Applications\Tools\Productivity\Clipboard.Stacker";       Out="Applications\Tools\Productivity" },
    [PSCustomObject]@{ Category="Applications / Tools / Productivity"; Name="Screen.Recorder";      Src="Applications\Tools\Productivity\Screen.Recorder";         Out="Applications\Tools\Productivity" },
    [PSCustomObject]@{ Category="Applications / Tools / Productivity"; Name="Text.Forge";           Src="Applications\Tools\Productivity\Text.Forge";              Out="Applications\Tools\Productivity" },
    # Applications / Tools / System
    [PSCustomObject]@{ Category="Applications / Tools / System";       Name="Env.Guard";             Src="Applications\Tools\System\Env.Guard";                     Out="Applications\Tools\System" },
    # Games / Action
    [PSCustomObject]@{ Category="Games / Action";                      Name="Dungeon.Dash";          Src="Games\Action\Dungeon.Dash";                               Out="Games\Action" },
    # Games / Arcade
    [PSCustomObject]@{ Category="Games / Arcade";                      Name="Brick.Blitz";           Src="Games\Arcade\Brick.Blitz";                                Out="Games\Arcade" },
    [PSCustomObject]@{ Category="Games / Arcade";                      Name="Dash.City";             Src="Games\Arcade\Dash.City";                                  Out="Games\Arcade" },
    [PSCustomObject]@{ Category="Games / Arcade";                      Name="Neon.Run";              Src="Games\Arcade\Neon.Run";                                   Out="Games\Arcade" },
    # Games / Puzzle
    [PSCustomObject]@{ Category="Games / Puzzle";                      Name="Gravity.Flip";          Src="Games\Puzzle\Gravity.Flip";                               Out="Games\Puzzle" },
    [PSCustomObject]@{ Category="Games / Puzzle";                      Name="Hue.Flow";              Src="Games\Puzzle\Hue.Flow";                                   Out="Games\Puzzle" },
    # Games / Racing
    [PSCustomObject]@{ Category="Games / Racing";                      Name="Nitro.Drift";           Src="Games\Racing\Nitro.Drift";                                Out="Games\Racing" },
    # Games / Rhythm
    [PSCustomObject]@{ Category="Games / Rhythm";                      Name="Beat.Drop";             Src="Games\Rhythm\Beat.Drop";                                  Out="Games\Rhythm" },
    # Games / Shooter
    [PSCustomObject]@{ Category="Games / Shooter";                     Name="Dodge.Blitz";           Src="Games\Shooter\Dodge.Blitz";                               Out="Games\Shooter" },
    [PSCustomObject]@{ Category="Games / Shooter";                     Name="Star.Strike";           Src="Games\Shooter\Star.Strike";                               Out="Games\Shooter" },
    # Games / Strategy
    [PSCustomObject]@{ Category="Games / Strategy";                    Name="Tower.Guard";           Src="Games\Strategy\Tower.Guard";                              Out="Games\Strategy" }
)

# ── 프로젝트 선택 ────────────────────────────────────────────────────────────
if ($All) {
    $selected = $AllProjects
    Write-Host ""
    Write-Host "Playground | Publish-All" -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "Playground | Publish — 프로젝트 선택 창을 확인하세요..." -ForegroundColor Cyan
    $selected = $AllProjects | Out-GridView `
        -Title "Playground Publish  |  배포할 프로젝트 선택  (Ctrl+클릭: 다중 선택  /  OK: 배포 시작)" `
        -PassThru
    if (-not $selected -or $selected.Count -eq 0) {
        Write-Host "취소되었습니다." -ForegroundColor Yellow
        exit 0
    }
    Write-Host ""
    Write-Host "Playground | Publish ($($selected.Count)개 선택됨)" -ForegroundColor Cyan
}

Write-Host "출력: $BIN" -ForegroundColor DarkGray
Write-Host "--------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

if (-not (Test-Path $BIN)) { New-Item -ItemType Directory -Path $BIN -Force | Out-Null }

# ── 배포 실행 ────────────────────────────────────────────────────────────────
$total = 0; $pass = 0; $fail = 0; $failed = @()

foreach ($p in $selected) {
    $total++
    $srcDir  = Join-Path $ROOT $p.Src
    $outDir  = Join-Path $BIN  $p.Out
    $logFile = Join-Path $env:TEMP "pub_$($p.Name).log"

    if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

    Write-Host "  > $($p.Name)" -ForegroundColor Cyan
    Write-Host "    $($p.Out)" -ForegroundColor DarkGray

    dotnet publish $srcDir -c Release -o $outDir > $logFile 2>&1

    if ($LASTEXITCODE -eq 0) {
        $pass++
        Write-Host "    [OK]" -ForegroundColor Green
    } else {
        $fail++
        $failed += $p.Name
        Write-Host "    [!!] 실패 — 로그: $logFile" -ForegroundColor Red
    }
    Write-Host ""
}

# ── .pdb 정리 ────────────────────────────────────────────────────────────────
Write-Host ".pdb 정리 중..." -ForegroundColor DarkGray
Get-ChildItem -Path $BIN -Recurse -Filter "*.pdb" -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "--------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""
if ($fail -gt 0) {
    Write-Host "결과: $pass / $total 성공  |  실패: $($failed -join ', ')" -ForegroundColor Red
} else {
    Write-Host "결과: $pass / $total 전체 성공" -ForegroundColor Green
}
Write-Host ""

# ── 알림 전송 ────────────────────────────────────────────────────────────────
$notifyScript = Join-Path $ROOT ".claude\Scripts\notify\notify.ps1"
if (Test-Path $notifyScript) {
    if ($fail -eq 0) {
        & $notifyScript -Message "Publish 성공: $pass/$total 프로젝트 배포 완료" -Level Info  -Title "Playground Publish"
    } else {
        & $notifyScript -Message "Publish 실패: $pass/$total 성공, 실패: $($failed -join ', ')" -Level Error -Title "Playground Publish"
    }
}

Read-Host "완료. 엔터를 누르면 종료합니다"
