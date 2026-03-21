param([string]$OutFile)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

try {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class WinDwm {
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
public static class WinUx {
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    public static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);
}
"@ -ErrorAction SilentlyContinue
} catch {}

$allApps = @(
    [pscustomobject]@{N=1;   Name="AI.Clip";               Cat="Applications/AI"}
    [pscustomobject]@{N=2;   Name="ANSI.Forge";            Cat="Applications/Text"}
    [pscustomobject]@{N=3;   Name="Api.Probe";             Cat="Applications/Network/Server"}
    [pscustomobject]@{N=4;   Name="App.Temp";              Cat="Applications/Development/Inspector"}
    [pscustomobject]@{N=5;   Name="Badge.Forge";           Cat="Applications/Tools.Utility"}
    [pscustomobject]@{N=6;   Name="Batch.Rename";          Cat="Applications/Files/Manager"}
    [pscustomobject]@{N=7;   Name="Beat.Drop";             Cat="Games/Rhythm"}
    [pscustomobject]@{N=8;   Name="Boot.Map";              Cat="Applications/System/Monitor"}
    [pscustomobject]@{N=9;   Name="Brick.Blitz";           Cat="Games/Arcade"}
    [pscustomobject]@{N=10;  Name="Bug.Hunt";              Cat="Games/Puzzle"}
    [pscustomobject]@{N=11;  Name="Burn.Rate";             Cat="Applications/System/Monitor"}
    [pscustomobject]@{N=12;  Name="Char.Art";              Cat="Applications/Text"}
    [pscustomobject]@{N=13;  Name="Cipher.Quest";          Cat="Games/Puzzle"}
    [pscustomobject]@{N=14;  Name="Circuit.Break";         Cat="Games/Puzzle"}
    [pscustomobject]@{N=15;  Name="Clipboard.Stacker";     Cat="Applications/Tools.Utility"}
    [pscustomobject]@{N=16;  Name="Code.Idle";             Cat="Games/Idle"}
    [pscustomobject]@{N=17;  Name="Color.Grade";           Cat="Applications/Photo.Picture"}
    [pscustomobject]@{N=18;  Name="Comic.Cast";            Cat="Applications/Photo.Picture"}
    [pscustomobject]@{N=19;  Name="Crossword.Cast";        Cat="Games/Puzzle"}
    [pscustomobject]@{N=20;  Name="Ctx.Menu";              Cat="Applications/System/Manager"}
    [pscustomobject]@{N=21;  Name="Dash.City";             Cat="Games/Arcade"}
    [pscustomobject]@{N=22;  Name="Data.Map";              Cat="Applications/Data"}
    [pscustomobject]@{N=23;  Name="Dep.Graph";             Cat="Applications/Development/Analyzer"}
    [pscustomobject]@{N=24;  Name="Dict.Cast";             Cat="Applications/Tools.Utility"}
    [pscustomobject]@{N=25;  Name="Disk.Lens";             Cat="Applications/Files/Inspector"}
    [pscustomobject]@{N=26;  Name="DNS.Flip";              Cat="Applications/Network/Monitor"}
    [pscustomobject]@{N=27;  Name="Dodge.Blitz";           Cat="Games/Shooter"}
    [pscustomobject]@{N=28;  Name="Dodge.Craft";           Cat="Games/Shooter"}
    [pscustomobject]@{N=29;  Name="Drive.Bench";           Cat="Applications/System/Monitor"}
    [pscustomobject]@{N=30;  Name="Dungeon.Dash";          Cat="Games/Action"}
    [pscustomobject]@{N=31;  Name="Ear.Train";             Cat="Games/Casual"}
    [pscustomobject]@{N=32;  Name="Echo.Text";             Cat="Applications/Text"}
    [pscustomobject]@{N=33;  Name="Env.Guard";             Cat="Applications/System/Manager"}
    [pscustomobject]@{N=34;  Name="Escape.Key";            Cat="Games/Puzzle"}
    [pscustomobject]@{N=35;  Name="Ext.Boss";              Cat="Applications/System/Manager"}
    [pscustomobject]@{N=36;  Name="File.Duplicates";       Cat="Applications/Files/Manager"}
    [pscustomobject]@{N=37;  Name="File.Unlocker";         Cat="Applications/Files/Manager"}
    [pscustomobject]@{N=38;  Name="Folder.Purge";          Cat="Applications/Files/Manager"}
    [pscustomobject]@{N=39;  Name="Geo.Quiz";              Cat="Games/Casual"}
    [pscustomobject]@{N=40;  Name="Git.Stats";             Cat="Applications/Development/Analyzer"}
    [pscustomobject]@{N=41;  Name="Glyph.Map";             Cat="Applications/Emoji.Icon"}
    [pscustomobject]@{N=42;  Name="Golf.Cast";             Cat="Games/Sports"}
    [pscustomobject]@{N=43;  Name="Gravity.Flip";          Cat="Games/Puzzle"}
    [pscustomobject]@{N=44;  Name="Hash.Check";            Cat="Applications/Files/Inspector"}
    [pscustomobject]@{N=45;  Name="Hex.Peek";              Cat="Applications/Development/Inspector"}
    [pscustomobject]@{N=46;  Name="Hotkey.Map";            Cat="Applications/System/Key"}
    [pscustomobject]@{N=47;  Name="Hue.Flow";              Cat="Games/Puzzle"}
    [pscustomobject]@{N=48;  Name="Icon.Hunt";             Cat="Applications/Emoji.Icon"}
    [pscustomobject]@{N=49;  Name="Icon.Maker";            Cat="Applications/Tools.Utility"}
    [pscustomobject]@{N=50;  Name="Img.Cast";              Cat="Applications/Emoji.Icon"}
    [pscustomobject]@{N=51;  Name="Img.Compare";           Cat="Applications/Photo.Picture"}
    [pscustomobject]@{N=52;  Name="JSON.Fmt";              Cat="Applications/Tools.Utility"}
    [pscustomobject]@{N=53;  Name="JSON.Tree";             Cat="Applications/Development/Inspector"}
    [pscustomobject]@{N=54;  Name="Key.Map";               Cat="Applications/System/Key"}
    [pscustomobject]@{N=55;  Name="Key.Test";              Cat="Applications/System/Key"}
    [pscustomobject]@{N=56;  Name="Leaf.Grow";             Cat="Games/Simulation"}
    [pscustomobject]@{N=57;  Name="Locale.View";           Cat="Applications/Development/Inspector"}
    [pscustomobject]@{N=58;  Name="Log.Lens";              Cat="Applications/Development/Analyzer"}
    [pscustomobject]@{N=59;  Name="Log.Merge";             Cat="Applications/Development/Analyzer"}
    [pscustomobject]@{N=60;  Name="Manga.View";            Cat="Applications/Files/Inspector"}
    [pscustomobject]@{N=61;  Name="Mark.View";             Cat="Applications/Text"}
    [pscustomobject]@{N=62;  Name="Mem.Lens";              Cat="Applications/System/Monitor"}
    [pscustomobject]@{N=63;  Name="Mock.Server";           Cat="Applications/Network/Server"}
    [pscustomobject]@{N=64;  Name="Morse.Run";             Cat="Games/Casual"}
    [pscustomobject]@{N=65;  Name="Mosaic.Forge";          Cat="Applications/Photo.Picture"}
    [pscustomobject]@{N=66;  Name="Mouse.Flick";           Cat="Applications/Tools.Utility"}
    [pscustomobject]@{N=67;  Name="Music.Player";          Cat="Applications/Audio"}
    [pscustomobject]@{N=68;  Name="Neon.Run";              Cat="Games/Arcade"}
    [pscustomobject]@{N=69;  Name="Neon.Slice";            Cat="Games/Arcade"}
    [pscustomobject]@{N=70;  Name="Net.Scan";              Cat="Applications/Network/Monitor"}
    [pscustomobject]@{N=71;  Name="Nitro.Drift";           Cat="Games/Racing"}
    [pscustomobject]@{N=72;  Name="Orbit.Craft";           Cat="Games/Puzzle"}
    [pscustomobject]@{N=73;  Name="Orbit.Raid";            Cat="Games/Puzzle"}
    [pscustomobject]@{N=74;  Name="Pad.Forge";             Cat="Applications/System/Manager"}
    [pscustomobject]@{N=75;  Name="Pane.Cast";             Cat="Applications/Automation"}
    [pscustomobject]@{N=76;  Name="Path.Guard";            Cat="Applications/System/Manager"}
    [pscustomobject]@{N=77;  Name="PDF.Forge";             Cat="Applications/Files/Inspector"}
    [pscustomobject]@{N=78;  Name="Persp.Shift";           Cat="Games/Puzzle"}
    [pscustomobject]@{N=79;  Name="Photo.Video.Organizer"; Cat="Applications/Photo.Picture"}
    [pscustomobject]@{N=80;  Name="Port.Watch";            Cat="Applications/Network/Monitor"}
    [pscustomobject]@{N=81;  Name="Proc.Bench";            Cat="Applications/System/Monitor"}
    [pscustomobject]@{N=82;  Name="Prompt.Forge";          Cat="Applications/AI"}
    [pscustomobject]@{N=83;  Name="QR.Forge";              Cat="Applications/Tools.Utility"}
    [pscustomobject]@{N=84;  Name="Quick.Calc";            Cat="Applications/Development/Inspector"}
    [pscustomobject]@{N=85;  Name="Reg.Vault";             Cat="Applications/System/Manager"}
    [pscustomobject]@{N=86;  Name="Sand.Fall";             Cat="Games/Sandbox"}
    [pscustomobject]@{N=87;  Name="Sched.Cast";            Cat="Applications/System/Manager"}
    [pscustomobject]@{N=88;  Name="Screen.Recorder";       Cat="Applications/Video"}
    [pscustomobject]@{N=89;  Name="Serve.Cast";            Cat="Applications/Network/Server"}
    [pscustomobject]@{N=90;  Name="Shortcut.Forge";        Cat="Applications/Files/Manager"}
    [pscustomobject]@{N=91;  Name="Signal.Flow";           Cat="Applications/Development/Inspector"}
    [pscustomobject]@{N=92;  Name="Skill.Cast";            Cat="Applications/Development/Inspector"}
    [pscustomobject]@{N=93;  Name="Sky.Drift";             Cat="Games/Arcade"}
    [pscustomobject]@{N=94;  Name="Snap.Duel";             Cat="Games/Casual"}
    [pscustomobject]@{N=95;  Name="Spec.Report";           Cat="Applications/System/Monitor"}
    [pscustomobject]@{N=96;  Name="Spec.View";             Cat="Applications/System/Monitor"}
    [pscustomobject]@{N=97;  Name="Star.Strike";           Cat="Games/Shooter"}
    [pscustomobject]@{N=98;  Name="Stay.Awake";            Cat="Applications/Automation"}
    [pscustomobject]@{N=99;  Name="Svc.Guard";             Cat="Applications/System/Manager"}
    [pscustomobject]@{N=100; Name="SVG.Forge";             Cat="Applications/Photo.Picture"}
    [pscustomobject]@{N=101; Name="Sys.Clean";             Cat="Applications/System/Manager"}
    [pscustomobject]@{N=102; Name="Tag.Forge";             Cat="Applications/Audio"}
    [pscustomobject]@{N=103; Name="Text.Forge";            Cat="Applications/Text"}
    [pscustomobject]@{N=104; Name="Tower.Guard";           Cat="Games/Strategy"}
    [pscustomobject]@{N=105; Name="Tray.Stats";            Cat="Applications/System/Monitor"}
    [pscustomobject]@{N=106; Name="Web.Shot";              Cat="Applications/Photo.Picture"}
    [pscustomobject]@{N=107; Name="Win.Event";             Cat="Applications/Development/Analyzer"}
    [pscustomobject]@{N=108; Name="Win.Scope";             Cat="Applications/System/Manager"}
    [pscustomobject]@{N=109; Name="Word.Cloud";            Cat="Applications/Text"}
    [pscustomobject]@{N=110; Name="Zip.Peek";              Cat="Applications/Files/Inspector"}
)

