#Requires -Version 5.0
# +publish-ui.ps1  —  Playground 선택 배포 TUI
# +publish.cmd 에서 powershell -ExecutionPolicy Bypass -File "...+publish-ui.ps1" 로 호출

param([string]$Root = $PSScriptRoot)

$BIN    = Join-Path $Root "bin"
$NOTIFY = Join-Path $Root ".claude\Scripts\notify\notify.ps1"
$ESC    = [char]27

$CY = "$ESC[96m"  # Cyan
$GR = "$ESC[92m"  # Green
$RE = "$ESC[91m"  # Red
$DG = "$ESC[90m"  # Dark Gray
$RS = "$ESC[0m"   # Reset
$BD = "$ESC[1m"   # Bold

# ── 프로젝트 목록 ──────────────────────────────────────────────────────────
$Projects = @(
    @{ Cat="Applications";         Name="AI.Clip";               Src="Applications\AI\AI.Clip";                               Out="Applications\AI"                   }
    @{ Cat="Applications";         Name="Music.Player";           Src="Applications\Audio\Music.Player";                       Out="Applications\Audio"                }
    @{ Cat="Applications";         Name="Stay.Awake";             Src="Applications\Automation\Stay.Awake";                    Out="Applications\Automation\StayAwake" }
    @{ Cat="Applications";         Name="Batch.Rename";           Src="Applications\Files\Batch.Rename";                       Out="Applications\Files"                }
    @{ Cat="Applications";         Name="File.Duplicates";        Src="Applications\Files\File.Duplicates";                    Out="Applications\Files"                }
    @{ Cat="Applications";         Name="Toast.Cast";             Src="Applications\Health\Toast.Cast";                        Out="Applications\Health"               }
    @{ Cat="Applications";         Name="Photo.Video.Organizer";  Src="Applications\Media\Photo.Video.Organizer";              Out="Applications\Media"                }
    @{ Cat="Tools / Dev";          Name="Api.Probe";              Src="Applications\Tools\Dev\Api.Probe";                      Out="Applications\Tools\Dev\Api.Probe"  }
    @{ Cat="Tools / Dev";          Name="Hash.Forge";             Src="Applications\Tools\Dev\Hash.Forge";                     Out="Applications\Tools\Dev"            }
    @{ Cat="Tools / Dev";          Name="Log.Lens";               Src="Applications\Tools\Dev\Log.Lens";                       Out="Applications\Tools\Dev"            }
    @{ Cat="Tools / Dev";          Name="Mock.Desk";              Src="Applications\Tools\Dev\Mock.Desk";                      Out="Applications\Tools\Dev\Mock.Desk"  }
    @{ Cat="Tools / Network";      Name="DNS.Flip";               Src="Applications\Tools\Network\DNS.Flip";                   Out="Applications\Tools\Network"        }
    @{ Cat="Tools / Network";      Name="Port.Watch";             Src="Applications\Tools\Network\Port.Watch";                 Out="Applications\Tools\Network"        }
    @{ Cat="Tools / Productivity"; Name="Clipboard.Stacker";      Src="Applications\Tools\Productivity\Clipboard.Stacker";     Out="Applications\Tools\Productivity"   }
    @{ Cat="Tools / Productivity"; Name="Screen.Recorder";        Src="Applications\Tools\Productivity\Screen.Recorder";       Out="Applications\Tools\Productivity"   }
    @{ Cat="Tools / Productivity"; Name="Text.Forge";             Src="Applications\Tools\Productivity\Text.Forge";            Out="Applications\Tools\Productivity"   }
    @{ Cat="Tools / System";       Name="Env.Guard";              Src="Applications\Tools\System\Env.Guard";                   Out="Applications\Tools\System"         }
    @{ Cat="Games / Action";       Name="Dungeon.Dash";           Src="Games\Action\Dungeon.Dash";                             Out="Games\Action"                      }
    @{ Cat="Games / Arcade";       Name="Brick.Blitz";            Src="Games\Arcade\Brick.Blitz";                              Out="Games\Arcade"                      }
    @{ Cat="Games / Arcade";       Name="Dash.City";              Src="Games\Arcade\Dash.City";                                Out="Games\Arcade"                      }
    @{ Cat="Games / Arcade";       Name="Neon.Run";               Src="Games\Arcade\Neon.Run";                                 Out="Games\Arcade"                      }
    @{ Cat="Games / Puzzle";       Name="Gravity.Flip";           Src="Games\Puzzle\Gravity.Flip";                             Out="Games\Puzzle"                      }
    @{ Cat="Games / Puzzle";       Name="Hue.Flow";               Src="Games\Puzzle\Hue.Flow";                                 Out="Games\Puzzle"                      }
    @{ Cat="Games / Racing";       Name="Nitro.Drift";            Src="Games\Racing\Nitro.Drift";                              Out="Games\Racing"                      }
    @{ Cat="Games / Rhythm";       Name="Beat.Drop";              Src="Games\Rhythm\Beat.Drop";                                Out="Games\Rhythm"                      }
    @{ Cat="Games / Shooter";      Name="Dodge.Blitz";            Src="Games\Shooter\Dodge.Blitz";                             Out="Games\Shooter"                     }
    @{ Cat="Games / Shooter";      Name="Star.Strike";            Src="Games\Shooter\Star.Strike";                             Out="Games\Shooter"                     }
    @{ Cat="Games / Strategy";     Name="Tower.Guard";            Src="Games\Strategy\Tower.Guard";                            Out="Games\Strategy"                    }
)

