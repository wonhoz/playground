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
echo !CY!   2!RS! Api.Probe             !DG!Applications/Network!RS!
echo !CY!   3!RS! App.Temp              !DG!Applications/Development!RS!
echo !CY!   4!RS! Auto.Build            !DG!Games/Puzzle!RS!
echo !CY!   5!RS! Batch.Rename          !DG!Files/Manager!RS!
echo !CY!   6!RS! Beat.Drop             !DG!Games/Rhythm!RS!
echo !CY!   7!RS! Boot.Map              !DG!Applications/System!RS!
echo !CY!   8!RS! Brick.Blitz           !DG!Games/Arcade!RS!
echo !CY!   9!RS! Burn.Rate             !DG!Applications/System!RS!
echo !CY!  10!RS! Char.Art              !DG!Applications/Text!RS!
echo !CY!  11!RS! Chord.Strike          !DG!Games/Rhythm!RS!
echo !CY!  12!RS! Clipboard.Stacker     !DG!Applications/Tools.Utility!RS!
echo !CY!  13!RS! Color.Grade           !DG!Applications/Photo.Picture!RS!
echo !CY!  14!RS! Ctx.Menu              !DG!Applications/System!RS!
echo !CY!  15!RS! Dash.City             !DG!Games/Arcade!RS!
echo !CY!  16!RS! Dict.Cast             !DG!Applications/Tools.Utility!RS!
echo !CY!  17!RS! Disk.Lens             !DG!Files/Inspector!RS!
echo !CY!  18!RS! DNS.Flip              !DG!Applications/Network!RS!
echo !CY!  19!RS! Dodge.Blitz           !DG!Games/Shooter!RS!
echo !CY!  20!RS! Drive.Bench           !DG!Applications/System!RS!
echo !CY!  21!RS! Dungeon.Dash          !DG!Games/Action!RS!
echo !CY!  22!RS! Echo.Text             !DG!Applications/Text!RS!
echo !CY!  23!RS! Env.Guard             !DG!Applications/System!RS!
echo !CY!  24!RS! Ext.Boss              !DG!Applications/System!RS!
echo !CY!  25!RS! File.Duplicates       !DG!Files/Manager!RS!
echo !CY!  26!RS! File.Unlocker         !DG!Files/Manager!RS!
echo !CY!  27!RS! Folder.Purge          !DG!Files/Manager!RS!
echo !CY!  28!RS! Glyph.Map             !DG!Applications/Emoji.Icon!RS!
echo !CY!  29!RS! Gravity.Flip          !DG!Games/Puzzle!RS!
echo !CY!  30!RS! Hash.Check            !DG!Files/Inspector!RS!
echo !CY!  31!RS! Hex.Peek              !DG!Applications/Development!RS!
echo !CY!  32!RS! Hook.Cast             !DG!Games/Casual!RS!
echo !CY!  33!RS! Hue.Flow              !DG!Games/Puzzle!RS!
echo !CY!  34!RS! Icon.Hunt             !DG!Applications/Emoji.Icon!RS!
echo !CY!  35!RS! Key.Map               !DG!Applications/System!RS!
echo !CY!  36!RS! Leaf.Grow             !DG!Games/Simulation!RS!
echo !CY!  37!RS! Log.Lens              !DG!Applications/Development!RS!
echo !CY!  38!RS! Mark.View             !DG!Applications/Text!RS!
echo !CY!  39!RS! Mock.Server           !DG!Applications/Network!RS!
echo !CY!  40!RS! Mosaic.Forge          !DG!Applications/Photo.Picture!RS!
echo !CY!  41!RS! Mouse.Flick           !DG!Applications/Tools.Utility!RS!
echo !CY!  42!RS! Music.Player          !DG!Applications/Audio!RS!
echo !CY!  43!RS! Neon.Run              !DG!Games/Arcade!RS!
echo !CY!  44!RS! Neon.Slice            !DG!Games/Arcade!RS!
echo !CY!  45!RS! Net.Scan              !DG!Applications/Network!RS!
echo !CY!  46!RS! Nitro.Drift           !DG!Games/Racing!RS!
echo !CY!  47!RS! Orbit.Craft           !DG!Games/Puzzle!RS!
echo !CY!  48!RS! PDF.Forge             !DG!Files/Inspector!RS!
echo !CY!  49!RS! Photo.Video.Organizer !DG!Applications/Photo.Picture!RS!
echo !CY!  50!RS! Port.Watch            !DG!Applications/Network!RS!
echo !CY!  51!RS! Prompt.Forge          !DG!Applications/AI!RS!
echo !CY!  52!RS! QR.Forge              !DG!Applications/Tools.Utility!RS!
echo !CY!  53!RS! Quick.Calc            !DG!Applications/Development!RS!
echo !CY!  54!RS! Sand.Fall             !DG!Games/Sandbox!RS!
echo !CY!  55!RS! Sched.Cast            !DG!Applications/System!RS!
echo !CY!  56!RS! Screen.Recorder       !DG!Applications/Video!RS!
echo !CY!  57!RS! Serve.Cast            !DG!Applications/Network!RS!
echo !CY!  58!RS! Signal.Flow           !DG!Applications/Development!RS!
echo !CY!  59!RS! Spec.Report           !DG!Applications/System!RS!
echo !CY!  60!RS! Spec.View             !DG!Applications/System!RS!
echo !CY!  61!RS! Star.Strike           !DG!Games/Shooter!RS!
echo !CY!  62!RS! Stay.Awake            !DG!Applications/Automation!RS!
echo !CY!  63!RS! Sys.Clean             !DG!Applications/System!RS!
echo !CY!  64!RS! Table.Craft           !DG!Applications/Data!RS!
echo !CY!  65!RS! Tag.Forge             !DG!Applications/Audio!RS!
echo !CY!  66!RS! Text.Forge            !DG!Applications/Text!RS!
echo !CY!  67!RS! Timeline.Craft        !DG!Applications/Data!RS!
echo !CY!  68!RS! Tower.Guard           !DG!Games/Strategy!RS!
echo !CY!  69!RS! Tray.Stats            !DG!Applications/System!RS!
echo !CY!  70!RS! Wave.Surf             !DG!Games/Casual!RS!
echo !CY!  71!RS! Web.Shot              !DG!Applications/Photo.Picture!RS!
echo !CY!  72!RS! Win.Event             !DG!Applications/Development!RS!
echo !CY!  73!RS! Word.Cloud            !DG!Applications/Text!RS!
echo !CY!  74!RS! Zip.Peek              !DG!Files/Inspector!RS!
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
    if "%%n"=="1"  call :pub "AI.Clip"               "Applications\AI\AI.Clip"                                "Ai.Clip.exe"               "Applications\AI"
    if "%%n"=="2"  call :pub "Api.Probe"              "Applications\Network\Api.Probe"                         "Api.Probe.exe"             "Applications\Network\Api.Probe"
    if "%%n"=="3"  call :pub "App.Temp"               "Applications\Development\App.Temp"                      "App.Temp.exe"              "Applications\Development"
    if "%%n"=="4"  call :pub "Auto.Build"             "Games\Puzzle\Auto.Build"                                "AutoBuild.exe"             "Games\Puzzle"
    if "%%n"=="5"  call :pub "Batch.Rename"           "Applications\Files\Manager\Batch.Rename"                "Batch.Rename.exe"          "Applications\Files\Manager"
    if "%%n"=="6"  call :pub "Beat.Drop"              "Games\Rhythm\Beat.Drop"                                 "Beat.Drop.exe"             "Games\Rhythm"
    if "%%n"=="7"  call :pub "Boot.Map"               "Applications\System\Boot.Map"                           "Boot.Map.exe"              "Applications\System"
    if "%%n"=="8"  call :pub "Brick.Blitz"            "Games\Arcade\Brick.Blitz"                               "Brick.Blitz.exe"           "Games\Arcade"
    if "%%n"=="9"  call :pub "Burn.Rate"              "Applications\System\Burn.Rate"                          "Burn.Rate.exe"             "Applications\System"
    if "%%n"=="10" call :pub "Char.Art"               "Applications\Text\Char.Art"                             "Char.Art.exe"              "Applications\Text"
    if "%%n"=="11" call :pub "Chord.Strike"           "Games\Rhythm\Chord.Strike"                              "ChordStrike.exe"           "Games\Rhythm"
    if "%%n"=="12" call :pub "Clipboard.Stacker"      "Applications\Tools.Utility\Clipboard.Stacker"           "Clipboard.Stacker.exe"     "Applications\Tools.Utility"
    if "%%n"=="13" call :pub "Color.Grade"            "Applications\Photo.Picture\Color.Grade"                 "Color.Grade.exe"           "Applications\Photo.Picture\Color.Grade"
    if "%%n"=="14" call :pub "Ctx.Menu"               "Applications\System\Ctx.Menu"                           "Ctx.Menu.exe"              "Applications\System\Ctx.Menu"
    if "%%n"=="15" call :pub "Dash.City"              "Games\Arcade\Dash.City"                                 "Dash.City.exe"             "Games\Arcade"
    if "%%n"=="16" call :pub "Dict.Cast"              "Applications\Tools.Utility\Dict.Cast"                   "Dict.Cast.exe"             "Applications\Tools.Utility\Dict.Cast"
    if "%%n"=="17" call :pub "Disk.Lens"              "Applications\Files\Inspector\Disk.Lens"                 "Disk.Lens.exe"             "Applications\Files\Inspector\Disk.Lens"
    if "%%n"=="18" call :pub "DNS.Flip"               "Applications\Network\DNS.Flip"                          "Dns.Flip.exe"              "Applications\Network"
    if "%%n"=="19" call :pub "Dodge.Blitz"            "Games\Shooter\Dodge.Blitz"                              "Dodge.Blitz.exe"           "Games\Shooter"
    if "%%n"=="20" call :pub "Drive.Bench"            "Applications\System\Drive.Bench"                        "Drive.Bench.exe"           "Applications\System"
    if "%%n"=="21" call :pub "Dungeon.Dash"           "Games\Action\Dungeon.Dash"                              "Dungeon.Dash.exe"          "Games\Action"
    if "%%n"=="22" call :pub "Echo.Text"              "Applications\Text\Echo.Text"                            "Echo.Text.exe"             "Applications\Text\Echo.Text"
    if "%%n"=="23" call :pub "Env.Guard"              "Applications\System\Env.Guard"                          "Env.Guard.exe"             "Applications\System"
    if "%%n"=="24" call :pub "Ext.Boss"               "Applications\System\Ext.Boss"                           "Ext.Boss.exe"              "Applications\System\Ext.Boss"
    if "%%n"=="25" call :pub "File.Duplicates"        "Applications\Files\Manager\File.Duplicates"             "File.Duplicates.exe"       "Applications\Files\Manager"
    if "%%n"=="26" call :pub "File.Unlocker"          "Applications\Files\Manager\File.Unlocker"               "File.Unlocker.exe"         "Applications\Files\Manager"
    if "%%n"=="27" call :pub "Folder.Purge"           "Applications\Files\Manager\Folder.Purge"                "Folder.Purge.exe"          "Applications\Files\Manager"
    if "%%n"=="28" call :pub "Glyph.Map"              "Applications\Emoji.Icon\Glyph.Map"                      "Glyph.Map.exe"             "Applications\Emoji.Icon\Glyph.Map"
    if "%%n"=="29" call :pub "Gravity.Flip"           "Games\Puzzle\Gravity.Flip"                              "Gravity.Flip.exe"          "Games\Puzzle"
    if "%%n"=="30" call :pub "Hash.Check"             "Applications\Files\Inspector\Hash.Check"                "Hash.Check.exe"            "Applications\Files\Inspector\Hash.Check"
    if "%%n"=="31" call :pub "Hex.Peek"               "Applications\Development\Hex.Peek"                      "Hex.Peek.exe"              "Applications\Development"
    if "%%n"=="32" call :pub "Hook.Cast"              "Games\Casual\Hook.Cast"                                 "Hook.Cast.exe"             "Games\Casual"
    if "%%n"=="33" call :pub "Hue.Flow"               "Games\Puzzle\Hue.Flow"                                  "Hue.Flow.exe"              "Games\Puzzle"
    if "%%n"=="34" call :pub "Icon.Hunt"              "Applications\Emoji.Icon\Icon.Hunt"                      "Icon.Hunt.exe"             "Applications\Emoji.Icon"
    if "%%n"=="35" call :pub "Key.Map"                "Applications\System\Key.Map"                            "Key.Map.exe"               "Applications\System\Key.Map"
    if "%%n"=="36" call :pub "Leaf.Grow"              "Games\Simulation\Leaf.Grow"                             "Leaf.Grow.exe"             "Games\Simulation"
    if "%%n"=="37" call :pub "Log.Lens"               "Applications\Development\Log.Lens"                      "Log.Lens.exe"              "Applications\Development"
    if "%%n"=="38" call :pub "Mark.View"              "Applications\Text\Mark.View"                            "Mark.View.exe"             "Applications\Text\Mark.View"
    if "%%n"=="39" call :pub "Mock.Server"            "Applications\Network\Mock.Server"                       "Mock.Server.exe"           "Applications\Network"
    if "%%n"=="40" call :pub "Mosaic.Forge"           "Applications\Photo.Picture\Mosaic.Forge"                "Mosaic.Forge.exe"          "Applications\Photo.Picture\Mosaic.Forge"
    if "%%n"=="41" call :pub "Mouse.Flick"            "Applications\Tools.Utility\Mouse.Flick"                 "Mouse.Flick.exe"           "Applications\Tools.Utility"
    if "%%n"=="42" call :pub "Music.Player"           "Applications\Audio\Music.Player"                        "Music.Player.exe"          "Applications\Audio"
    if "%%n"=="43" call :pub "Neon.Run"               "Games\Arcade\Neon.Run"                                  "Neon.Run.exe"              "Games\Arcade"
    if "%%n"=="44" call :pub "Neon.Slice"             "Games\Arcade\Neon.Slice"                                "Neon.Slice.exe"            "Games\Arcade\Neon.Slice"
    if "%%n"=="45" call :pub "Net.Scan"               "Applications\Network\Net.Scan"                          "Net.Scan.exe"              "Applications\Network\Net.Scan"
    if "%%n"=="46" call :pub "Nitro.Drift"            "Games\Racing\Nitro.Drift"                               "Nitro.Drift.exe"           "Games\Racing"
    if "%%n"=="47" call :pub "Orbit.Craft"            "Games\Puzzle\Orbit.Craft"                               "Orbit.Craft.exe"           "Games\Puzzle\Orbit.Craft"
    if "%%n"=="48" call :pub "PDF.Forge"              "Applications\Files\Inspector\PDF.Forge"                 "Pdf.Forge.exe"             "Applications\Files\Inspector\Pdf.Forge"
    if "%%n"=="49" call :pub "Photo.Video.Organizer"  "Applications\Photo.Picture\Photo.Video.Organizer"       "Photo.Video.Organizer.exe" "Applications\Photo.Picture"
    if "%%n"=="50" call :pub "Port.Watch"             "Applications\Network\Port.Watch"                        "Port.Watch.exe"            "Applications\Network"
    if "%%n"=="51" call :pub "Prompt.Forge"           "Applications\AI\Prompt.Forge"                           "Prompt.Forge.exe"          "Applications\AI\Prompt.Forge"
    if "%%n"=="52" call :pub "QR.Forge"               "Applications\Tools.Utility\QR.Forge"                    "QR.Forge.exe"              "Applications\Tools.Utility"
    if "%%n"=="53" call :pub "Quick.Calc"             "Applications\Development\Quick.Calc"                    "Quick.Calc.exe"            "Applications\Development"
    if "%%n"=="54" call :pub "Sand.Fall"              "Games\Sandbox\Sand.Fall"                                "Sand.Fall.exe"             "Games\Sandbox"
    if "%%n"=="55" call :pub "Sched.Cast"             "Applications\System\Sched.Cast"                         "Sched.Cast.exe"            "Applications\System\Sched.Cast"
    if "%%n"=="56" call :pub "Screen.Recorder"        "Applications\Video\Screen.Recorder"                     "Screen.Recorder.exe"       "Applications\Video"
    if "%%n"=="57" call :pub "Serve.Cast"             "Applications\Network\Serve.Cast"                        "Serve.Cast.exe"            "Applications\Network"
    if "%%n"=="58" call :pub "Signal.Flow"            "Applications\Development\Signal.Flow"                   "Signal.Flow.exe"           "Applications\Development"
    if "%%n"=="59" call :pub "Spec.Report"            "Applications\System\Spec.Report"                        "Spec.Report.exe"           "Applications\System"
    if "%%n"=="60" call :pub "Spec.View"              "Applications\System\Spec.View"                          "Spec.View.exe"             "Applications\System\Spec.View"
    if "%%n"=="61" call :pub "Star.Strike"            "Games\Shooter\Star.Strike"                              "Star.Strike.exe"           "Games\Shooter"
    if "%%n"=="62" call :pub "Stay.Awake"             "Applications\Automation\Stay.Awake"                     "Stay.Awake.exe"            "Applications\Automation\Stay.Awake"
    if "%%n"=="63" call :pub "Sys.Clean"              "Applications\System\Sys.Clean"                          "Sys.Clean.exe"             "Applications\System\Sys.Clean"
    if "%%n"=="64" call :pub "Table.Craft"            "Applications\Data\Table.Craft"                          "Table.Craft.exe"           "Applications\Data"
    if "%%n"=="65" call :pub "Tag.Forge"              "Applications\Audio\Tag.Forge"                           "Tag.Forge.exe"             "Applications\Audio\Tag.Forge"
    if "%%n"=="66" call :pub "Text.Forge"             "Applications\Text\Text.Forge"                           "Text.Forge.exe"            "Applications\Text"
    if "%%n"=="67" call :pub "Timeline.Craft"         "Applications\Data\Timeline.Craft"                       "Timeline.Craft.exe"        "Applications\Data\Timeline.Craft"
    if "%%n"=="68" call :pub "Tower.Guard"            "Games\Strategy\Tower.Guard"                             "Tower.Guard.exe"           "Games\Strategy"
    if "%%n"=="69" call :pub "Tray.Stats"             "Applications\System\Tray.Stats"                         "Tray.Stats.exe"            "Applications\System\Tray.Stats"
    if "%%n"=="70" call :pub "Wave.Surf"              "Games\Casual\Wave.Surf"                                 "Wave.Surf.exe"             "Games\Casual"
    if "%%n"=="71" call :pub "Web.Shot"               "Applications\Photo.Picture\Web.Shot"                    "Web.Shot.exe"              "Applications\Photo.Picture\Web.Shot"
    if "%%n"=="72" call :pub "Win.Event"              "Applications\Development\Win.Event"                     "WinEvent.exe"              "Applications\Development"
    if "%%n"=="73" call :pub "Word.Cloud"             "Applications\Text\Word.Cloud"                           "Word.Cloud.exe"            "Applications\Text"
    if "%%n"=="74" call :pub "Zip.Peek"               "Applications\Files\Inspector\Zip.Peek"                  "Zip.Peek.exe"              "Applications\Files\Inspector\Zip.Peek"
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

