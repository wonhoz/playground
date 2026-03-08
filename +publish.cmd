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
echo !BD!!DG!  Applications!RS!
echo !CY!   1!RS! AI.Clip               !DG!Applications/AI!RS!
echo !CY!   2!RS! Music.Player          !DG!Applications/Audio!RS!
echo !CY!   3!RS! Stay.Awake            !DG!Applications/Automation!RS!
echo !DG!      Files / Manager!RS!
echo !CY!   4!RS! Batch.Rename          !DG!Applications/Files/Manager!RS!
echo !CY!   5!RS! File.Duplicates       !DG!Applications/Files/Manager!RS!
echo !CY!   6!RS! File.Unlocker         !DG!Applications/Files/Manager!RS!
echo !CY!   7!RS! Folder.Purge          !DG!Applications/Files/Manager!RS!
echo !DG!      Files / Inspector!RS!
echo !CY!   8!RS! Disk.Lens             !DG!Applications/Files/Inspector!RS!
echo !CY!   9!RS! Hash.Check            !DG!Applications/Files/Inspector!RS!
echo !CY!  10!RS! PDF.Forge             !DG!Applications/Files/Inspector!RS!
echo !CY!  11!RS! Zip.Peek              !DG!Applications/Files/Inspector!RS!
echo !CY!  12!RS! Toast.Cast            !DG!Applications/Health!RS!
echo !CY!  13!RS! Photo.Video.Organizer !DG!Applications/Media!RS!
echo.
echo !BD!!DG!  Tools / Dev!RS!
echo !DG!      Dev / Network!RS!
echo !CY!  14!RS! Api.Probe             !DG!Tools/Dev/Network!RS!
echo !CY!  15!RS! Mock.Server           !DG!Tools/Dev/Network!RS!
echo !CY!  16!RS! Serve.Cast            !DG!Tools/Dev/Network!RS!
echo !DG!      Dev / Debug!RS!
echo !CY!  17!RS! Hex.Peek              !DG!Tools/Dev/Debug!RS!
echo !CY!  18!RS! Log.Lens              !DG!Tools/Dev/Debug!RS!
echo !CY!  19!RS! Signal.Flow           !DG!Tools/Dev/Debug!RS!
echo !DG!      Dev / Assets!RS!
echo !CY!  20!RS! Glyph.Map             !DG!Tools/Dev/Assets!RS!
echo !CY!  21!RS! Icon.Hunt             !DG!Tools/Dev/Assets!RS!
echo !CY!  22!RS! Key.Map               !DG!Tools/Dev/Assets!RS!
echo !CY!  23!RS! Locale.Forge          !DG!Tools/Dev/Assets!RS!
echo !DG!      Dev / Data!RS!
echo !CY!  24!RS! Boot.Map              !DG!Tools/Dev/Data!RS!
echo !CY!  25!RS! Table.Craft           !DG!Tools/Dev/Data!RS!
echo !DG!      Dev / System!RS!
echo !CY!  26!RS! Layout.Forge          !DG!Tools/Dev/System!RS!
echo !CY!  27!RS! Sched.Cast            !DG!Tools/Dev/System!RS!
echo.
echo !BD!!DG!  Tools / Network!RS!
echo !CY!  28!RS! DNS.Flip              !DG!Tools/Network!RS!
echo !CY!  29!RS! Net.Scan              !DG!Tools/Network!RS!
echo !CY!  30!RS! Port.Watch            !DG!Tools/Network!RS!
echo.
echo !BD!!DG!  Tools / Productivity!RS!
echo !DG!      Productivity / Capture!RS!
echo !CY!  31!RS! Code.Snap             !DG!Tools/Productivity/Capture!RS!
echo !CY!  32!RS! Screen.Recorder       !DG!Tools/Productivity/Capture!RS!
echo !DG!      Productivity / Text!RS!
echo !CY!  33!RS! Echo.Text             !DG!Tools/Productivity/Text!RS!
echo !CY!  34!RS! Mark.View             !DG!Tools/Productivity/Text!RS!
echo !CY!  35!RS! Text.Forge            !DG!Tools/Productivity/Text!RS!
echo !DG!      Productivity / Visual!RS!
echo !CY!  36!RS! Char.Art              !DG!Tools/Productivity/Visual!RS!
echo !CY!  37!RS! Timeline.Craft        !DG!Tools/Productivity/Visual!RS!
echo !CY!  38!RS! Word.Cloud            !DG!Tools/Productivity/Visual!RS!
echo !DG!      Productivity / Utility!RS!
echo !CY!  39!RS! Clipboard.Stacker     !DG!Tools/Productivity/Utility!RS!
echo !CY!  40!RS! Dict.Cast             !DG!Tools/Productivity/Utility!RS!
echo !CY!  41!RS! Mouse.Flick           !DG!Tools/Productivity/Utility!RS!
echo !CY!  42!RS! Prompt.Forge          !DG!Tools/Productivity/Utility!RS!
echo !CY!  43!RS! QR.Forge              !DG!Tools/Productivity/Utility!RS!
echo !DG!      Productivity / Media!RS!
echo !CY!  44!RS! Tag.Forge             !DG!Tools/Productivity/Media!RS!
echo !DG!      Productivity / Creative!RS!
echo !CY!  45!RS! Color.Grade           !DG!Tools/Productivity/Creative!RS!
echo.
echo !BD!!DG!  Tools / System!RS!
echo !CY!  46!RS! Env.Guard             !DG!Tools/System!RS!
echo !CY!  47!RS! Sys.Clean             !DG!Tools/System!RS!
echo !CY!  48!RS! Tray.Stats            !DG!Tools/System!RS!
echo.
echo !BD!!DG!  Games!RS!
echo !CY!  49!RS! Mosaic.Forge          !DG!Applications/Media!RS!
echo !CY!  50!RS! Dungeon.Dash          !DG!Games/Action!RS!
echo !CY!  51!RS! Brick.Blitz           !DG!Games/Arcade!RS!
echo !CY!  52!RS! Dash.City             !DG!Games/Arcade!RS!
echo !CY!  53!RS! Neon.Run              !DG!Games/Arcade!RS!
echo !CY!  54!RS! Neon.Slice            !DG!Games/Arcade!RS!
echo !CY!  55!RS! Gravity.Flip          !DG!Games/Puzzle!RS!
echo !CY!  56!RS! Hue.Flow              !DG!Games/Puzzle!RS!
echo !CY!  57!RS! Orbit.Craft           !DG!Games/Puzzle!RS!
echo !CY!  58!RS! Nitro.Drift           !DG!Games/Racing!RS!
echo !CY!  59!RS! Beat.Drop             !DG!Games/Rhythm!RS!
echo !CY!  60!RS! Sand.Fall             !DG!Games/Sandbox!RS!
echo !CY!  61!RS! Dodge.Blitz           !DG!Games/Shooter!RS!
echo !CY!  62!RS! Star.Strike           !DG!Games/Shooter!RS!
echo !CY!  63!RS! Tower.Guard           !DG!Games/Strategy!RS!
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
    if "%%n"=="1"  call :pub "AI.Clip"               "Applications\AI\AI.Clip"                                   "Ai.Clip.exe"               "Applications\AI"
    if "%%n"=="2"  call :pub "Music.Player"           "Applications\Audio\Music.Player"                           "Music.Player.exe"          "Applications\Audio"
    if "%%n"=="3"  call :pub "Stay.Awake"             "Applications\Automation\Stay.Awake"                        "Stay.Awake.exe"            "Applications\Automation\Stay.Awake"
    if "%%n"=="4"  call :pub "Batch.Rename"           "Applications\Files\Manager\Batch.Rename"                   "Batch.Rename.exe"          "Applications\Files\Manager"
    if "%%n"=="5"  call :pub "File.Duplicates"        "Applications\Files\Manager\File.Duplicates"                "File.Duplicates.exe"       "Applications\Files\Manager"
    if "%%n"=="6"  call :pub "File.Unlocker"          "Applications\Files\Manager\File.Unlocker"                  "File.Unlocker.exe"         "Applications\Files\Manager"
    if "%%n"=="7"  call :pub "Folder.Purge"           "Applications\Files\Manager\Folder.Purge"                   "Folder.Purge.exe"          "Applications\Files\Manager"
    if "%%n"=="8"  call :pub "Disk.Lens"              "Applications\Files\Inspector\Disk.Lens"                    "Disk.Lens.exe"             "Applications\Files\Inspector\Disk.Lens"
    if "%%n"=="9"  call :pub "Hash.Check"             "Applications\Files\Inspector\Hash.Check"                   "HashCheck.exe"             "Applications\Files\Inspector"
    if "%%n"=="10" call :pub "PDF.Forge"              "Applications\Files\Inspector\PDF.Forge"                    "PdfForge.exe"              "Applications\Files\Inspector"
    if "%%n"=="11" call :pub "Zip.Peek"               "Applications\Files\Inspector\Zip.Peek"                     "ZipPeek.exe"               "Applications\Files\Inspector"
    if "%%n"=="12" call :pub "Toast.Cast"             "Applications\Health\Toast.Cast"                            "Toast.Cast.exe"            "Applications\Health"
    if "%%n"=="13" call :pub "Photo.Video.Organizer"  "Applications\Media\Photo.Video.Organizer"                  "Photo.Video.Organizer.exe" "Applications\Media"
    if "%%n"=="14" call :pub "Api.Probe"              "Applications\Tools\Dev\Network\Api.Probe"                  "Api.Probe.exe"             "Applications\Tools\Dev\Network\Api.Probe"
    if "%%n"=="15" call :pub "Mock.Server"            "Applications\Tools\Dev\Network\Mock.Server"                "Mock.Server.exe"           "Applications\Tools\Dev\Network"
    if "%%n"=="16" call :pub "Serve.Cast"             "Applications\Tools\Dev\Network\Serve.Cast"                 "Serve.Cast.exe"            "Applications\Tools\Dev\Network"
    if "%%n"=="17" call :pub "Hex.Peek"               "Applications\Tools\Dev\Debug\Hex.Peek"                     "Hex.Peek.exe"              "Applications\Tools\Dev\Debug"
    if "%%n"=="18" call :pub "Log.Lens"               "Applications\Tools\Dev\Debug\Log.Lens"                     "Log.Lens.exe"              "Applications\Tools\Dev\Debug"
    if "%%n"=="19" call :pub "Signal.Flow"            "Applications\Tools\Dev\Debug\Signal.Flow"                  "Signal.Flow.exe"           "Applications\Tools\Dev\Debug"
    if "%%n"=="20" call :pub "Glyph.Map"              "Applications\Tools\Dev\Assets\Glyph.Map"                   "Glyph.Map.exe"             "Applications\Tools\Dev\Assets\Glyph.Map"
    if "%%n"=="21" call :pub "Icon.Hunt"              "Applications\Tools\Dev\Assets\Icon.Hunt"                   "IconHunt.exe"              "Applications\Tools\Dev\Assets"
    if "%%n"=="22" call :pub "Key.Map"                "Applications\Tools\Dev\Assets\Key.Map"                     "Key.Map.exe"               "Applications\Tools\Dev\Assets"
    if "%%n"=="23" call :pub "Locale.Forge"           "Applications\Tools\Dev\Assets\Locale.Forge"                "LocaleForge.exe"           "Applications\Tools\Dev\Assets"
    if "%%n"=="24" call :pub "Boot.Map"               "Applications\Tools\Dev\Data\Boot.Map"                      "BootMap.exe"               "Applications\Tools\Dev\Data"
    if "%%n"=="25" call :pub "Table.Craft"            "Applications\Tools\Dev\Data\Table.Craft"                   "Table.Craft.exe"           "Applications\Tools\Dev\Data"
    if "%%n"=="26" call :pub "Layout.Forge"           "Applications\Tools\Dev\System\Layout.Forge"                "Layout.Forge.exe"          "Applications\Tools\Dev\System"
    if "%%n"=="27" call :pub "Sched.Cast"             "Applications\Tools\Dev\System\Sched.Cast"                  "Sched.Cast.exe"            "Applications\Tools\Dev\System"
    if "%%n"=="28" call :pub "DNS.Flip"               "Applications\Tools\Network\DNS.Flip"                       "Dns.Flip.exe"              "Applications\Tools\Network"
    if "%%n"=="29" call :pub "Net.Scan"               "Applications\Tools\Network\Net.Scan"                       "NetScan.exe"               "Applications\Tools\Network"
    if "%%n"=="30" call :pub "Port.Watch"             "Applications\Tools\Network\Port.Watch"                     "Port.Watch.exe"            "Applications\Tools\Network"
    if "%%n"=="31" call :pub "Code.Snap"              "Applications\Tools\Productivity\Capture\Code.Snap"         "Code.Snap.exe"             "Applications\Tools\Productivity\Capture"
    if "%%n"=="32" call :pub "Screen.Recorder"        "Applications\Tools\Productivity\Capture\Screen.Recorder"   "Screen.Recorder.exe"       "Applications\Tools\Productivity\Capture"
    if "%%n"=="33" call :pub "Echo.Text"              "Applications\Tools\Productivity\Text\Echo.Text"            "EchoText.exe"              "Applications\Tools\Productivity\Text"
    if "%%n"=="34" call :pub "Mark.View"              "Applications\Tools\Productivity\Text\Mark.View"            "Mark.View.exe"             "Applications\Tools\Productivity\Text\Mark.View"
    if "%%n"=="35" call :pub "Text.Forge"             "Applications\Tools\Productivity\Text\Text.Forge"           "Text.Forge.exe"            "Applications\Tools\Productivity\Text"
    if "%%n"=="36" call :pub "Char.Art"               "Applications\Tools\Productivity\Visual\Char.Art"           "Char.Art.exe"              "Applications\Tools\Productivity\Visual"
    if "%%n"=="37" call :pub "Timeline.Craft"         "Applications\Tools\Productivity\Visual\Timeline.Craft"     "Timeline.Craft.exe"        "Applications\Tools\Productivity\Visual"
    if "%%n"=="38" call :pub "Word.Cloud"             "Applications\Tools\Productivity\Visual\Word.Cloud"         "Word.Cloud.exe"            "Applications\Tools\Productivity\Visual"
    if "%%n"=="39" call :pub "Clipboard.Stacker"      "Applications\Tools\Productivity\Utility\Clipboard.Stacker" "Clipboard.Stacker.exe"     "Applications\Tools\Productivity\Utility"
    if "%%n"=="40" call :pub "Dict.Cast"              "Applications\Tools\Productivity\Utility\Dict.Cast"         "Dict.Cast.exe"             "Applications\Tools\Productivity\Utility"
    if "%%n"=="41" call :pub "Mouse.Flick"            "Applications\Tools\Productivity\Utility\Mouse.Flick"       "Mouse.Flick.exe"           "Applications\Tools\Productivity\Utility"
    if "%%n"=="42" call :pub "Prompt.Forge"           "Applications\Tools\Productivity\Utility\Prompt.Forge"      "Prompt.Forge.exe"          "Applications\Tools\Productivity\Utility"
    if "%%n"=="43" call :pub "QR.Forge"               "Applications\Tools\Productivity\Utility\QR.Forge"          "QR.Forge.exe"              "Applications\Tools\Productivity\Utility"
    if "%%n"=="44" call :pub "Tag.Forge"              "Applications\Tools\Productivity\Media\Tag.Forge"            "Tag.Forge.exe"             "Applications\Tools\Productivity\Media"
    if "%%n"=="45" call :pub "Color.Grade"            "Applications\Tools\Productivity\Creative\Color.Grade"       "Color.Grade.exe"           "Applications\Tools\Productivity\Creative"
    if "%%n"=="46" call :pub "Env.Guard"              "Applications\Tools\System\Env.Guard"                       "Env.Guard.exe"             "Applications\Tools\System"
    if "%%n"=="47" call :pub "Sys.Clean"              "Applications\Tools\System\Sys.Clean"                       "Sys.Clean.exe"             "Applications\Tools\System\Sys.Clean"
    if "%%n"=="48" call :pub "Tray.Stats"             "Applications\Tools\System\Tray.Stats"                      "Tray.Stats.exe"            "Applications\Tools\System"
    if "%%n"=="49" call :pub "Mosaic.Forge"           "Applications\Media\Mosaic.Forge"                           "Mosaic.Forge.exe"          "Applications\Media"
    if "%%n"=="50" call :pub "Dungeon.Dash"           "Games\Action\Dungeon.Dash"                                 "Dungeon.Dash.exe"          "Games\Action"
    if "%%n"=="51" call :pub "Brick.Blitz"            "Games\Arcade\Brick.Blitz"                                  "Brick.Blitz.exe"           "Games\Arcade"
    if "%%n"=="52" call :pub "Dash.City"              "Games\Arcade\Dash.City"                                    "Dash.City.exe"             "Games\Arcade"
    if "%%n"=="53" call :pub "Neon.Run"               "Games\Arcade\Neon.Run"                                     "Neon.Run.exe"              "Games\Arcade"
    if "%%n"=="54" call :pub "Neon.Slice"             "Games\Arcade\Neon.Slice"                                   "Neon.Slice.exe"            "Games\Arcade\Neon.Slice"
    if "%%n"=="55" call :pub "Gravity.Flip"           "Games\Puzzle\Gravity.Flip"                                 "Gravity.Flip.exe"          "Games\Puzzle"
    if "%%n"=="56" call :pub "Hue.Flow"               "Games\Puzzle\Hue.Flow"                                     "Hue.Flow.exe"              "Games\Puzzle"
    if "%%n"=="57" call :pub "Orbit.Craft"            "Games\Puzzle\Orbit.Craft"                                  "Orbit.Craft.exe"           "Games\Puzzle\Orbit.Craft"
    if "%%n"=="58" call :pub "Nitro.Drift"            "Games\Racing\Nitro.Drift"                                  "Nitro.Drift.exe"           "Games\Racing"
    if "%%n"=="59" call :pub "Beat.Drop"              "Games\Rhythm\Beat.Drop"                                    "Beat.Drop.exe"             "Games\Rhythm"
    if "%%n"=="60" call :pub "Sand.Fall"              "Games\Sandbox\Sand.Fall"                                   "Sand.Fall.exe"             "Games\Sandbox"
    if "%%n"=="61" call :pub "Dodge.Blitz"            "Games\Shooter\Dodge.Blitz"                                 "Dodge.Blitz.exe"           "Games\Shooter"
    if "%%n"=="62" call :pub "Star.Strike"            "Games\Shooter\Star.Strike"                                 "Star.Strike.exe"           "Games\Shooter"
    if "%%n"=="63" call :pub "Tower.Guard"            "Games\Strategy\Tower.Guard"                                "Tower.Guard.exe"           "Games\Strategy"
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