$n       = $Projects.Count
$checked = [bool[]]::new($n)
$cursor  = 0

# ── 플랫 행 목록 빌드 (카테고리 헤더 + 항목 행) ─────────────────────────────
$rows = [System.Collections.Generic.List[hashtable]]::new()
$prevCat = ""
for ($i = 0; $i -lt $n; $i++) {
    if ($Projects[$i].Cat -ne $prevCat) {
        $rows.Add(@{ Type="cat"; ItemIdx=-1; Text=$Projects[$i].Cat })
        $prevCat = $Projects[$i].Cat
    }
    $rows.Add(@{ Type="item"; ItemIdx=$i })
}

# 항목 인덱스 → 행 인덱스 맵
$itemToRow = @{}
for ($r = 0; $r -lt $rows.Count; $r++) {
    if ($rows[$r].Type -eq "item") { $itemToRow[$rows[$r].ItemIdx] = $r }
}
$R = $rows.Count

# ── 뷰포트 ────────────────────────────────────────────────────────────────
$HEADER_H = 4
$FOOTER_H = 4
$topRow   = 0

function Get-PageSize { [Math]::Max(5, [Console]::WindowHeight - $HEADER_H - $FOOTER_H - 2) }

function Ensure-Visible {
    $curRow  = $itemToRow[$cursor]
    $pg      = Get-PageSize
    if ($curRow -lt $script:topRow) {
        $script:topRow = $curRow
    }
    if ($curRow -ge $script:topRow + $pg) {
        $script:topRow = $curRow - $pg + 1
    }
    # 뷰포트 상단이 카테고리 헤더이면 한 칸 위로 올려 헤더도 표시
    if ($script:topRow -gt 0 -and $rows[$script:topRow].Type -eq "cat") {
        $script:topRow = [Math]::Max(0, $script:topRow - 1)
    }
}

# ── 한 줄 출력 (ANSI 이스케이프 제거 후 폭 보정) ───────────────────────────
function Write-TLine {
    param([string]$line = "")
    $w   = [Console]::WindowWidth
    $vis = [regex]::Replace($line, "`e\[[0-9;]*m", "")
    # Hangul 등 CJK 문자는 터미널 2칸 폭 → 보정
    $wide = ([regex]::Matches($vis, '[\uAC00-\uD7A3\u3000-\u9FFF]')).Count
    $pad  = [Math]::Max(0, $w - $vis.Length - $wide - 1)
    [Console]::Write($line + (' ' * $pad) + "`n")
}

# ── TUI 렌더링 ───────────────────────────────────────────────────────────
function Draw-TUI {
    [Console]::SetCursorPosition(0, 0)
    $pg       = Get-PageSize
    $selCount = 0; foreach ($c in $checked) { if ($c) { $selCount++ } }
    $endRow   = [Math]::Min($topRow + $pg - 1, $R - 1)
    $hasUp    = $topRow -gt 0
    $hasDown  = ($topRow + $pg) -lt $R

    # 헤더
    Write-TLine ""
    Write-TLine "  ${BD}${CY}Playground | Publish${RS}"
    $upMark = if ($hasUp) { "${DG}(▲)${RS} " } else { "     " }
    Write-TLine "  ${upMark}${DG}----------------------------------------------${RS}"
    Write-TLine ""

    # 뷰포트 행 출력
    $drawn = 0
    for ($r = $topRow; $r -le $endRow; $r++) {
        $row = $rows[$r]
        if ($row.Type -eq "cat") {
            Write-TLine "  ${BD}${DG}  $($row.Text)${RS}"
        } else {
            $i   = $row.ItemIdx
            $chk = if ($checked[$i]) { "${GR}■${RS}" } else { "${DG}□${RS}" }
            $cur = if ($i -eq $cursor) { "${CY}▶${RS}" } else { "  " }
            $nm  = if ($i -eq $cursor) { "${BD}${CY}$($Projects[$i].Name)${RS}" } else { $Projects[$i].Name }
            Write-TLine "  $cur [$chk] $nm"
        }
        $drawn++
    }
    # 남은 행 공백으로 지우기
    for ($r = $drawn; $r -lt $pg; $r++) { Write-TLine "" }

    # 푸터
    $dnMark  = if ($hasDown) { "${DG}(▼)${RS} " } else { "     " }
    Write-TLine "  ${dnMark}${DG}----------------------------------------------${RS}"
    $selStr  = if ($selCount -gt 0) { "${GR}${selCount}개 선택${RS}" } else { "${DG}없음${RS}" }
    Write-TLine "  ${DG}선택:${RS} $selStr   ${DG}↑↓ Space  A=전체  Enter=배포  Esc=취소${RS}"
    Write-TLine ""
}