$checkedArr = New-Object bool[] $allApps.Count
$visibleIdx = [System.Collections.Generic.List[int]]::new()

$CB  = [System.Drawing.Color]::FromArgb(24, 24, 38)
$CPB = [System.Drawing.Color]::FromArgb(30, 30, 46)
$CT  = [System.Drawing.Color]::FromArgb(220, 220, 235)
$CDm = [System.Drawing.Color]::FromArgb(110, 110, 145)
$CAc = [System.Drawing.Color]::FromArgb(0, 200, 255)
$CBt = [System.Drawing.Color]::FromArgb(40, 40, 62)
$CBr = [System.Drawing.Color]::FromArgb(65, 65, 95)

$form = New-Object System.Windows.Forms.Form
$form.Text = "Playground | Publish"
$form.Size = New-Object System.Drawing.Size(720, 800)
$form.MinimumSize = New-Object System.Drawing.Size(500, 500)
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
$form.BackColor = $CB
$form.ForeColor = $CT
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::Sizable

$form.Add_Shown({
    try {
        $v = 1
        [WinDwm]::DwmSetWindowAttribute($form.Handle, 20, [ref]$v, 4) | Out-Null
        [WinUx]::SetWindowTheme($clb.Handle, "DarkMode_Explorer", $null) | Out-Null
    } catch {}
})