call :pub "AI.Clip"               "Applications\AI\AI.Clip"                                "Ai.Clip.exe"               "Applications\AI"
call :pub "Api.Probe"             "Applications\Network\Api.Probe"                         "Api.Probe.exe"             "Applications\Network\Api.Probe"
call :pub "App.Temp"              "Applications\Development\App.Temp"                      "App.Temp.exe"              "Applications\Development"
call :pub "Auto.Build"            "Games\Puzzle\Auto.Build"                                "AutoBuild.exe"             "Games\Puzzle"
call :pub "Batch.Rename"          "Applications\Files\Manager\Batch.Rename"                "Batch.Rename.exe"          "Applications\Files\Manager"
call :pub "Beat.Drop"             "Games\Rhythm\Beat.Drop"                                 "Beat.Drop.exe"             "Games\Rhythm"
call :pub "Boot.Map"              "Applications\System\Boot.Map"                           "Boot.Map.exe"              "Applications\System"
call :pub "Brick.Blitz"           "Games\Arcade\Brick.Blitz"                               "Brick.Blitz.exe"           "Games\Arcade"
call :pub "Burn.Rate"             "Applications\System\Burn.Rate"                          "Burn.Rate.exe"             "Applications\System"
call :pub "Char.Art"              "Applications\Text\Char.Art"                             "Char.Art.exe"              "Applications\Text"
call :pub "Chord.Strike"          "Games\Rhythm\Chord.Strike"                              "ChordStrike.exe"           "Games\Rhythm"
call :pub "Clipboard.Stacker"     "Applications\Tools.Utility\Clipboard.Stacker"           "Clipboard.Stacker.exe"     "Applications\Tools.Utility"
call :pub "Color.Grade"           "Applications\Photo.Picture\Color.Grade"                 "Color.Grade.exe"           "Applications\Photo.Picture\Color.Grade"
call :pub "Ctx.Menu"              "Applications\System\Ctx.Menu"                           "Ctx.Menu.exe"              "Applications\System\Ctx.Menu"
call :pub "Dash.City"             "Games\Arcade\Dash.City"                                 "Dash.City.exe"             "Games\Arcade"
call :pub "Dict.Cast"             "Applications\Tools.Utility\Dict.Cast"                   "Dict.Cast.exe"             "Applications\Tools.Utility\Dict.Cast"
call :pub "Disk.Lens"             "Applications\Files\Inspector\Disk.Lens"                 "Disk.Lens.exe"             "Applications\Files\Inspector\Disk.Lens"
call :pub "DNS.Flip"              "Applications\Network\DNS.Flip"                          "Dns.Flip.exe"              "Applications\Network"
call :pub "Dodge.Blitz"           "Games\Shooter\Dodge.Blitz"                              "Dodge.Blitz.exe"           "Games\Shooter"
call :pub "Drive.Bench"           "Applications\System\Drive.Bench"                        "Drive.Bench.exe"           "Applications\System"
call :pub "Dungeon.Dash"          "Games\Action\Dungeon.Dash"                              "Dungeon.Dash.exe"          "Games\Action"
call :pub "Echo.Text"             "Applications\Text\Echo.Text"                            "Echo.Text.exe"             "Applications\Text\Echo.Text"
call :pub "Env.Guard"             "Applications\System\Env.Guard"                          "Env.Guard.exe"             "Applications\System"
call :pub "Ext.Boss"              "Applications\System\Ext.Boss"                           "Ext.Boss.exe"              "Applications\System\Ext.Boss"
call :pub "File.Duplicates"       "Applications\Files\Manager\File.Duplicates"             "File.Duplicates.exe"       "Applications\Files\Manager"
call :pub "File.Unlocker"         "Applications\Files\Manager\File.Unlocker"               "File.Unlocker.exe"         "Applications\Files\Manager"
call :pub "Folder.Purge"          "Applications\Files\Manager\Folder.Purge"                "Folder.Purge.exe"          "Applications\Files\Manager"
call :pub "Glyph.Map"             "Applications\Emoji.Icon\Glyph.Map"                      "Glyph.Map.exe"             "Applications\Emoji.Icon\Glyph.Map"
call :pub "Gravity.Flip"          "Games\Puzzle\Gravity.Flip"                              "Gravity.Flip.exe"          "Games\Puzzle"
call :pub "Hash.Check"            "Applications\Files\Inspector\Hash.Check"                "Hash.Check.exe"            "Applications\Files\Inspector\Hash.Check"
call :pub "Hex.Peek"              "Applications\Development\Hex.Peek"                      "Hex.Peek.exe"              "Applications\Development"
call :pub "Hook.Cast"             "Games\Casual\Hook.Cast"                                 "Hook.Cast.exe"             "Games\Casual"
call :pub "Hue.Flow"              "Games\Puzzle\Hue.Flow"                                  "Hue.Flow.exe"              "Games\Puzzle"
call :pub "Icon.Hunt"             "Applications\Emoji.Icon\Icon.Hunt"                      "Icon.Hunt.exe"             "Applications\Emoji.Icon"
call :pub "Key.Map"               "Applications\System\Key.Map"                            "Key.Map.exe"               "Applications\System\Key.Map"
call :pub "Leaf.Grow"             "Games\Simulation\Leaf.Grow"                             "Leaf.Grow.exe"             "Games\Simulation"
call :pub "Log.Lens"              "Applications\Development\Log.Lens"                      "Log.Lens.exe"              "Applications\Development"
call :pub "Mark.View"             "Applications\Text\Mark.View"                            "Mark.View.exe"             "Applications\Text\Mark.View"
call :pub "Mock.Server"           "Applications\Network\Mock.Server"                       "Mock.Server.exe"           "Applications\Network"
call :pub "Mosaic.Forge"          "Applications\Photo.Picture\Mosaic.Forge"                "Mosaic.Forge.exe"          "Applications\Photo.Picture\Mosaic.Forge"
call :pub "Mouse.Flick"           "Applications\Tools.Utility\Mouse.Flick"                 "Mouse.Flick.exe"           "Applications\Tools.Utility"
call :pub "Music.Player"          "Applications\Audio\Music.Player"                        "Music.Player.exe"          "Applications\Audio"
call :pub "Neon.Run"              "Games\Arcade\Neon.Run"                                  "Neon.Run.exe"              "Games\Arcade"
call :pub "Neon.Slice"            "Games\Arcade\Neon.Slice"                                "Neon.Slice.exe"            "Games\Arcade\Neon.Slice"
call :pub "Net.Scan"              "Applications\Network\Net.Scan"                          "Net.Scan.exe"              "Applications\Network\Net.Scan"
call :pub "Nitro.Drift"           "Games\Racing\Nitro.Drift"                               "Nitro.Drift.exe"           "Games\Racing"
call :pub "Orbit.Craft"           "Games\Puzzle\Orbit.Craft"                               "Orbit.Craft.exe"           "Games\Puzzle\Orbit.Craft"
call :pub "PDF.Forge"             "Applications\Files\Inspector\PDF.Forge"                 "Pdf.Forge.exe"             "Applications\Files\Inspector\Pdf.Forge"
call :pub "Photo.Video.Organizer" "Applications\Photo.Picture\Photo.Video.Organizer"       "Photo.Video.Organizer.exe" "Applications\Photo.Picture"
call :pub "Port.Watch"            "Applications\Network\Port.Watch"                        "Port.Watch.exe"            "Applications\Network"
call :pub "Prompt.Forge"          "Applications\AI\Prompt.Forge"                           "Prompt.Forge.exe"          "Applications\AI\Prompt.Forge"
call :pub "QR.Forge"              "Applications\Tools.Utility\QR.Forge"                    "QR.Forge.exe"              "Applications\Tools.Utility"
call :pub "Quick.Calc"            "Applications\Development\Quick.Calc"                    "Quick.Calc.exe"            "Applications\Development"
call :pub "Sand.Fall"             "Games\Sandbox\Sand.Fall"                                "Sand.Fall.exe"             "Games\Sandbox"
call :pub "Sched.Cast"            "Applications\System\Sched.Cast"                         "Sched.Cast.exe"            "Applications\System\Sched.Cast"
call :pub "Screen.Recorder"       "Applications\Video\Screen.Recorder"                     "Screen.Recorder.exe"       "Applications\Video"
call :pub "Serve.Cast"            "Applications\Network\Serve.Cast"                        "Serve.Cast.exe"            "Applications\Network"
call :pub "Signal.Flow"           "Applications\Development\Signal.Flow"                   "Signal.Flow.exe"           "Applications\Development"
call :pub "Spec.Report"           "Applications\System\Spec.Report"                        "Spec.Report.exe"           "Applications\System"
call :pub "Spec.View"             "Applications\System\Spec.View"                          "Spec.View.exe"             "Applications\System\Spec.View"
call :pub "Star.Strike"           "Games\Shooter\Star.Strike"                              "Star.Strike.exe"           "Games\Shooter"
call :pub "Stay.Awake"            "Applications\Automation\Stay.Awake"                     "Stay.Awake.exe"            "Applications\Automation\Stay.Awake"
call :pub "Sys.Clean"             "Applications\System\Sys.Clean"                          "Sys.Clean.exe"             "Applications\System\Sys.Clean"
call :pub "Table.Craft"           "Applications\Data\Table.Craft"                          "Table.Craft.exe"           "Applications\Data"
call :pub "Tag.Forge"             "Applications\Audio\Tag.Forge"                           "Tag.Forge.exe"             "Applications\Audio\Tag.Forge"
call :pub "Text.Forge"            "Applications\Text\Text.Forge"                           "Text.Forge.exe"            "Applications\Text"
call :pub "Timeline.Craft"        "Applications\Data\Timeline.Craft"                       "Timeline.Craft.exe"        "Applications\Data\Timeline.Craft"
call :pub "Tower.Guard"           "Games\Strategy\Tower.Guard"                             "Tower.Guard.exe"           "Games\Strategy"
call :pub "Tray.Stats"            "Applications\System\Tray.Stats"                         "Tray.Stats.exe"            "Applications\System\Tray.Stats"
call :pub "Wave.Surf"             "Games\Casual\Wave.Surf"                                 "Wave.Surf.exe"             "Games\Casual"
call :pub "Web.Shot"              "Applications\Photo.Picture\Web.Shot"                    "Web.Shot.exe"              "Applications\Photo.Picture\Web.Shot"
call :pub "Win.Event"             "Applications\Development\Win.Event"                     "WinEvent.exe"              "Applications\Development"
call :pub "Word.Cloud"            "Applications\Text\Word.Cloud"                           "Word.Cloud.exe"            "Applications\Text"
call :pub "Zip.Peek"              "Applications\Files\Inspector\Zip.Peek"                  "Zip.Peek.exe"              "Applications\Files\Inspector\Zip.Peek"

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