call :pub "AI.Clip"               "Applications\AI\AI.Clip"                                   "Ai.Clip.exe"               "Applications\AI"
call :pub "Music.Player"          "Applications\Audio\Music.Player"                           "Music.Player.exe"          "Applications\Audio"
call :pub "Stay.Awake"            "Applications\Automation\Stay.Awake"                        "Stay.Awake.exe"            "Applications\Automation\Stay.Awake"
call :pub "Batch.Rename"          "Applications\Files\Manager\Batch.Rename"                   "Batch.Rename.exe"          "Applications\Files\Manager"
call :pub "File.Duplicates"       "Applications\Files\Manager\File.Duplicates"                "File.Duplicates.exe"       "Applications\Files\Manager"
call :pub "File.Unlocker"         "Applications\Files\Manager\File.Unlocker"                  "File.Unlocker.exe"         "Applications\Files\Manager"
call :pub "Folder.Purge"          "Applications\Files\Manager\Folder.Purge"                   "Folder.Purge.exe"          "Applications\Files\Manager"
call :pub "Disk.Lens"             "Applications\Files\Inspector\Disk.Lens"                    "Disk.Lens.exe"             "Applications\Files\Inspector\Disk.Lens"
call :pub "Hash.Check"            "Applications\Files\Inspector\Hash.Check"                   "HashCheck.exe"             "Applications\Files\Inspector"
call :pub "PDF.Forge"             "Applications\Files\Inspector\PDF.Forge"                    "PdfForge.exe"              "Applications\Files\Inspector"
call :pub "Zip.Peek"              "Applications\Files\Inspector\Zip.Peek"                     "ZipPeek.exe"               "Applications\Files\Inspector"
call :pub "Toast.Cast"            "Applications\Health\Toast.Cast"                            "Toast.Cast.exe"            "Applications\Health"
call :pub "Mosaic.Forge"          "Applications\Media\Mosaic.Forge"                           "Mosaic.Forge.exe"          "Applications\Media"
call :pub "Photo.Video.Organizer" "Applications\Media\Photo.Video.Organizer"                  "Photo.Video.Organizer.exe" "Applications\Media"
call :pub "Api.Probe"             "Applications\Tools\Dev\Network\Api.Probe"                  "Api.Probe.exe"             "Applications\Tools\Dev\Network\Api.Probe"
call :pub "Mock.Server"           "Applications\Tools\Dev\Network\Mock.Server"                "Mock.Server.exe"           "Applications\Tools\Dev\Network"
call :pub "Serve.Cast"            "Applications\Tools\Dev\Network\Serve.Cast"                 "Serve.Cast.exe"            "Applications\Tools\Dev\Network"
call :pub "Hex.Peek"              "Applications\Tools\Dev\Debug\Hex.Peek"                     "Hex.Peek.exe"              "Applications\Tools\Dev\Debug"
call :pub "Log.Lens"              "Applications\Tools\Dev\Debug\Log.Lens"                     "Log.Lens.exe"              "Applications\Tools\Dev\Debug"
call :pub "Signal.Flow"           "Applications\Tools\Dev\Debug\Signal.Flow"                  "Signal.Flow.exe"           "Applications\Tools\Dev\Debug"
call :pub "Glyph.Map"             "Applications\Tools\Dev\Assets\Glyph.Map"                   "Glyph.Map.exe"             "Applications\Tools\Dev\Assets\Glyph.Map"
call :pub "Icon.Hunt"             "Applications\Tools\Dev\Assets\Icon.Hunt"                   "IconHunt.exe"              "Applications\Tools\Dev\Assets"
call :pub "Key.Map"               "Applications\Tools\Dev\Assets\Key.Map"                     "Key.Map.exe"               "Applications\Tools\Dev\Assets"
call :pub "Locale.Forge"          "Applications\Tools\Dev\Assets\Locale.Forge"                "LocaleForge.exe"           "Applications\Tools\Dev\Assets"
call :pub "Boot.Map"              "Applications\Tools\Dev\Data\Boot.Map"                      "BootMap.exe"               "Applications\Tools\Dev\Data"
call :pub "Table.Craft"           "Applications\Tools\Dev\Data\Table.Craft"                   "Table.Craft.exe"           "Applications\Tools\Dev\Data"
call :pub "Layout.Forge"          "Applications\Tools\Dev\System\Layout.Forge"                "Layout.Forge.exe"          "Applications\Tools\Dev\System"
call :pub "Sched.Cast"            "Applications\Tools\Dev\System\Sched.Cast"                  "Sched.Cast.exe"            "Applications\Tools\Dev\System"
call :pub "DNS.Flip"              "Applications\Tools\Network\DNS.Flip"                       "Dns.Flip.exe"              "Applications\Tools\Network"
call :pub "Net.Scan"              "Applications\Tools\Network\Net.Scan"                       "NetScan.exe"               "Applications\Tools\Network"
call :pub "Port.Watch"            "Applications\Tools\Network\Port.Watch"                     "Port.Watch.exe"            "Applications\Tools\Network"
call :pub "Code.Snap"             "Applications\Tools\Productivity\Capture\Code.Snap"         "Code.Snap.exe"             "Applications\Tools\Productivity\Capture"
call :pub "Screen.Recorder"       "Applications\Tools\Productivity\Capture\Screen.Recorder"   "Screen.Recorder.exe"       "Applications\Tools\Productivity\Capture"
call :pub "Echo.Text"             "Applications\Tools\Productivity\Text\Echo.Text"            "EchoText.exe"              "Applications\Tools\Productivity\Text"
call :pub "Mark.View"             "Applications\Tools\Productivity\Text\Mark.View"            "Mark.View.exe"             "Applications\Tools\Productivity\Text\Mark.View"
call :pub "Text.Forge"            "Applications\Tools\Productivity\Text\Text.Forge"           "Text.Forge.exe"            "Applications\Tools\Productivity\Text"
call :pub "Char.Art"              "Applications\Tools\Productivity\Visual\Char.Art"           "Char.Art.exe"              "Applications\Tools\Productivity\Visual"
call :pub "Timeline.Craft"        "Applications\Tools\Productivity\Visual\Timeline.Craft"     "Timeline.Craft.exe"        "Applications\Tools\Productivity\Visual"
call :pub "Word.Cloud"            "Applications\Tools\Productivity\Visual\Word.Cloud"         "Word.Cloud.exe"            "Applications\Tools\Productivity\Visual"
call :pub "Clipboard.Stacker"     "Applications\Tools\Productivity\Utility\Clipboard.Stacker" "Clipboard.Stacker.exe"     "Applications\Tools\Productivity\Utility"
call :pub "Dict.Cast"             "Applications\Tools\Productivity\Utility\Dict.Cast"         "Dict.Cast.exe"             "Applications\Tools\Productivity\Utility"
call :pub "Mouse.Flick"           "Applications\Tools\Productivity\Utility\Mouse.Flick"       "Mouse.Flick.exe"           "Applications\Tools\Productivity\Utility"
call :pub "Prompt.Forge"          "Applications\Tools\Productivity\Utility\Prompt.Forge"      "Prompt.Forge.exe"          "Applications\Tools\Productivity\Utility"
call :pub "QR.Forge"              "Applications\Tools\Productivity\Utility\QR.Forge"          "QR.Forge.exe"              "Applications\Tools\Productivity\Utility"
call :pub "Tag.Forge"             "Applications\Tools\Productivity\Media\Tag.Forge"            "Tag.Forge.exe"             "Applications\Tools\Productivity\Media"
call :pub "Color.Grade"           "Applications\Tools\Productivity\Creative\Color.Grade"       "Color.Grade.exe"           "Applications\Tools\Productivity\Creative"
call :pub "Env.Guard"             "Applications\Tools\System\Env.Guard"                       "Env.Guard.exe"             "Applications\Tools\System"
call :pub "Sys.Clean"             "Applications\Tools\System\Sys.Clean"                       "Sys.Clean.exe"             "Applications\Tools\System\Sys.Clean"
call :pub "Tray.Stats"            "Applications\Tools\System\Tray.Stats"                      "Tray.Stats.exe"            "Applications\Tools\System"
call :pub "Dungeon.Dash"          "Games\Action\Dungeon.Dash"                                 "Dungeon.Dash.exe"          "Games\Action"
call :pub "Brick.Blitz"           "Games\Arcade\Brick.Blitz"                                  "Brick.Blitz.exe"           "Games\Arcade"
call :pub "Dash.City"             "Games\Arcade\Dash.City"                                    "Dash.City.exe"             "Games\Arcade"
call :pub "Neon.Run"              "Games\Arcade\Neon.Run"                                     "Neon.Run.exe"              "Games\Arcade"
call :pub "Neon.Slice"            "Games\Arcade\Neon.Slice"                                   "Neon.Slice.exe"            "Games\Arcade\Neon.Slice"
call :pub "Gravity.Flip"          "Games\Puzzle\Gravity.Flip"                                 "Gravity.Flip.exe"          "Games\Puzzle"
call :pub "Hue.Flow"              "Games\Puzzle\Hue.Flow"                                     "Hue.Flow.exe"              "Games\Puzzle"
call :pub "Orbit.Craft"           "Games\Puzzle\Orbit.Craft"                                  "Orbit.Craft.exe"           "Games\Puzzle\Orbit.Craft"
call :pub "Nitro.Drift"           "Games\Racing\Nitro.Drift"                                  "Nitro.Drift.exe"           "Games\Racing"
call :pub "Beat.Drop"             "Games\Rhythm\Beat.Drop"                                    "Beat.Drop.exe"             "Games\Rhythm"
call :pub "Sand.Fall"             "Games\Sandbox\Sand.Fall"                                   "Sand.Fall.exe"             "Games\Sandbox"
call :pub "Dodge.Blitz"           "Games\Shooter\Dodge.Blitz"                                 "Dodge.Blitz.exe"           "Games\Shooter"
call :pub "Star.Strike"           "Games\Shooter\Star.Strike"                                 "Star.Strike.exe"           "Games\Shooter"
call :pub "Tower.Guard"           "Games\Strategy\Tower.Guard"                                "Tower.Guard.exe"           "Games\Strategy"

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