# Title
$lbTitle = New-Object System.Windows.Forms.Label
$lbTitle.Text = "Playground  |  Publish"
$lbTitle.Font = New-Object System.Drawing.Font("Segoe UI", 14, [System.Drawing.FontStyle]::Bold)
$lbTitle.ForeColor = $CAc
$lbTitle.Location = New-Object System.Drawing.Point(14, 14)
$lbTitle.AutoSize = $true
$form.Controls.Add($lbTitle)

# Filter box
$tbFilter = New-Object System.Windows.Forms.TextBox
$tbFilter.Location = New-Object System.Drawing.Point(14, 54)
$tbFilter.Size = New-Object System.Drawing.Size(686, 26)
$tbFilter.Anchor = [System.Windows.Forms.AnchorStyles]::Top -bor [System.Windows.Forms.AnchorStyles]::Left -bor [System.Windows.Forms.AnchorStyles]::Right
$tbFilter.BackColor = [System.Drawing.Color]::FromArgb(38, 38, 58)
$tbFilter.ForeColor = $CT
$tbFilter.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
$tbFilter.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$form.Controls.Add($tbFilter)

# Count label
$lbCount = New-Object System.Windows.Forms.Label
$lbCount.Location = New-Object System.Drawing.Point(14, 86)
$lbCount.AutoSize = $true
$lbCount.ForeColor = $CDm
$lbCount.Font = New-Object System.Drawing.Font("Segoe UI", 8.5)
$form.Controls.Add($lbCount)

