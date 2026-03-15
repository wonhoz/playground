using System.Runtime.InteropServices;
using SysIO = System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HotkeyMap;

public enum HotkeyStatus { Unknown, Free, Occupied, SystemReserved }

public class HotkeyEntry
{
    public string DisplayKey { get; set; } = "";
    public string Owner { get; set; } = "";
    public HotkeyStatus Status { get; set; }
    public uint Modifier { get; set; }
    public uint VKey { get; set; }

    public Brush StatusColor => Status switch
    {
        HotkeyStatus.Occupied => new SolidColorBrush(Color.FromRgb(0x3D, 0x5A, 0x80)),
        HotkeyStatus.Free => new SolidColorBrush(Color.FromRgb(0x2E, 0x4A, 0x2E)),
        HotkeyStatus.SystemReserved => new SolidColorBrush(Color.FromRgb(0x4A, 0x2E, 0x2E)),
        _ => new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))
    };
}

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    const uint MOD_ALT = 0x0001;
    const uint MOD_CONTROL = 0x0002;
    const uint MOD_SHIFT = 0x0004;
    const uint MOD_WIN = 0x0008;
    const uint MOD_NOREPEAT = 0x4000;

    static readonly HashSet<uint> SystemWinKeys = [
        0x44, 0x45, 0x4C, 0x52, 0x53, 0x49, 0x58, 0x50, 0x4B, 0x4D, 0x20, 0x09,
        0x70, 0x71, 0x72, 0x73, 0x26, 0x28, 0x25, 0x27, 0xDB, 0xDD
    ];

    private readonly List<HotkeyEntry> _allEntries = [];
    private List<HotkeyEntry> _filtered = [];
    private IntPtr _hwnd;
    private uint _currentModifier = MOD_CONTROL;
    private readonly Dictionary<uint, Border> _keyBorders = [];

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        _hwnd = helper.Handle;
        int dark = 1;
        DwmSetWindowAttribute(_hwnd, 20, ref dark, sizeof(int));

        ModifierCombo.ItemsSource = new[] { "Ctrl", "Alt", "Ctrl+Alt", "Ctrl+Shift", "Alt+Shift", "Win" };
        ModifierCombo.SelectedIndex = 0;

        DrawKeyboard();
        StatusBar.Text = "수식키를 선택하고 [스캔]을 클릭하세요.";
    }

    uint GetCurrentModifier() => ModifierCombo.SelectedIndex switch
    {
        0 => MOD_CONTROL,
        1 => MOD_ALT,
        2 => MOD_CONTROL | MOD_ALT,
        3 => MOD_CONTROL | MOD_SHIFT,
        4 => MOD_ALT | MOD_SHIFT,
        5 => MOD_WIN,
        _ => MOD_CONTROL
    };

    void ModifierCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _currentModifier = GetCurrentModifier();
        RefreshKeyboardColors();
        ApplyFilter();
        UpdateStats();
    }

    // ─── 키보드 다이어그램 ─────────────────────────────────────────────
    static readonly (string Label, uint VKey, double W)[][] KeyRows =
    [
        [("Esc",0x1B,1),("F1",0x70,1),("F2",0x71,1),("F3",0x72,1),("F4",0x73,1),(" ",0,0.5),
         ("F5",0x74,1),("F6",0x75,1),("F7",0x76,1),("F8",0x77,1),(" ",0,0.5),
         ("F9",0x78,1),("F10",0x79,1),("F11",0x7A,1),("F12",0x7B,1)],
        [("`",0xC0,1),("1",0x31,1),("2",0x32,1),("3",0x33,1),("4",0x34,1),("5",0x35,1),
         ("6",0x36,1),("7",0x37,1),("8",0x38,1),("9",0x39,1),("0",0x30,1),
         ("-",0xBD,1),("=",0xBB,1),("⌫",0x08,2)],
        [("Tab",0x09,1.5),("Q",0x51,1),("W",0x57,1),("E",0x45,1),("R",0x52,1),("T",0x54,1),
         ("Y",0x59,1),("U",0x55,1),("I",0x49,1),("O",0x4F,1),("P",0x50,1),
         ("[",0xDB,1),("]",0xDD,1),("\\",0xDC,1.5)],
        [("Caps",0x14,1.75),("A",0x41,1),("S",0x53,1),("D",0x44,1),("F",0x46,1),("G",0x47,1),
         ("H",0x48,1),("J",0x4A,1),("K",0x4B,1),("L",0x4C,1),(";",0xBA,1),
         ("'",0xDE,1),("↵",0x0D,2.25)],
        [("Shift",0xA0,2.25),("Z",0x5A,1),("X",0x58,1),("C",0x43,1),("V",0x56,1),("B",0x42,1),
         ("N",0x4E,1),("M",0x4D,1),(",",0xBC,1),(".",0xBE,1),("/",0xBF,1),("Shift",0xA1,2.75)],
        [("Ctrl",0xA2,1.25),("Win",0x5B,1.25),("Alt",0xA4,1.25),("Space",0x20,6.25),
         ("Alt",0xA5,1.25),("Win",0x5C,1.25),("≡",0x5D,1.25),("Ctrl",0xA3,1.25)],
    ];

    void DrawKeyboard()
    {
        KeyboardCanvas.Children.Clear();
        _keyBorders.Clear();

        double keyW = 52, keyH = 46, gap = 4;
        double y = 0;

        foreach (var row in KeyRows)
        {
            double x = 0;
            foreach (var (label, vkey, w) in row)
            {
                if (w == 0) { x += keyW * 0.5 + gap; continue; }
                double kw = keyW * w + gap * (w - 1);

                bool isModKey = vkey is 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x5B or 0x5C;

                var border = new Border
                {
                    Width = kw, Height = keyH,
                    Background = vkey == 0 ? Brushes.Transparent
                        : isModKey ? new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x40))
                        : new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E)),
                    BorderBrush = vkey == 0 ? Brushes.Transparent
                        : new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Cursor = vkey != 0 ? Cursors.Hand : Cursors.Arrow,
                    Tag = vkey,
                    ToolTip = vkey != 0 ? BuildDisplayKey(_currentModifier, vkey) : null
                };
                if (vkey != 0) border.MouseDown += Key_MouseDown;

                border.Child = new TextBlock
                {
                    Text = label, FontSize = 9.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
                };

                Canvas.SetLeft(border, x);
                Canvas.SetTop(border, y);
                KeyboardCanvas.Children.Add(border);

                if (vkey != 0 && !_keyBorders.ContainsKey(vkey))
                    _keyBorders[vkey] = border;

                x += kw + gap;
            }
            y += keyH + gap;
        }
    }

    void RefreshKeyboardColors()
    {
        foreach (var (vkey, border) in _keyBorders)
        {
            bool isModKey = vkey is 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x5B or 0x5C;
            if (isModKey) continue;

            var entry = _allEntries.FirstOrDefault(e => e.VKey == vkey && e.Modifier == _currentModifier);
            border.Background = entry?.Status switch
            {
                HotkeyStatus.Occupied => new SolidColorBrush(Color.FromRgb(0x3D, 0x5A, 0x80)),
                HotkeyStatus.Free => new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x1A)),
                HotkeyStatus.SystemReserved => new SolidColorBrush(Color.FromRgb(0x4A, 0x1A, 0x1A)),
                _ => new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x2E))
            };
            border.ToolTip = BuildDisplayKey(_currentModifier, vkey) +
                (entry != null ? $"\n{entry.Status}: {entry.Owner}" : "\n미검사");
        }
    }

    void Key_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b && b.Tag is uint vkey)
        {
            var entry = _allEntries.FirstOrDefault(en => en.VKey == vkey && en.Modifier == _currentModifier);
            if (entry != null)
            {
                SelectedInfo.Text = $"{entry.DisplayKey}  →  {entry.Status}  /  {entry.Owner}";
                HotkeyList.SelectedItem = _filtered.FirstOrDefault(en => en.VKey == vkey && en.Modifier == _currentModifier);
            }
        }
    }

    // ─── 스캔 ──────────────────────────────────────────────────────────
    void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        _currentModifier = GetCurrentModifier();
        StatusText.Text = "스캔 중...";
        _allEntries.RemoveAll(en => en.Modifier == _currentModifier);

        var vkeys = new List<uint>();
        for (uint v = 0x41; v <= 0x5A; v++) vkeys.Add(v);
        for (uint v = 0x30; v <= 0x39; v++) vkeys.Add(v);
        for (uint v = 0x70; v <= 0x7B; v++) vkeys.Add(v);
        vkeys.AddRange([0x20u, 0x0Du, 0x09u, 0xBBu, 0xBDu, 0xDCu, 0xDBu, 0xDDu, 0xBAu, 0xBCu, 0xBEu, 0xBFu]);

        int tempId = 9000 + (_allEntries.Count * 100);
        foreach (var vk in vkeys)
        {
            if (_currentModifier == MOD_WIN && SystemWinKeys.Contains(vk))
            {
                _allEntries.Add(new HotkeyEntry
                {
                    VKey = vk, Modifier = _currentModifier,
                    DisplayKey = BuildDisplayKey(_currentModifier, vk),
                    Owner = "Windows 시스템",
                    Status = HotkeyStatus.SystemReserved
                });
                continue;
            }

            bool ok = RegisterHotKey(_hwnd, tempId, _currentModifier | MOD_NOREPEAT, vk);
            if (ok) UnregisterHotKey(_hwnd, tempId);
            _allEntries.Add(new HotkeyEntry
            {
                VKey = vk, Modifier = _currentModifier,
                DisplayKey = BuildDisplayKey(_currentModifier, vk),
                Owner = ok ? "사용 가능" : GetKnownOwner(vk),
                Status = ok ? HotkeyStatus.Free : HotkeyStatus.Occupied
            });
            tempId++;
        }

        ApplyFilter();
        RefreshKeyboardColors();
        UpdateStats();
        StatusText.Text = $"완료 ({vkeys.Count}개 검사)";
        StatusBar.Text = $"[{ModifierCombo.SelectedItem}] 스캔 완료.";
    }

    string BuildDisplayKey(uint mod, uint vk)
    {
        var parts = new List<string>();
        if ((mod & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mod & MOD_ALT) != 0) parts.Add("Alt");
        if ((mod & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((mod & MOD_WIN) != 0) parts.Add("Win");
        parts.Add(VKeyToString(vk));
        return string.Join("+", parts);
    }

    string VKeyToString(uint vk) => vk switch
    {
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x70 and <= 0x7B => $"F{vk - 0x6F}",
        0x20 => "Space", 0x0D => "Enter", 0x09 => "Tab",
        0xBB => "=", 0xBD => "-", 0xDC => "\\",
        0xDB => "[", 0xDD => "]", 0xBA => ";",
        0xBC => ",", 0xBE => ".", 0xBF => "/",
        _ => $"0x{vk:X2}"
    };

    string GetKnownOwner(uint vk)
    {
        if (_currentModifier == MOD_CONTROL)
        {
            return vk switch
            {
                0x41 => "전체 선택 (시스템)", 0x43 => "복사 (시스템)",
                0x56 => "붙여넣기 (시스템)", 0x58 => "잘라내기 (시스템)",
                0x5A => "실행 취소 (시스템)", 0x59 => "다시 실행 (시스템)",
                0x53 => "저장 (시스템)", 0x50 => "인쇄 (시스템)",
                0x46 => "찾기 (시스템)", 0x4E => "새 파일 (시스템)",
                0x57 => "닫기 (시스템)", 0x54 => "새 탭 (시스템)",
                _ => "다른 앱이 점유 중"
            };
        }
        if (_currentModifier == MOD_WIN)
        {
            return vk switch
            {
                0x44 => "바탕화면 표시 (Windows)", 0x45 => "파일 탐색기 (Windows)",
                0x4C => "잠금 화면 (Windows)", 0x52 => "실행 (Windows)",
                0x53 => "검색 (Windows)", 0x49 => "설정 (Windows)",
                _ => "Windows가 점유 중"
            };
        }
        return "다른 앱이 점유 중";
    }

    void ApplyFilter()
    {
        string q = SearchBox.Text.Trim().ToLower();
        _filtered = string.IsNullOrEmpty(q)
            ? [.. _allEntries.Where(e => e.Modifier == _currentModifier).OrderBy(e => e.Status).ThenBy(e => e.VKey)]
            : [.. _allEntries.Where(e => e.Modifier == _currentModifier &&
               (e.DisplayKey.ToLower().Contains(q) || e.Owner.ToLower().Contains(q)))];
        HotkeyList.ItemsSource = _filtered;
    }

    void UpdateStats()
    {
        var entries = _allEntries.Where(e => e.Modifier == _currentModifier).ToList();
        StatTotal.Text = $"전체: {entries.Count}개 검사";
        StatOccupied.Text = $"점유: {entries.Count(e => e.Status == HotkeyStatus.Occupied)}개";
        StatFree.Text = $"사용 가능: {entries.Count(e => e.Status == HotkeyStatus.Free)}개";
        StatSystem.Text = $"시스템 예약: {entries.Count(e => e.Status == HotkeyStatus.SystemReserved)}개";
    }

    // ─── 테스트 ───────────────────────────────────────────────────────
    void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Window
        {
            Title = "단축키 테스트", Width = 380, Height = 200,
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize, Owner = this
        };
        var resultTb = new TextBlock
        {
            FontSize = 22, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0)
        };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "단축키를 누르면 충돌 여부를 즉시 확인합니다.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(resultTb);
        dlg.Content = panel;
        dlg.KeyDown += (_, ke) =>
        {
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(ke.Key);
            uint mod = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mod |= MOD_CONTROL;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mod |= MOD_ALT;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mod |= MOD_SHIFT;
            if (mod == 0 || vk < 0x20) return;

            IntPtr h = new System.Windows.Interop.WindowInteropHelper(dlg).Handle;
            bool ok = RegisterHotKey(h, 99998, mod | MOD_NOREPEAT, vk);
            if (ok) UnregisterHotKey(h, 99998);
            resultTb.Text = ok ? "✅ 사용 가능" : "❌ 점유됨";
            resultTb.Foreground = new SolidColorBrush(ok ? Color.FromRgb(0x66, 0xBB, 0x6A) : Color.FromRgb(0xEF, 0x53, 0x50));
        };
        dlg.Loaded += (_, _) =>
        {
            var h = new System.Windows.Interop.WindowInteropHelper(dlg);
            int dark = 1;
            DwmSetWindowAttribute(h.Handle, 20, ref dark, sizeof(int));
        };
        dlg.ShowDialog();
    }

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { if (IsLoaded) ApplyFilter(); }
    void HotkeyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HotkeyList.SelectedItem is HotkeyEntry entry)
            SelectedInfo.Text = $"{entry.DisplayKey}  →  {entry.Status}  /  {entry.Owner}";
    }
    void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _allEntries.Clear(); _filtered.Clear(); HotkeyList.ItemsSource = null;
        DrawKeyboard();
        StatTotal.Text = StatOccupied.Text = StatFree.Text = StatSystem.Text = SelectedInfo.Text = StatusText.Text = "";
        StatusBar.Text = "초기화 완료.";
    }

    void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var free = _allEntries.Where(en => en.Status == HotkeyStatus.Free).ToList();
        if (!free.Any()) { MessageBox.Show("스캔 후 사용 가능한 단축키가 있을 때 내보낼 수 있습니다.", "Hotkey.Map"); return; }
        var sb = new StringBuilder();
        sb.AppendLine("; Hotkey.Map — 사용 가능한 단축키 (AutoHotkey v2)");
        sb.AppendLine($"; 생성일: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        foreach (var en in free)
        {
            string m = "";
            if ((en.Modifier & MOD_CONTROL) != 0) m += "^";
            if ((en.Modifier & MOD_ALT) != 0) m += "!";
            if ((en.Modifier & MOD_SHIFT) != 0) m += "+";
            if ((en.Modifier & MOD_WIN) != 0) m += "#";
            sb.AppendLine($"{m}{VKeyToString(en.VKey).ToLower()}::  ; {en.DisplayKey}");
            sb.AppendLine("    MsgBox \"단축키: " + en.DisplayKey + "\"");
            sb.AppendLine("return\n");
        }
        var path = SysIO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "hotkey_map_free.ahk");
        SysIO.File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        StatusBar.Text = $"내보내기 완료: {path}";
    }
}