# ── 메인 입력 루프 ───────────────────────────────────────────────────────
[Console]::CursorVisible = $false
[Console]::Clear()

$action  = "cancel"
$running = $true

try {
    while ($running) {
        Ensure-Visible
        Draw-TUI

        $key     = [Console]::ReadKey($true)
        $keyName = $key.Key.ToString()

        switch ($keyName) {
            "UpArrow" {
                if ($cursor -gt 0) { $cursor-- } else { $cursor = $n - 1 }
            }
            "DownArrow" {
                if ($cursor -lt $n - 1) { $cursor++ } else { $cursor = 0 }
            }
            "PageUp" {
                $cursor = [Math]::Max(0, $cursor - (Get-PageSize))
            }
            "PageDown" {
                $cursor = [Math]::Min($n - 1, $cursor + (Get-PageSize))
            }
            "Home" { $cursor = 0 }
            "End"  { $cursor = $n - 1 }
            "Spacebar" {
                $checked[$cursor] = -not $checked[$cursor]
            }
            "A" {
                $allOn = $true
                foreach ($c in $checked) { if (-not $c) { $allOn = $false; break } }
                for ($i = 0; $i -lt $n; $i++) { $checked[$i] = -not $allOn }
            }
            "Enter" {
                $selCount = 0; foreach ($c in $checked) { if ($c) { $selCount++ } }
                if ($selCount -gt 0) { $action = "publish"; $running = $false }
            }
            "Escape" {
                $action  = "cancel"
                $running = $false
            }
        }
    }
} finally {
    [Console]::CursorVisible = $true
}

# ── 취소 ─────────────────────────────────────────────────────────────────
if ($action -eq "cancel") {
    [Console]::Clear()
    Write-Host "`n  ${DG}취소되었습니다.${RS}`n"
    exit 0
}

# ── 배포 실행 ────────────────────────────────────────────────────────────
[Console]::Clear()

if (-not (Test-Path $BIN)) { New-Item -ItemType Directory -Path $BIN | Out-Null }

$total = 0; $pass = 0; $fail = 0; $failed = @()

Write-Host ""
Write-Host "  ${BD}${CY}Playground | Publish (선택)${RS}"
Write-Host "  ${DG}Output: $BIN${RS}"
Write-Host "  ${DG}--------------------------------------------------${RS}"
Write-Host ""

for ($i = 0; $i -lt $n; $i++) {
    if (-not $checked[$i]) { continue }

    $p       = $Projects[$i]
    $total++
    $srcDir  = Join-Path $Root $p.Src
    $outDir  = Join-Path $BIN  $p.Out
    $logFile = Join-Path $env:TEMP "pub_$($p.Name).log"

    if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

    Write-Host "  ${CY}> $($p.Name)${RS}"
    Write-Host "  ${DG}  $($p.Out)${RS}"

    $output = & dotnet publish "$srcDir" -c Release -o "$outDir" 2>&1
    $rc     = $LASTEXITCODE
    $output | Out-File -FilePath $logFile -Encoding utf8

    if ($rc -eq 0) {
        $pass++
        Write-Host "  ${GR}  [OK]${RS}"
    } else {
        $fail++
        $failed += $p.Name
        Write-Host "  ${RE}  [!!]  Failed - log: $logFile${RS}"
    }
    Write-Host ""
}

# .pdb 제거
Write-Host "  ${DG}Cleaning .pdb files...${RS}"
Get-ChildItem -Path $BIN -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "  ${DG}--------------------------------------------------${RS}"
Write-Host ""

if ($fail -gt 0) {
    Write-Host "  ${BD}${RE}Result: $pass/$total OK  |  Failed: $($failed -join ', ')${RS}"
} else {
    Write-Host "  ${BD}${GR}Result: $pass/$total All succeeded${RS}"
}
Write-Host ""

# ── 알림 ─────────────────────────────────────────────────────────────────
if (Test-Path $NOTIFY) {
    if ($fail -eq 0) {
        & powershell -ExecutionPolicy Bypass -File "$NOTIFY" `
            -Message "Publish 성공: $pass/$total 프로젝트 배포 완료" `
            -Level Info -Title "Playground Publish"
    } else {
        & powershell -ExecutionPolicy Bypass -File "$NOTIFY" `
            -Message "Publish 실패: $pass/$total 성공, 실패: $($failed -join ', ')" `
            -Level Error -Title "Playground Publish"
    }
}

Read-Host "  계속하려면 Enter 키를 누르세요"