# CheckedListBox
$clb = New-Object System.Windows.Forms.CheckedListBox
$clb.Location = New-Object System.Drawing.Point(14, 108)
$clb.Size = New-Object System.Drawing.Size(686, 590)
$clb.Anchor = [System.Windows.Forms.AnchorStyles]::Top -bor [System.Windows.Forms.AnchorStyles]::Left -bor [System.Windows.Forms.AnchorStyles]::Right -bor [System.Windows.Forms.AnchorStyles]::Bottom
$clb.BackColor = $CPB
$clb.ForeColor = $CT
$clb.Font = New-Object System.Drawing.Font("Consolas", 9.5)
$clb.CheckOnClick = $true
$clb.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
$clb.ItemHeight = 22
$form.Controls.Add($clb)

# Button factory
function MakeBtn($text, $x, $y, $w) {
    $b = New-Object System.Windows.Forms.Button
    $b.Text = $text
    $b.Location = New-Object System.Drawing.Point($x, $y)
    $b.Size = New-Object System.Drawing.Size($w, 32)
    $b.Anchor = [System.Windows.Forms.AnchorStyles]::Bottom -bor [System.Windows.Forms.AnchorStyles]::Left
    $b.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $b.BackColor = $CBt
    $b.ForeColor = $CT
    $b.FlatAppearance.BorderColor = $CBr
    $b.Cursor = [System.Windows.Forms.Cursors]::Hand
    return $b
}

