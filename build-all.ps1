#Requires -Version 5.1

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$SolutionFile = Join-Path $PSScriptRoot 'Playground.sln'
$Configs      = @('Debug', 'Release')

function Write-Header([string]$text) {
    Write-Host ''
    Write-Host ('-' * 60) -ForegroundColor DarkGray
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host ('-' * 60) -ForegroundColor DarkGray
}

function Write-Ok([string]$text)   { Write-Host "  [OK]  $text" -ForegroundColor Green }
function Write-Fail([string]$text) { Write-Host "  [!!]  $text" -ForegroundColor Red   }
function Write-Info([string]$text) { Write-Host "   -   $text"  -ForegroundColor Gray  }

$results   = [System.Collections.Generic.List[hashtable]]::new()
$anyFailed = $false
$totalSw   = [System.Diagnostics.Stopwatch]::StartNew()

foreach ($cfg in $Configs) {
    Write-Header "Building  [$cfg]"

    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    $output   = & dotnet build $SolutionFile -c $cfg --nologo 2>&1
    $exitCode = $LASTEXITCODE
    $sw.Stop()

    $elapsed = '{0:F1}s' -f $sw.Elapsed.TotalSeconds

    $builtLines   = @($output | Where-Object { $_ -match ' -> ' })
    $projectNames = $builtLines | ForEach-Object {
        $left = ($_ -split ' -> ')[0].Trim()
        ($left -split '[\\\/]')[-1] -replace '\.(cs|vb|fs)proj$', ''
    }

    $errorLines = @($output | Where-Object { $_ -match '^\s*Build FAILED' -or $_ -match ': error ' })
    $warnCount  = @($output | Where-Object { $_ -match ': warning ' }).Count

    if ($exitCode -eq 0) {
        $warnSuffix = if ($warnCount -gt 0) { ", $warnCount warning(s)" } else { '' }
        Write-Ok "[$cfg]  Build succeeded  ($elapsed$warnSuffix)"
        foreach ($p in $projectNames) { Write-Info $p }
    }
    else {
        Write-Fail "[$cfg]  Build FAILED  ($elapsed)"
        foreach ($e in ($errorLines | Select-Object -First 5)) {
            Write-Host "         $e" -ForegroundColor DarkRed
        }
        $anyFailed = $true
    }

    $results.Add(@{
        Config   = $cfg
        Success  = ($exitCode -eq 0)
        Elapsed  = $elapsed
        Projects = $projectNames
        Warnings = $warnCount
        Errors   = $errorLines
    })
}

$totalSw.Stop()

Write-Header 'Summary'

foreach ($r in $results) {
    $mark  = if ($r.Success) { '[OK]' } else { '[!!]' }
    $color = if ($r.Success) { 'Green' } else { 'Red' }
    $warn  = if ($r.Warnings -gt 0) { "  ($($r.Warnings) warning(s))" } else { '' }
    Write-Host ("  {0}  {1,-10}  {2}{3}" -f $mark, $r.Config, $r.Elapsed, $warn) -ForegroundColor $color
}

$totalElapsed = '{0:F1}s' -f $totalSw.Elapsed.TotalSeconds
Write-Host ''
Write-Host "  Total elapsed: $totalElapsed" -ForegroundColor DarkGray
Write-Host ('-' * 60) -ForegroundColor DarkGray
Write-Host ''

if ($anyFailed) {
    Write-Host '  Build failed. Check errors above.' -ForegroundColor Red
    Write-Host ''
    exit 1
}
else {
    Write-Host '  All configurations built successfully!' -ForegroundColor Green
    Write-Host ''
    exit 0
}
