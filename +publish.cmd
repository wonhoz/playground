@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "BIN=%ROOT%bin"

:: ANSI color vars - must be set before any call (prevents [90m misparse in subroutine)
for /f %%a in ('echo prompt $E ^| cmd /q') do set "ESC=%%a"
set "CY=!ESC![96m"
set "GR=!ESC![92m"
set "RE=!ESC![91m"
set "DG=!ESC![90m"
set "RS=!ESC![0m"
set "BD=!ESC![1m"

if not exist "%BIN%" mkdir "%BIN%"

:: ── 메뉴 출력 ─────────────────────────────────────────────────────
:MENU
cls
echo.
echo !BD!!CY!Playground ^| Publish!RS!
echo !DG!--------------------------------------------------!RS!
echo.
echo !CY!   1!RS! AI.Clip               !DG!Applications/AI!RS!
echo !CY!   2!RS! Api.Probe             !DG!Applications/Network/Server!RS!
echo !CY!   3!RS! App.Temp              !DG!Applications/Development/Inspector!RS!
echo !CY!   4!RS! Auto.Build            !DG!Games/Puzzle!RS!
echo !CY!   5!RS! Batch.Rename          !DG!Applications/Files/Manager!RS!
echo !CY!   6!RS! Beat.Drop             !DG!Games/Rhythm!RS!
echo !CY!   7!RS! Boot.Map              !DG!Applications/System/Monitor!RS!
echo !CY!   8!RS! Brick.Blitz           !DG!Games/Arcade!RS!
echo !CY!   9!RS! Burn.Rate             !DG!Applications/System/Monitor!RS!
echo !CY!  10!RS! Char.Art              !DG!Applications/Text!RS!
echo !CY!  11!RS! Chord.Strike          !DG!Games/Rhythm!RS!
echo !CY!  12!RS! Clipboard.Stacker     !DG!Applications/Tools.Utility!RS!
echo !CY!  13!RS! Code.Idle             !DG!Games/Idle!RS!
echo !CY!  14!RS! Color.Grade           !DG!Applications/Photo.Picture!RS!
echo !CY!  15!RS! Crash.View            !DG!Applications/Development/Analyzer!RS!
echo !CY!  16!RS! Ctx.Menu              !DG!Applications/System/Manager!RS!
echo !CY!  17!RS! Dash.City             !DG!Games/Arcade!RS!
echo !CY!  18!RS! Dict.Cast             !DG!Applications/Tools.Utility!RS!
echo !CY!  19!RS! Disk.Lens             !DG!Applications/Files/Inspector!RS!
echo !CY!  20!RS! DNS.Flip              !DG!Applications/Network/Monitor!RS!
echo !CY!  21!RS! Dodge.Blitz           !DG!Games/Shooter!RS!
echo !CY!  22!RS! Dodge.Craft           !DG!Games/Shooter!RS!
echo !CY!  23!RS! Drive.Bench           !DG!Applications/System/Monitor!RS!
echo !CY!  24!RS! Dungeon.Dash          !DG!Games/Action!RS!
echo !CY!  25!RS! Echo.Text             !DG!Applications/Text!RS!
echo !CY!  26!RS! Env.Guard             !DG!Applications/System/Manager!RS!
echo !CY!  27!RS! Ext.Boss              !DG!Applications/System/Manager!RS!
echo !CY!  28!RS! File.Duplicates       !DG!Applications/Files/Manager!RS!
echo !CY!  29!RS! File.Unlocker         !DG!Applications/Files/Manager!RS!
echo !CY!  30!RS! Folder.Purge          !DG!Applications/Files/Manager!RS!
echo !CY!  31!RS! Geo.Quiz              !DG!Games/Casual!RS!
echo !CY!  32!RS! Git.Stats             !DG!Applications/Development/Analyzer!RS!
echo !CY!  33!RS! Glyph.Map             !DG!Applications/Emoji.Icon!RS!
echo !CY!  34!RS! Gravity.Flip          !DG!Games/Puzzle!RS!
echo !CY!  35!RS! Hash.Check            !DG!Applications/Files/Inspector!RS!
echo !CY!  36!RS! Hex.Peek              !DG!Applications/Development/Inspector!RS!
echo !CY!  37!RS! Hook.Cast             !DG!Games/Casual!RS!
echo !CY!  38!RS! Hue.Flow              !DG!Games/Puzzle!RS!
echo !CY!  39!RS! Icon.Hunt             !DG!Applications/Emoji.Icon!RS!
echo !CY!  40!RS! Key.Map               !DG!Applications/System/Manager!RS!
echo !CY!  41!RS! Key.Test              !DG!Applications/System/Manager!RS!
echo !CY!  42!RS! Leaf.Grow             !DG!Games/Simulation!RS!
echo !CY!  43!RS! Log.Lens              !DG!Applications/Development/Analyzer!RS!
echo !CY!  44!RS! Log.Merge             !DG!Applications/Development/Analyzer!RS!
echo !CY!  45!RS! Mark.View             !DG!Applications/Text!RS!
echo !CY!  46!RS! Mem.Lens              !DG!Applications/System/Monitor!RS!
echo !CY!  47!RS! Mock.Server           !DG!Applications/Network/Server!RS!
echo !CY!  48!RS! Mosaic.Forge          !DG!Applications/Photo.Picture!RS!
echo !CY!  49!RS! Mouse.Flick           !DG!Applications/Tools.Utility!RS!
echo !CY!  50!RS! Music.Player          !DG!Applications/Audio!RS!
echo !CY!  51!RS! Neon.Run              !DG!Games/Arcade!RS!
echo !CY!  52!RS! Neon.Slice            !DG!Games/Arcade!RS!
echo !CY!  53!RS! Net.Scan              !DG!Applications/Network/Monitor!RS!
echo !CY!  54!RS! Net.Trace             !DG!Applications/Network/Monitor!RS!
echo !CY!  55!RS! Nitro.Drift           !DG!Games/Racing!RS!
echo !CY!  56!RS! Orbit.Craft           !DG!Games/Puzzle!RS!
echo !CY!  57!RS! Path.Guard            !DG!Applications/System/Manager!RS!
echo !CY!  58!RS! PDF.Forge             !DG!Applications/Files/Inspector!RS!
echo !CY!  59!RS! Photo.Video.Organizer !DG!Applications/Photo.Picture!RS!
echo !CY!  60!RS! Port.Watch            !DG!Applications/Network/Monitor!RS!
echo !CY!  61!RS! Prompt.Forge          !DG!Applications/AI!RS!
echo !CY!  62!RS! QR.Forge              !DG!Applications/Tools.Utility!RS!
echo !CY!  63!RS! Quick.Calc            !DG!Applications/Development/Inspector!RS!
echo !CY!  64!RS! Reg.Vault             !DG!Applications/System/Manager!RS!
echo !CY!  65!RS! Sand.Fall             !DG!Games/Sandbox!RS!
echo !CY!  66!RS! Sched.Cast            !DG!Applications/System/Manager!RS!
echo !CY!  67!RS! Screen.Recorder       !DG!Applications/Video!RS!
echo !CY!  68!RS! Serve.Cast            !DG!Applications/Network/Server!RS!
echo !CY!  69!RS! Shortcut.Forge        !DG!Applications/Files/Manager!RS!
echo !CY!  70!RS! Signal.Flow           !DG!Applications/Development/Inspector!RS!
echo !CY!  71!RS! Sky.Drift             !DG!Games/Arcade!RS!
echo !CY!  72!RS! Spec.Report           !DG!Applications/System/Monitor!RS!
echo !CY!  73!RS! Spec.View             !DG!Applications/System/Monitor!RS!
echo !CY!  74!RS! Star.Strike           !DG!Games/Shooter!RS!
echo !CY!  75!RS! Stay.Awake            !DG!Applications/Automation!RS!
echo !CY!  76!RS! Svc.Guard             !DG!Applications/System/Manager!RS!
echo !CY!  77!RS! SVG.Forge             !DG!Applications/Photo.Picture!RS!
echo !CY!  78!RS! Sys.Clean             !DG!Applications/System/Manager!RS!
echo !CY!  79!RS! Table.Craft           !DG!Applications/Data!RS!
echo !CY!  80!RS! Tag.Forge             !DG!Applications/Audio!RS!
echo !CY!  81!RS! Text.Forge            !DG!Applications/Text!RS!
echo !CY!  82!RS! Timeline.Craft        !DG!Applications/Data!RS!
echo !CY!  83!RS! Tower.Guard           !DG!Games/Strategy!RS!
echo !CY!  84!RS! Tray.Stats            !DG!Applications/System/Monitor!RS!
echo !CY!  85!RS! VPN.Cast              !DG!Applications/Network/Monitor!RS!
echo !CY!  86!RS! Wave.Surf             !DG!Games/Casual!RS!
echo !CY!  87!RS! Web.Shot              !DG!Applications/Photo.Picture!RS!
echo !CY!  88!RS! Win.Event             !DG!Applications/Development/Analyzer!RS!
echo !CY!  89!RS! Word.Cloud            !DG!Applications/Text!RS!
echo !CY!  90!RS! Zip.Peek              !DG!Applications/Files/Inspector!RS!
echo !CY!  91!RS! Pad.Forge             !DG!Applications/System/Manager!RS!
echo !CY!  92!RS! Comic.Cast            !DG!Applications/Photo.Picture!RS!
echo !CY!  93!RS! Golf.Cast             !DG!Games/Sports!RS!
echo !CY!  94!RS! Persp.Shift           !DG!Games/Puzzle!RS!
echo !CY!  95!RS! Crossword.Cast        !DG!Games/Puzzle!RS!
echo !CY!  96!RS! Cipher.Quest          !DG!Games/Puzzle!RS!
echo !CY!  97!RS! WiFi.Cast             !DG!Applications/Network!RS!
echo.
echo !DG!--------------------------------------------------!RS!
echo !DG!  번호 입력 (공백/쉼표로 구분)   예: 1 3 5  또는  1,3,5!RS!
echo !DG!  전체 배포: ALL   ^|   취소: Q!RS!
echo.
set /p "SEL=  선택 > "

:: ── 입력 검증 ─────────────────────────────────────────────────────
if /i "!SEL!"=="Q"   goto :EOF
if /i "!SEL!"=="ALL" goto :PUBALL
if "!SEL!"==""       goto :MENU

:: 쉼표를 공백으로 변환
set "SEL=!SEL:,= !"

:: ── 선택 배포 ─────────────────────────────────────────────────────
set /a TOTAL=0
set /a PASS=0
set /a FAIL=0
set "FAILED="

echo.
echo !BD!!CY!Playground ^| Publish (선택)!RS!
echo !DG!Output: %BIN%!RS!
echo !DG!--------------------------------------------------!RS!
echo.

for %%n in (!SEL!) do (
    if "%%n"=="1"  call :pub "AI.Clip"               "Applications\AI\AI.Clip"                                    "Ai.Clip.exe"               "Applications\AI"
    if "%%n"=="2"  call :pub "Api.Probe"              "Applications\Network\Server\Api.Probe"                      "Api.Probe.exe"             "Applications\Network\Server\Api.Probe"
    if "%%n"=="3"  call :pub "App.Temp"               "Applications\Development\Inspector\App.Temp"                "App.Temp.exe"              "Applications\Development\Inspector"
    if "%%n"=="4"  call :pub "Auto.Build"             "Games\Puzzle\Auto.Build"                                    "AutoBuild.exe"             "Games\Puzzle"
    if "%%n"=="5"  call :pub "Batch.Rename"           "Applications\Files\Manager\Batch.Rename"                    "Batch.Rename.exe"          "Applications\Files\Manager"
    if "%%n"=="6"  call :pub "Beat.Drop"              "Games\Rhythm\Beat.Drop"                                     "Beat.Drop.exe"             "Games\Rhythm"
    if "%%n"=="7"  call :pub "Boot.Map"               "Applications\System\Monitor\Boot.Map"                       "Boot.Map.exe"              "Applications\System\Monitor"
    if "%%n"=="8"  call :pub "Brick.Blitz"            "Games\Arcade\Brick.Blitz"                                   "Brick.Blitz.exe"           "Games\Arcade"
    if "%%n"=="9"  call :pub "Burn.Rate"              "Applications\System\Monitor\Burn.Rate"                      "Burn.Rate.exe"             "Applications\System\Monitor"
    if "%%n"=="10" call :pub "Char.Art"               "Applications\Text\Char.Art"                                 "Char.Art.exe"              "Applications\Text"
    if "%%n"=="11" call :pub "Chord.Strike"           "Games\Rhythm\Chord.Strike"                                  "ChordStrike.exe"           "Games\Rhythm"
    if "%%n"=="12" call :pub "Clipboard.Stacker"      "Applications\Tools.Utility\Clipboard.Stacker"               "Clipboard.Stacker.exe"     "Applications\Tools.Utility"
    if "%%n"=="13" call :pub "Code.Idle"              "Games\Idle\Code.Idle"                                       "Code.Idle.exe"             "Games\Idle"
    if "%%n"=="14" call :pub "Color.Grade"            "Applications\Photo.Picture\Color.Grade"                     "Color.Grade.exe"           "Applications\Photo.Picture\Color.Grade"
    if "%%n"=="15" call :pub "Crash.View"             "Applications\Development\Analyzer\Crash.View"               "CrashView.exe"             "Applications\Development\Analyzer"
    if "%%n"=="16" call :pub "Ctx.Menu"               "Applications\System\Manager\Ctx.Menu"                       "Ctx.Menu.exe"              "Applications\System\Manager\Ctx.Menu"
    if "%%n"=="17" call :pub "Dash.City"              "Games\Arcade\Dash.City"                                     "Dash.City.exe"             "Games\Arcade"
    if "%%n"=="18" call :pub "Dict.Cast"              "Applications\Tools.Utility\Dict.Cast"                       "Dict.Cast.exe"             "Applications\Tools.Utility\Dict.Cast"
    if "%%n"=="19" call :pub "Disk.Lens"              "Applications\Files\Inspector\Disk.Lens"                     "Disk.Lens.exe"             "Applications\Files\Inspector\Disk.Lens"
    if "%%n"=="20" call :pub "DNS.Flip"               "Applications\Network\Monitor\DNS.Flip"                      "Dns.Flip.exe"              "Applications\Network\Monitor"
    if "%%n"=="21" call :pub "Dodge.Blitz"            "Games\Shooter\Dodge.Blitz"                                  "Dodge.Blitz.exe"           "Games\Shooter"
    if "%%n"=="22" call :pub "Dodge.Craft"            "Games\Shooter\Dodge.Craft"                                  "DodgeCraft.exe"            "Games\Shooter"
    if "%%n"=="23" call :pub "Drive.Bench"            "Applications\System\Monitor\Drive.Bench"                    "Drive.Bench.exe"           "Applications\System\Monitor"
    if "%%n"=="24" call :pub "Dungeon.Dash"           "Games\Action\Dungeon.Dash"                                  "Dungeon.Dash.exe"          "Games\Action"
    if "%%n"=="25" call :pub "Echo.Text"              "Applications\Text\Echo.Text"                                "Echo.Text.exe"             "Applications\Text\Echo.Text"
    if "%%n"=="26" call :pub "Env.Guard"              "Applications\System\Manager\Env.Guard"                      "Env.Guard.exe"             "Applications\System\Manager"
    if "%%n"=="27" call :pub "Ext.Boss"               "Applications\System\Manager\Ext.Boss"                       "Ext.Boss.exe"              "Applications\System\Manager\Ext.Boss"
    if "%%n"=="28" call :pub "File.Duplicates"        "Applications\Files\Manager\File.Duplicates"                 "File.Duplicates.exe"       "Applications\Files\Manager"
    if "%%n"=="29" call :pub "File.Unlocker"          "Applications\Files\Manager\File.Unlocker"                   "File.Unlocker.exe"         "Applications\Files\Manager"
    if "%%n"=="30" call :pub "Folder.Purge"           "Applications\Files\Manager\Folder.Purge"                    "Folder.Purge.exe"          "Applications\Files\Manager"
    if "%%n"=="31" call :pub "Geo.Quiz"               "Games\Casual\Geo.Quiz"                                      "Geo.Quiz.exe"              "Games\Casual"
    if "%%n"=="32" call :pub "Git.Stats"              "Applications\Development\Analyzer\Git.Stats"                "Git.Stats.exe"             "Applications\Development\Analyzer"
    if "%%n"=="33" call :pub "Glyph.Map"              "Applications\Emoji.Icon\Glyph.Map"                          "Glyph.Map.exe"             "Applications\Emoji.Icon\Glyph.Map"
    if "%%n"=="34" call :pub "Gravity.Flip"           "Games\Puzzle\Gravity.Flip"                                  "Gravity.Flip.exe"          "Games\Puzzle"
    if "%%n"=="35" call :pub "Hash.Check"             "Applications\Files\Inspector\Hash.Check"                    "Hash.Check.exe"            "Applications\Files\Inspector\Hash.Check"
    if "%%n"=="36" call :pub "Hex.Peek"               "Applications\Development\Inspector\Hex.Peek"                "Hex.Peek.exe"              "Applications\Development\Inspector"
    if "%%n"=="37" call :pub "Hook.Cast"              "Games\Casual\Hook.Cast"                                     "Hook.Cast.exe"             "Games\Casual"
    if "%%n"=="38" call :pub "Hue.Flow"               "Games\Puzzle\Hue.Flow"                                      "Hue.Flow.exe"              "Games\Puzzle"
    if "%%n"=="39" call :pub "Icon.Hunt"              "Applications\Emoji.Icon\Icon.Hunt"                          "Icon.Hunt.exe"             "Applications\Emoji.Icon"
    if "%%n"=="40" call :pub "Key.Map"                "Applications\System\Manager\Key.Map"                        "Key.Map.exe"               "Applications\System\Manager\Key.Map"
    if "%%n"=="41" call :pub "Key.Test"               "Applications\System\Manager\Key.Test"                       "KeyTest.exe"               "Applications\System\Manager"
    if "%%n"=="42" call :pub "Leaf.Grow"              "Games\Simulation\Leaf.Grow"                                 "Leaf.Grow.exe"             "Games\Simulation"
    if "%%n"=="43" call :pub "Log.Lens"               "Applications\Development\Analyzer\Log.Lens"                 "Log.Lens.exe"              "Applications\Development\Analyzer"
    if "%%n"=="44" call :pub "Log.Merge"              "Applications\Development\Analyzer\Log.Merge"                "Log.Merge.exe"             "Applications\Development\Analyzer"
    if "%%n"=="45" call :pub "Mark.View"              "Applications\Text\Mark.View"                                "Mark.View.exe"             "Applications\Text\Mark.View"
    if "%%n"=="46" call :pub "Mem.Lens"               "Applications\System\Monitor\Mem.Lens"                       "MemLens.exe"               "Applications\System\Monitor"
    if "%%n"=="47" call :pub "Mock.Server"            "Applications\Network\Server\Mock.Server"                    "Mock.Server.exe"           "Applications\Network\Server"
    if "%%n"=="48" call :pub "Mosaic.Forge"           "Applications\Photo.Picture\Mosaic.Forge"                    "Mosaic.Forge.exe"          "Applications\Photo.Picture\Mosaic.Forge"
    if "%%n"=="49" call :pub "Mouse.Flick"            "Applications\Tools.Utility\Mouse.Flick"                     "Mouse.Flick.exe"           "Applications\Tools.Utility"
    if "%%n"=="50" call :pub "Music.Player"           "Applications\Audio\Music.Player"                            "Music.Player.exe"          "Applications\Audio"
    if "%%n"=="51" call :pub "Neon.Run"               "Games\Arcade\Neon.Run"                                      "Neon.Run.exe"              "Games\Arcade"
    if "%%n"=="52" call :pub "Neon.Slice"             "Games\Arcade\Neon.Slice"                                    "Neon.Slice.exe"            "Games\Arcade\Neon.Slice"
    if "%%n"=="53" call :pub "Net.Scan"               "Applications\Network\Monitor\Net.Scan"                      "Net.Scan.exe"              "Applications\Network\Monitor\Net.Scan"
    if "%%n"=="54" call :pub "Net.Trace"              "Applications\Network\Monitor\Net.Trace"                     "Net.Trace.exe"             "Applications\Network\Monitor"
    if "%%n"=="55" call :pub "Nitro.Drift"            "Games\Racing\Nitro.Drift"                                   "Nitro.Drift.exe"           "Games\Racing"
    if "%%n"=="56" call :pub "Orbit.Craft"            "Games\Puzzle\Orbit.Craft"                                   "Orbit.Craft.exe"           "Games\Puzzle\Orbit.Craft"
    if "%%n"=="57" call :pub "Path.Guard"             "Applications\System\Manager\Path.Guard"                     "PathGuard.exe"             "Applications\System\Manager"
    if "%%n"=="58" call :pub "PDF.Forge"              "Applications\Files\Inspector\PDF.Forge"                     "Pdf.Forge.exe"             "Applications\Files\Inspector\Pdf.Forge"
    if "%%n"=="59" call :pub "Photo.Video.Organizer"  "Applications\Photo.Picture\Photo.Video.Organizer"           "Photo.Video.Organizer.exe" "Applications\Photo.Picture"
    if "%%n"=="60" call :pub "Port.Watch"             "Applications\Network\Monitor\Port.Watch"                    "Port.Watch.exe"            "Applications\Network\Monitor"
    if "%%n"=="61" call :pub "Prompt.Forge"           "Applications\AI\Prompt.Forge"                               "Prompt.Forge.exe"          "Applications\AI\Prompt.Forge"
    if "%%n"=="62" call :pub "QR.Forge"               "Applications\Tools.Utility\QR.Forge"                        "QR.Forge.exe"              "Applications\Tools.Utility"
    if "%%n"=="63" call :pub "Quick.Calc"             "Applications\Development\Inspector\Quick.Calc"              "Quick.Calc.exe"            "Applications\Development\Inspector"
    if "%%n"=="64" call :pub "Reg.Vault"              "Applications\System\Manager\Reg.Vault"                      "RegVault.exe"              "Applications\System\Manager"
    if "%%n"=="65" call :pub "Sand.Fall"              "Games\Sandbox\Sand.Fall"                                    "Sand.Fall.exe"             "Games\Sandbox"
    if "%%n"=="66" call :pub "Sched.Cast"             "Applications\System\Manager\Sched.Cast"                     "Sched.Cast.exe"            "Applications\System\Manager\Sched.Cast"
    if "%%n"=="67" call :pub "Screen.Recorder"        "Applications\Video\Screen.Recorder"                         "Screen.Recorder.exe"       "Applications\Video"
    if "%%n"=="68" call :pub "Serve.Cast"             "Applications\Network\Server\Serve.Cast"                     "Serve.Cast.exe"            "Applications\Network\Server"
    if "%%n"=="69" call :pub "Shortcut.Forge"         "Applications\Files\Manager\Shortcut.Forge"                  "ShortcutForge.exe"         "Applications\Files\Manager"
    if "%%n"=="70" call :pub "Signal.Flow"            "Applications\Development\Inspector\Signal.Flow"             "Signal.Flow.exe"           "Applications\Development\Inspector"
    if "%%n"=="71" call :pub "Sky.Drift"              "Games\Arcade\Sky.Drift"                                     "SkyDrift.exe"              "Games\Arcade"
    if "%%n"=="72" call :pub "Spec.Report"            "Applications\System\Monitor\Spec.Report"                    "Spec.Report.exe"           "Applications\System\Monitor"
    if "%%n"=="73" call :pub "Spec.View"              "Applications\System\Monitor\Spec.View"                      "Spec.View.exe"             "Applications\System\Monitor\Spec.View"
    if "%%n"=="74" call :pub "Star.Strike"            "Games\Shooter\Star.Strike"                                  "Star.Strike.exe"           "Games\Shooter"
    if "%%n"=="75" call :pub "Stay.Awake"             "Applications\Automation\Stay.Awake"                         "Stay.Awake.exe"            "Applications\Automation\Stay.Awake"
    if "%%n"=="76" call :pub "Svc.Guard"              "Applications\System\Manager\Svc.Guard"                      "Svc.Guard.exe"             "Applications\System\Manager\Svc.Guard"
    if "%%n"=="77" call :pub "SVG.Forge"              "Applications\Photo.Picture\SVG.Forge"                       "SVG.Forge.exe"             "Applications\Photo.Picture\SVG.Forge"
    if "%%n"=="78" call :pub "Sys.Clean"              "Applications\System\Manager\Sys.Clean"                      "Sys.Clean.exe"             "Applications\System\Manager\Sys.Clean"
    if "%%n"=="79" call :pub "Table.Craft"            "Applications\Data\Table.Craft"                              "Table.Craft.exe"           "Applications\Data"
    if "%%n"=="80" call :pub "Tag.Forge"              "Applications\Audio\Tag.Forge"                               "Tag.Forge.exe"             "Applications\Audio\Tag.Forge"
    if "%%n"=="81" call :pub "Text.Forge"             "Applications\Text\Text.Forge"                               "Text.Forge.exe"            "Applications\Text"
    if "%%n"=="82" call :pub "Timeline.Craft"         "Applications\Data\Timeline.Craft"                           "Timeline.Craft.exe"        "Applications\Data\Timeline.Craft"
    if "%%n"=="83" call :pub "Tower.Guard"            "Games\Strategy\Tower.Guard"                                 "Tower.Guard.exe"           "Games\Strategy"
    if "%%n"=="84" call :pub "Tray.Stats"             "Applications\System\Monitor\Tray.Stats"                     "Tray.Stats.exe"            "Applications\System\Monitor\Tray.Stats"
    if "%%n"=="85" call :pub "VPN.Cast"               "Applications\Network\Monitor\VPN.Cast"                      "VpnCast.exe"               "Applications\Network\Monitor"
    if "%%n"=="86" call :pub "Wave.Surf"              "Games\Casual\Wave.Surf"                                     "Wave.Surf.exe"             "Games\Casual"
    if "%%n"=="87" call :pub "Web.Shot"               "Applications\Photo.Picture\Web.Shot"                        "Web.Shot.exe"              "Applications\Photo.Picture\Web.Shot"
    if "%%n"=="88" call :pub "Win.Event"              "Applications\Development\Analyzer\Win.Event"                "WinEvent.exe"              "Applications\Development\Analyzer"
    if "%%n"=="89" call :pub "Word.Cloud"             "Applications\Text\Word.Cloud"                               "Word.Cloud.exe"            "Applications\Text"
    if "%%n"=="90" call :pub "Zip.Peek"               "Applications\Files\Inspector\Zip.Peek"                      "Zip.Peek.exe"              "Applications\Files\Inspector\Zip.Peek"
    if "%%n"=="91" call :pub "Pad.Forge"              "Applications\System\Manager\Pad.Forge"                      "Pad.Forge.exe"             "Applications\System\Manager\Pad.Forge"
    if "%%n"=="92" call :pub "Comic.Cast"             "Applications\Photo.Picture\Comic.Cast"                      "Comic.Cast.exe"            "Applications\Photo.Picture\Comic.Cast"
    if "%%n"=="93" call :pub "Golf.Cast"              "Games\Sports\Golf.Cast"                                     "Golf.Cast.exe"             "Games\Sports"
    if "%%n"=="94" call :pub "Persp.Shift"            "Games\Puzzle\Persp.Shift"                                   "Persp.Shift.exe"           "Games\Puzzle"
    if "%%n"=="95" call :pub "Crossword.Cast"         "Games\Puzzle\Crossword.Cast"                                "Crossword.Cast.exe"        "Games\Puzzle"
    if "%%n"=="96" call :pub "Cipher.Quest"           "Games\Puzzle\Cipher.Quest"                                  "Cipher.Quest.exe"          "Games\Puzzle"
    if "%%n"=="97" call :pub "WiFi.Cast"              "Applications\Network\WiFi.Cast"                             "WiFiCast.exe"              "Applications\Network"
)
goto :DONE

:: ── 전체 배포 ─────────────────────────────────────────────────────
:PUBALL
set /a TOTAL=0
set /a PASS=0
set /a FAIL=0
set "FAILED="

echo.
echo !BD!!CY!Playground ^| Publish-All!RS!
echo !DG!Output: %BIN%!RS!
echo !DG!--------------------------------------------------!RS!
echo.

call :pub "AI.Clip"               "Applications\AI\AI.Clip"                                    "Ai.Clip.exe"               "Applications\AI"
call :pub "Prompt.Forge"          "Applications\AI\Prompt.Forge"                               "Prompt.Forge.exe"          "Applications\AI\Prompt.Forge"
call :pub "Music.Player"          "Applications\Audio\Music.Player"                            "Music.Player.exe"          "Applications\Audio"
call :pub "Tag.Forge"             "Applications\Audio\Tag.Forge"                               "Tag.Forge.exe"             "Applications\Audio\Tag.Forge"
call :pub "Stay.Awake"            "Applications\Automation\Stay.Awake"                         "Stay.Awake.exe"            "Applications\Automation\Stay.Awake"
call :pub "Table.Craft"           "Applications\Data\Table.Craft"                              "Table.Craft.exe"           "Applications\Data"
call :pub "Timeline.Craft"        "Applications\Data\Timeline.Craft"                           "Timeline.Craft.exe"        "Applications\Data\Timeline.Craft"
call :pub "Crash.View"            "Applications\Development\Analyzer\Crash.View"               "CrashView.exe"             "Applications\Development\Analyzer"
call :pub "Git.Stats"             "Applications\Development\Analyzer\Git.Stats"                "Git.Stats.exe"             "Applications\Development\Analyzer"
call :pub "Log.Lens"              "Applications\Development\Analyzer\Log.Lens"                 "Log.Lens.exe"              "Applications\Development\Analyzer"
call :pub "Log.Merge"             "Applications\Development\Analyzer\Log.Merge"                "Log.Merge.exe"             "Applications\Development\Analyzer"
call :pub "Win.Event"             "Applications\Development\Analyzer\Win.Event"                "WinEvent.exe"              "Applications\Development\Analyzer"
call :pub "App.Temp"              "Applications\Development\Inspector\App.Temp"                "App.Temp.exe"              "Applications\Development\Inspector"
call :pub "Hex.Peek"              "Applications\Development\Inspector\Hex.Peek"                "Hex.Peek.exe"              "Applications\Development\Inspector"
call :pub "Quick.Calc"            "Applications\Development\Inspector\Quick.Calc"              "Quick.Calc.exe"            "Applications\Development\Inspector"
call :pub "Signal.Flow"           "Applications\Development\Inspector\Signal.Flow"             "Signal.Flow.exe"           "Applications\Development\Inspector"
call :pub "Glyph.Map"             "Applications\Emoji.Icon\Glyph.Map"                          "Glyph.Map.exe"             "Applications\Emoji.Icon\Glyph.Map"
call :pub "Icon.Hunt"             "Applications\Emoji.Icon\Icon.Hunt"                          "Icon.Hunt.exe"             "Applications\Emoji.Icon"
call :pub "Disk.Lens"             "Applications\Files\Inspector\Disk.Lens"                     "Disk.Lens.exe"             "Applications\Files\Inspector\Disk.Lens"
call :pub "Hash.Check"            "Applications\Files\Inspector\Hash.Check"                    "Hash.Check.exe"            "Applications\Files\Inspector\Hash.Check"
call :pub "PDF.Forge"             "Applications\Files\Inspector\PDF.Forge"                     "Pdf.Forge.exe"             "Applications\Files\Inspector\Pdf.Forge"
call :pub "Zip.Peek"              "Applications\Files\Inspector\Zip.Peek"                      "Zip.Peek.exe"              "Applications\Files\Inspector\Zip.Peek"
call :pub "Batch.Rename"          "Applications\Files\Manager\Batch.Rename"                    "Batch.Rename.exe"          "Applications\Files\Manager"
call :pub "File.Duplicates"       "Applications\Files\Manager\File.Duplicates"                 "File.Duplicates.exe"       "Applications\Files\Manager"
call :pub "File.Unlocker"         "Applications\Files\Manager\File.Unlocker"                   "File.Unlocker.exe"         "Applications\Files\Manager"
call :pub "Folder.Purge"          "Applications\Files\Manager\Folder.Purge"                    "Folder.Purge.exe"          "Applications\Files\Manager"
call :pub "Shortcut.Forge"        "Applications\Files\Manager\Shortcut.Forge"                  "ShortcutForge.exe"         "Applications\Files\Manager"
call :pub "Api.Probe"             "Applications\Network\Server\Api.Probe"                      "Api.Probe.exe"             "Applications\Network\Server\Api.Probe"
call :pub "Mock.Server"           "Applications\Network\Server\Mock.Server"                    "Mock.Server.exe"           "Applications\Network\Server"
call :pub "Serve.Cast"            "Applications\Network\Server\Serve.Cast"                     "Serve.Cast.exe"            "Applications\Network\Server"
call :pub "DNS.Flip"              "Applications\Network\Monitor\DNS.Flip"                      "Dns.Flip.exe"              "Applications\Network\Monitor"
call :pub "Net.Scan"              "Applications\Network\Monitor\Net.Scan"                      "Net.Scan.exe"              "Applications\Network\Monitor\Net.Scan"
call :pub "Net.Trace"             "Applications\Network\Monitor\Net.Trace"                     "Net.Trace.exe"             "Applications\Network\Monitor"
call :pub "Port.Watch"            "Applications\Network\Monitor\Port.Watch"                    "Port.Watch.exe"            "Applications\Network\Monitor"
call :pub "VPN.Cast"              "Applications\Network\Monitor\VPN.Cast"                      "VpnCast.exe"               "Applications\Network\Monitor"
call :pub "Color.Grade"           "Applications\Photo.Picture\Color.Grade"                     "Color.Grade.exe"           "Applications\Photo.Picture\Color.Grade"
call :pub "Mosaic.Forge"          "Applications\Photo.Picture\Mosaic.Forge"                    "Mosaic.Forge.exe"          "Applications\Photo.Picture\Mosaic.Forge"
call :pub "Photo.Video.Organizer" "Applications\Photo.Picture\Photo.Video.Organizer"           "Photo.Video.Organizer.exe" "Applications\Photo.Picture"
call :pub "SVG.Forge"             "Applications\Photo.Picture\SVG.Forge"                       "SVG.Forge.exe"             "Applications\Photo.Picture\SVG.Forge"
call :pub "Web.Shot"              "Applications\Photo.Picture\Web.Shot"                        "Web.Shot.exe"              "Applications\Photo.Picture\Web.Shot"
call :pub "Boot.Map"              "Applications\System\Monitor\Boot.Map"                       "Boot.Map.exe"              "Applications\System\Monitor"
call :pub "Burn.Rate"             "Applications\System\Monitor\Burn.Rate"                      "Burn.Rate.exe"             "Applications\System\Monitor"
call :pub "Drive.Bench"           "Applications\System\Monitor\Drive.Bench"                    "Drive.Bench.exe"           "Applications\System\Monitor"
call :pub "Mem.Lens"              "Applications\System\Monitor\Mem.Lens"                       "MemLens.exe"               "Applications\System\Monitor"
call :pub "Spec.Report"           "Applications\System\Monitor\Spec.Report"                    "Spec.Report.exe"           "Applications\System\Monitor"
call :pub "Spec.View"             "Applications\System\Monitor\Spec.View"                      "Spec.View.exe"             "Applications\System\Monitor\Spec.View"
call :pub "Tray.Stats"            "Applications\System\Monitor\Tray.Stats"                     "Tray.Stats.exe"            "Applications\System\Monitor\Tray.Stats"
call :pub "Ctx.Menu"              "Applications\System\Manager\Ctx.Menu"                       "Ctx.Menu.exe"              "Applications\System\Manager\Ctx.Menu"
call :pub "Env.Guard"             "Applications\System\Manager\Env.Guard"                      "Env.Guard.exe"             "Applications\System\Manager"
call :pub "Ext.Boss"              "Applications\System\Manager\Ext.Boss"                       "Ext.Boss.exe"              "Applications\System\Manager\Ext.Boss"
call :pub "Key.Map"               "Applications\System\Manager\Key.Map"                        "Key.Map.exe"               "Applications\System\Manager\Key.Map"
call :pub "Key.Test"              "Applications\System\Manager\Key.Test"                       "KeyTest.exe"               "Applications\System\Manager"
call :pub "Path.Guard"            "Applications\System\Manager\Path.Guard"                     "PathGuard.exe"             "Applications\System\Manager"
call :pub "Reg.Vault"             "Applications\System\Manager\Reg.Vault"                      "RegVault.exe"              "Applications\System\Manager"
call :pub "Sched.Cast"            "Applications\System\Manager\Sched.Cast"                     "Sched.Cast.exe"            "Applications\System\Manager\Sched.Cast"
call :pub "Svc.Guard"             "Applications\System\Manager\Svc.Guard"                      "Svc.Guard.exe"             "Applications\System\Manager\Svc.Guard"
call :pub "Sys.Clean"             "Applications\System\Manager\Sys.Clean"                      "Sys.Clean.exe"             "Applications\System\Manager\Sys.Clean"
call :pub "Char.Art"              "Applications\Text\Char.Art"                                 "Char.Art.exe"              "Applications\Text"
call :pub "Echo.Text"             "Applications\Text\Echo.Text"                                "Echo.Text.exe"             "Applications\Text\Echo.Text"
call :pub "Mark.View"             "Applications\Text\Mark.View"                                "Mark.View.exe"             "Applications\Text\Mark.View"
call :pub "Text.Forge"            "Applications\Text\Text.Forge"                               "Text.Forge.exe"            "Applications\Text"
call :pub "Word.Cloud"            "Applications\Text\Word.Cloud"                               "Word.Cloud.exe"            "Applications\Text"
call :pub "Clipboard.Stacker"     "Applications\Tools.Utility\Clipboard.Stacker"               "Clipboard.Stacker.exe"     "Applications\Tools.Utility"
call :pub "Dict.Cast"             "Applications\Tools.Utility\Dict.Cast"                       "Dict.Cast.exe"             "Applications\Tools.Utility\Dict.Cast"
call :pub "Mouse.Flick"           "Applications\Tools.Utility\Mouse.Flick"                     "Mouse.Flick.exe"           "Applications\Tools.Utility"
call :pub "QR.Forge"              "Applications\Tools.Utility\QR.Forge"                        "QR.Forge.exe"              "Applications\Tools.Utility"
call :pub "Screen.Recorder"       "Applications\Video\Screen.Recorder"                         "Screen.Recorder.exe"       "Applications\Video"
call :pub "Dungeon.Dash"          "Games\Action\Dungeon.Dash"                                  "Dungeon.Dash.exe"          "Games\Action"
call :pub "Brick.Blitz"           "Games\Arcade\Brick.Blitz"                                   "Brick.Blitz.exe"           "Games\Arcade"
call :pub "Dash.City"             "Games\Arcade\Dash.City"                                     "Dash.City.exe"             "Games\Arcade"
call :pub "Neon.Run"              "Games\Arcade\Neon.Run"                                      "Neon.Run.exe"              "Games\Arcade"
call :pub "Neon.Slice"            "Games\Arcade\Neon.Slice"                                    "Neon.Slice.exe"            "Games\Arcade\Neon.Slice"
call :pub "Sky.Drift"             "Games\Arcade\Sky.Drift"                                     "SkyDrift.exe"              "Games\Arcade"
call :pub "Geo.Quiz"              "Games\Casual\Geo.Quiz"                                      "Geo.Quiz.exe"              "Games\Casual"
call :pub "Hook.Cast"             "Games\Casual\Hook.Cast"                                     "Hook.Cast.exe"             "Games\Casual"
call :pub "Wave.Surf"             "Games\Casual\Wave.Surf"                                     "Wave.Surf.exe"             "Games\Casual"
call :pub "Code.Idle"             "Games\Idle\Code.Idle"                                       "Code.Idle.exe"             "Games\Idle"
call :pub "Auto.Build"            "Games\Puzzle\Auto.Build"                                    "AutoBuild.exe"             "Games\Puzzle"
call :pub "Gravity.Flip"          "Games\Puzzle\Gravity.Flip"                                  "Gravity.Flip.exe"          "Games\Puzzle"
call :pub "Hue.Flow"              "Games\Puzzle\Hue.Flow"                                      "Hue.Flow.exe"              "Games\Puzzle"
call :pub "Orbit.Craft"           "Games\Puzzle\Orbit.Craft"                                   "Orbit.Craft.exe"           "Games\Puzzle\Orbit.Craft"
call :pub "Nitro.Drift"           "Games\Racing\Nitro.Drift"                                   "Nitro.Drift.exe"           "Games\Racing"
call :pub "Beat.Drop"             "Games\Rhythm\Beat.Drop"                                     "Beat.Drop.exe"             "Games\Rhythm"
call :pub "Chord.Strike"          "Games\Rhythm\Chord.Strike"                                  "ChordStrike.exe"           "Games\Rhythm"
call :pub "Sand.Fall"             "Games\Sandbox\Sand.Fall"                                    "Sand.Fall.exe"             "Games\Sandbox"
call :pub "Dodge.Blitz"           "Games\Shooter\Dodge.Blitz"                                  "Dodge.Blitz.exe"           "Games\Shooter"
call :pub "Dodge.Craft"           "Games\Shooter\Dodge.Craft"                                  "DodgeCraft.exe"            "Games\Shooter"
call :pub "Star.Strike"           "Games\Shooter\Star.Strike"                                  "Star.Strike.exe"           "Games\Shooter"
call :pub "Leaf.Grow"             "Games\Simulation\Leaf.Grow"                                 "Leaf.Grow.exe"             "Games\Simulation"
call :pub "Tower.Guard"           "Games\Strategy\Tower.Guard"                                 "Tower.Guard.exe"           "Games\Strategy"
call :pub "Pad.Forge"             "Applications\System\Manager\Pad.Forge"                      "Pad.Forge.exe"             "Applications\System\Manager\Pad.Forge"
call :pub "Comic.Cast"            "Applications\Photo.Picture\Comic.Cast"                      "Comic.Cast.exe"            "Applications\Photo.Picture\Comic.Cast"
call :pub "Golf.Cast"             "Games\Sports\Golf.Cast"                                     "Golf.Cast.exe"             "Games\Sports"
call :pub "Persp.Shift"           "Games\Puzzle\Persp.Shift"                                   "Persp.Shift.exe"           "Games\Puzzle"
call :pub "Crossword.Cast"        "Games\Puzzle\Crossword.Cast"                                "Crossword.Cast.exe"        "Games\Puzzle"
call :pub "Cipher.Quest"          "Games\Puzzle\Cipher.Quest"                                  "Cipher.Quest.exe"          "Games\Puzzle"
call :pub "WiFi.Cast"             "Applications\Network\WiFi.Cast"                             "WiFiCast.exe"              "Applications\Network"

:: ── 완료 ──────────────────────────────────────────────────────────
:DONE
echo !DG!Cleaning .pdb files...!RS!
del /s /q "%BIN%\*.pdb" 2>nul
echo !DG!--------------------------------------------------!RS!
echo.
if !FAIL! gtr 0 echo !BD!!RE!Result: !PASS!/!TOTAL! OK  ^|  Failed:!FAILED!!RS!
if !FAIL! equ 0 echo !BD!!GR!Result: !PASS!/!TOTAL! All succeeded!RS!
echo.

set "NOTIFY=%ROOT%.claude\Scripts\notify\notify.ps1"
if !FAIL! equ 0 (
    set "MSG=Publish 성공: !PASS!/!TOTAL! 프로젝트 배포 완료"
    set "LV=Info"
) else (
    set "MSG=Publish 실패: !PASS!/!TOTAL! 성공, 실패:!FAILED!"
    set "LV=Error"
)
powershell -ExecutionPolicy Bypass -File "%NOTIFY%" -Message "!MSG!" -Level !LV! -Title "Playground Publish"

pause
goto :eof

:: --------------------------------------------------------------------------
:pub
set /a TOTAL+=1
set "NM=%~1"
set "DIR=%ROOT%%~2"
set "EX=%~3"
set "OUT=%BIN%\%~4"
if not exist "!OUT!" mkdir "!OUT!"
echo !CY!  ^> %NM%!RS!
echo !DG!    %~4\%EX%!RS!
dotnet publish "%DIR%" -c Release -o "!OUT!" > "%TEMP%\pub_%NM%.log" 2>&1
set "RC=!errorlevel!"
if !RC! equ 0 set /a PASS+=1
if !RC! equ 0 echo !GR!    [OK]!RS!
if !RC! neq 0 set /a FAIL+=1
if !RC! neq 0 set "FAILED=!FAILED! %NM%"
if !RC! neq 0 echo !RE!    [!!]  Failed - log: %TEMP%\pub_%NM%.log!RS!
echo.
goto :eof