$BY = 718
$btnAll    = MakeBtn "Select All"  14  $BY 108
$btnNone   = MakeBtn "Select None" 128 $BY 108

$btnCancel = MakeBtn "Cancel"  506 $BY 94
$btnCancel.Anchor = [System.Windows.Forms.AnchorStyles]::Bottom -bor [System.Windows.Forms.AnchorStyles]::Right

$btnOK = MakeBtn "Publish" 606 $BY 94
$btnOK.Anchor = [System.Windows.Forms.AnchorStyles]::Bottom -bor [System.Windows.Forms.AnchorStyles]::Right
$btnOK.BackColor = [System.Drawing.Color]::FromArgb(0, 100, 68)
$btnOK.FlatAppearance.BorderColor = [System.Drawing.Color]::FromArgb(0, 160, 100)

$btnCancel.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
$btnOK.DialogResult     = [System.Windows.Forms.DialogResult]::OK

$form.Controls.AddRange(@($btnAll, $btnNone, $btnCancel, $btnOK))
$form.AcceptButton = $btnOK
$form.CancelButton = $btnCancel

function UpdateCount {
    $n = 0
    foreach ($c in $checkedArr) { if ($c) { $n++ } }
    $lbCount.Text = "$n / $($allApps.Count) selected"
    $btnOK.Text    = if ($n -gt 0) { "Publish  ($n)" } else { "Publish" }
    $btnOK.Enabled = ($n -gt 0)
}

function RefreshList {
    $f = $tbFilter.Text.ToLower()
    $clb.BeginUpdate()
    $clb.Items.Clear()
    $visibleIdx.Clear()
    for ($i = 0; $i -lt $allApps.Count; $i++) {
        $a = $allApps[$i]
        if (-not $f -or $a.Name.ToLower().Contains($f) -or $a.Cat.ToLower().Contains($f)) {
            $line = "{0,3}. {1,-26}{2}" -f $a.N, $a.Name, $a.Cat
            $clb.Items.Add($line, $checkedArr[$i]) | Out-Null
            $visibleIdx.Add($i) | Out-Null
        }
    }
    $clb.EndUpdate()
    UpdateCount
}

$clb.Add_ItemCheck({
    param($s, $e)
    $ai = $visibleIdx[$e.Index]
    $checkedArr[$ai] = ($e.NewValue -eq [System.Windows.Forms.CheckState]::Checked)
    UpdateCount
})

$tbFilter.Add_TextChanged({ RefreshList })

$btnAll.Add_Click({
    for ($i = 0; $i -lt $checkedArr.Length; $i++) { $checkedArr[$i] = $true }
    RefreshList
})

$btnNone.Add_Click({
    for ($i = 0; $i -lt $checkedArr.Length; $i++) { $checkedArr[$i] = $false }
    RefreshList
})

RefreshList
$result = $form.ShowDialog()

if ($result -eq [System.Windows.Forms.DialogResult]::OK -and $OutFile) {
    $nums = @()
    for ($i = 0; $i -lt $allApps.Count; $i++) {
        if ($checkedArr[$i]) { $nums += $allApps[$i].N }
    }
    if ($nums.Count -gt 0) {
        ($nums -join " ") | Out-File -FilePath $OutFile -Encoding ascii -NoNewline
    }
}
