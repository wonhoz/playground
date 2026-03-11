using System.Text;

namespace KeyTest;

/// <summary>키 이벤트 로그 항목</summary>
public class KeyLogEntry
{
    public string Time    { get; set; } = "";
    public string Message { get; set; } = "";
    public SolidColorBrush Color { get; set; } = new(Colors.White);
}

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    // ── 저수준 키보드 훅 P/Invoke ──────────────────────────────────────────
    private const int  WH_KEYBOARD_LL = 13;
    private const int  WM_KEYDOWN     = 0x0100;
    private const int  WM_KEYUP       = 0x0101;
    private const int  WM_SYSKEYDOWN  = 0x0104;
    private const int  WM_SYSKEYUP    = 0x0105;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private IntPtr           _hookHandle = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc;

    // ── 상태 ──────────────────────────────────────────────────────────────
    private readonly ObservableCollection<KeyLogEntry> _log = [];

    // 키 상태: VK → (눌림 시각, 눌린 횟수, 채터링 횟수)
    private readonly Dictionary<uint, (DateTime PressTime, int PressCount, int ChatterCount)> _keyState = [];
    private readonly HashSet<uint>  _pressedKeys  = [];  // 현재 눌린 키
    private readonly HashSet<uint>  _testedKeys   = [];  // 한 번이라도 눌린 키
    private readonly HashSet<uint>  _problemKeys  = [];  // 문제 키

    private int _totalPresses;
    private int _maxSimultaneous;

    // 채터링 임계: 50ms 이내 재입력 = 채터링
    private const int ChatterThresholdMs = 50;

    // ── 키보드 레이아웃 (ANSI 104) ────────────────────────────────────────
    // 각 키: (label, VK, col_start, row, col_span)
    private record KeyDef(string Label, uint Vk, double X, double Y, double W, double H = 36);

    private readonly Dictionary<uint, Border> _keyButtons = [];

    // 색상
    private static readonly SolidColorBrush BrIdle     = new(Color.FromRgb(0x22, 0x22, 0x33));
    private static readonly SolidColorBrush BrPressed  = new(Color.FromRgb(0x20, 0x80, 0x20));
    private static readonly SolidColorBrush BrTested   = new(Color.FromRgb(0x18, 0x40, 0x18));
    private static readonly SolidColorBrush BrProblem  = new(Color.FromRgb(0x60, 0x10, 0x10));
    private static readonly SolidColorBrush BrBorder   = new(Color.FromRgb(0x3A, 0x3A, 0x5A));
    private static readonly SolidColorBrush BrGreen    = new(Color.FromRgb(0x6E, 0xFF, 0x6E));
    private static readonly SolidColorBrush BrRed      = new(Color.FromRgb(0xFF, 0x6B, 0x6B));
    private static readonly SolidColorBrush BrPurple   = new(Color.FromRgb(0x7B, 0x68, 0xEE));
    private static readonly SolidColorBrush BrGray     = new(Color.FromRgb(0x88, 0x88, 0x88));

    static MainWindow()
    {
        BrIdle.Freeze(); BrPressed.Freeze(); BrTested.Freeze(); BrProblem.Freeze();
        BrBorder.Freeze(); BrGreen.Freeze(); BrRed.Freeze(); BrPurple.Freeze(); BrGray.Freeze();
    }

    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            int v = 1;
            DwmSetWindowAttribute(h, 20, ref v, sizeof(int));
        };

        LstLog.ItemsSource = _log;

        Loaded += (_, _) =>
        {
            BuildKeyboard();
            InstallHook();
        };

        Closed += (_, _) => UninstallHook();
    }

    // ── 키보드 레이아웃 구성 ──────────────────────────────────────────────

    private void BuildKeyboard()
    {
        KeyboardCanvas.Children.Clear();
        _keyButtons.Clear();

        double ks = 40; // 키 크기 (px)
        double gap = 2;
        double u   = ks + gap;

        // Row 0: Function keys
        var row0 = new[]
        {
            ("Esc",  0x1Bu, 0.0,     0.0),
            ("F1",   0x70u, u*1.5,   0.0), ("F2",  0x71u, u*2.5,   0.0), ("F3",  0x72u, u*3.5,   0.0), ("F4",  0x73u, u*4.5,   0.0),
            ("F5",   0x74u, u*5.75,  0.0), ("F6",  0x75u, u*6.75,  0.0), ("F7",  0x76u, u*7.75,  0.0), ("F8",  0x77u, u*8.75,  0.0),
            ("F9",   0x78u, u*10.0,  0.0), ("F10", 0x79u, u*11.0,  0.0), ("F11", 0x7Au, u*12.0,  0.0), ("F12", 0x7Bu, u*13.0,  0.0),
            ("PrtSc",0x2Cu, u*14.25, 0.0), ("ScrLk",0x91u,u*15.25, 0.0), ("Pause",0x13u,u*16.25, 0.0),
        };

        // Row 1: Number row
        var row1 = new[]
        {
            ("`",  0xC0u,0.0,  u*1.1), ("1",0x31u,u,    u*1.1), ("2",0x32u,u*2,  u*1.1), ("3",0x33u,u*3,  u*1.1),
            ("4",  0x34u,u*4,  u*1.1), ("5",0x35u,u*5,  u*1.1), ("6",0x36u,u*6,  u*1.1), ("7",0x37u,u*7,  u*1.1),
            ("8",  0x38u,u*8,  u*1.1), ("9",0x39u,u*9,  u*1.1), ("0",0x30u,u*10, u*1.1), ("-",0xBDu,u*11, u*1.1),
            ("=",  0xBBu,u*12, u*1.1),
        };

        // Row 2: QWERTY
        var row2 = new[]
        {
            ("Q",0x51u,u*1.5,u*2.1),("W",0x57u,u*2.5,u*2.1),("E",0x45u,u*3.5,u*2.1),("R",0x52u,u*4.5,u*2.1),
            ("T",0x54u,u*5.5,u*2.1),("Y",0x59u,u*6.5,u*2.1),("U",0x55u,u*7.5,u*2.1),("I",0x49u,u*8.5,u*2.1),
            ("O",0x4Fu,u*9.5,u*2.1),("P",0x50u,u*10.5,u*2.1),("[",0xDBu,u*11.5,u*2.1),("]",0xDDu,u*12.5,u*2.1),
            ("\\",0xDCu,u*13.5,u*2.1),
        };

        // Row 3: ASDF
        var row3 = new[]
        {
            ("A",0x41u,u*1.75,u*3.1),("S",0x53u,u*2.75,u*3.1),("D",0x44u,u*3.75,u*3.1),("F",0x46u,u*4.75,u*3.1),
            ("G",0x47u,u*5.75,u*3.1),("H",0x48u,u*6.75,u*3.1),("J",0x4Au,u*7.75,u*3.1),("K",0x4Bu,u*8.75,u*3.1),
            ("L",0x4Cu,u*9.75,u*3.1),(";",0xBAu,u*10.75,u*3.1),("'",0xDEu,u*11.75,u*3.1),
        };

        // Row 4: ZXCV
        var row4 = new[]
        {
            ("Z",0x5Au,u*2.25,u*4.1),("X",0x58u,u*3.25,u*4.1),("C",0x43u,u*4.25,u*4.1),("V",0x56u,u*5.25,u*4.1),
            ("B",0x42u,u*6.25,u*4.1),("N",0x4Eu,u*7.25,u*4.1),("M",0x4Du,u*8.25,u*4.1),(",",0xBCu,u*9.25,u*4.1),
            (".",0xBEu,u*10.25,u*4.1),("/",0xBFu,u*11.25,u*4.1),
        };

        // 특수 키
        var specials = new[]
        {
            ("Backspace", 0x08u, u*13.0,  u*1.1, u*2.0),
            ("Tab",       0x09u, 0.0,     u*2.1, u*1.5),
            ("CapsLock",  0x14u, 0.0,     u*3.1, u*1.75),
            ("Enter",     0x0Du, u*12.75, u*3.1, u*2.25),
            ("L.Shift",   0xA0u, 0.0,     u*4.1, u*2.25),
            ("R.Shift",   0xA1u, u*12.25, u*4.1, u*2.75),
            ("L.Ctrl",    0xA2u, 0.0,     u*5.1, u*1.5),
            ("L.Win",     0x5Bu, u*1.5,   u*5.1, u*1.25),
            ("L.Alt",     0xA4u, u*2.75,  u*5.1, u*1.25),
            ("Space",     0x20u, u*4.0,   u*5.1, u*5.5),
            ("R.Alt",     0xA5u, u*9.5,   u*5.1, u*1.25),
            ("R.Win",     0x5Cu, u*10.75, u*5.1, u*1.25),
            ("Menu",      0x5Du, u*12.0,  u*5.1, u*1.25),
            ("R.Ctrl",    0xA3u, u*13.25, u*5.1, u*1.5),
            // Navigation cluster
            ("Ins",  0x2Du, u*14.25, u*1.1, ks),
            ("Home", 0x24u, u*15.25, u*1.1, ks),
            ("PgUp", 0x21u, u*16.25, u*1.1, ks),
            ("Del",  0x2Eu, u*14.25, u*2.1, ks),
            ("End",  0x23u, u*15.25, u*2.1, ks),
            ("PgDn", 0x22u, u*16.25, u*2.1, ks),
            ("↑",    0x26u, u*15.25, u*4.1, ks),
            ("←",    0x25u, u*14.25, u*5.1, ks),
            ("↓",    0x28u, u*15.25, u*5.1, ks),
            ("→",    0x27u, u*16.25, u*5.1, ks),
        };

        // 일반 키 배치
        foreach (var rows in new[] { row0, row1, row2, row3, row4 })
            foreach (var (label, vk, x, y) in rows)
                AddKey(label, vk, x, y, ks, ks);

        // 특수 키 배치
        foreach (var (label, vk, x, y, w) in specials)
            AddKey(label, vk, x, y, w is double d and > 40 ? d : ks, ks);
    }

    private void AddKey(string label, uint vk, double x, double y, double w, double h)
    {
        var border = new Border
        {
            Width = w, Height = h, CornerRadius = new CornerRadius(4),
            Background = BrIdle, BorderBrush = BrBorder, BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand, Tag = vk,
        };
        var tb = new TextBlock
        {
            Text = label, Foreground = BrGray,
            FontSize = label.Length > 3 ? 9 : 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Consolas"),
        };
        border.Child = tb;
        Canvas.SetLeft(border, x); Canvas.SetTop(border, y);
        KeyboardCanvas.Children.Add(border);
        _keyButtons[vk] = border;
    }

    // ── 저수준 훅 ─────────────────────────────────────────────────────────

    private void InstallHook()
    {
        _hookProc = LowLevelHookCallback;
        var module = GetModuleHandle(null);
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, module, 0);
        TxtStatus.Text = _hookHandle != IntPtr.Zero
            ? "저수준 키보드 훅 활성화 — 모든 키 이벤트 캡처 중"
            : "훅 설치 실패 (관리자 권한 필요 시 재실행)";
    }

    private void UninstallHook()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr LowLevelHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kbs  = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            bool down = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;
            bool up   = wParam == WM_KEYUP   || wParam == WM_SYSKEYUP;
            var  now  = DateTime.UtcNow;

            if (down || up)
                Dispatcher.InvokeAsync(() => HandleKey(kbs.vkCode, kbs.scanCode, down, now));
        }
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void HandleKey(uint vk, uint scanCode, bool down, DateTime eventTime)
    {
        string keyName = GetKeyName(vk);

        if (down)
        {
            _pressedKeys.Add(vk);
            _testedKeys.Add(vk);
            _totalPresses++;

            // 채터링 감지
            bool chattering = false;
            if (_keyState.TryGetValue(vk, out var prev))
            {
                double msSinceLast = (eventTime - prev.PressTime).TotalMilliseconds;
                if (msSinceLast < ChatterThresholdMs && msSinceLast > 1)
                {
                    chattering = true;
                    _problemKeys.Add(vk);
                    var chatterEntry = new KeyLogEntry
                    {
                        Time    = eventTime.ToString("HH:mm:ss"),
                        Message = $"채터링 감지! [{keyName}] 간격: {msSinceLast:F1}ms",
                        Color   = BrRed,
                    };
                    _log.Insert(0, chatterEntry);
                    if (_log.Count > 200) _log.RemoveAt(_log.Count - 1);
                    UpdateBdrChattering(true, keyName);
                }
                _keyState[vk] = (eventTime, prev.PressCount + 1, prev.ChatterCount + (chattering ? 1 : 0));
            }
            else
            {
                _keyState[vk] = (eventTime, 1, 0);
            }

            // 동시 입력 측정 (N-Key Rollover)
            int simCount = _pressedKeys.Count;
            if (simCount > _maxSimultaneous) _maxSimultaneous = simCount;

            // 키 비주얼 업데이트
            SetKeyColor(vk, BrPressed);

            // HUD
            TxtCurrentKey.Text = keyName;
            TxtVkCode.Text     = $"0x{vk:X2} ({vk})";
            TxtScanCode.Text   = $"0x{scanCode:X2} ({scanCode})";
            TxtGhosting.Text   = $"동시 입력: {simCount}키 (최대 {_maxSimultaneous}키)";

            // 로그 (주요 이벤트만)
            var entry = new KeyLogEntry
            {
                Time    = eventTime.ToString("HH:mm:ss"),
                Message = $"↓ {keyName,-12} VK=0x{vk:X2}  SC=0x{scanCode:X2}",
                Color   = BrPurple,
            };
            _log.Insert(0, entry);
            if (_log.Count > 300) _log.RemoveAt(_log.Count - 1);
        }
        else // up
        {
            _pressedKeys.Remove(vk);

            // 고착 키 감지: 3초 이상 눌림
            if (_keyState.TryGetValue(vk, out var state))
            {
                double heldMs = (eventTime - state.PressTime).TotalMilliseconds;
                if (heldMs > 3000 && !IsModifierKey(vk))
                {
                    _problemKeys.Add(vk);
                    var stickEntry = new KeyLogEntry
                    {
                        Time    = eventTime.ToString("HH:mm:ss"),
                        Message = $"고착 감지! [{keyName}] {heldMs / 1000:F1}초 동안 눌림",
                        Color   = BrRed,
                    };
                    _log.Insert(0, stickEntry);
                    if (_log.Count > 300) _log.RemoveAt(_log.Count - 1);
                    SetKeyColor(vk, BrProblem);
                    UpdateBdrSticking(keyName);
                }
                else
                {
                    SetKeyColor(vk, _problemKeys.Contains(vk) ? BrProblem : BrTested);
                }
            }

            TxtGhosting.Text = $"동시 입력: {_pressedKeys.Count}키 (최대 {_maxSimultaneous}키)";
        }

        // 통계 업데이트
        TxtTotalPresses.Text = _totalPresses.ToString();
        TxtTestedKeys.Text   = $"{_testedKeys.Count} / 104";
        TxtIssues.Text       = _problemKeys.Count == 0 ? "없음" : $"{_problemKeys.Count}개";
        TxtIssues.Foreground = _problemKeys.Count == 0 ? BrGreen : BrRed;
        TxtNkro.Text         = $"최대 동시 입력: {_maxSimultaneous}키";
    }

    private void SetKeyColor(uint vk, SolidColorBrush color)
    {
        if (_keyButtons.TryGetValue(vk, out var border))
            border.Background = color;
    }

    private void UpdateBdrChattering(bool detected, string key)
    {
        if (BdrChattering.Child is TextBlock tb)
        {
            tb.Text       = detected ? $"채터링 감지: {key}" : "채터링 정상";
            tb.Foreground = detected ? BrRed : BrGreen;
        }
    }

    private void UpdateBdrSticking(string key)
    {
        if (BdrSticking.Child is TextBlock tb)
        {
            tb.Text       = $"고착 감지: {key}";
            tb.Foreground = BrRed;
        }
    }

    private static bool IsModifierKey(uint vk) =>
        vk is 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x5B or 0x5C or 0x14;

    private static string GetKeyName(uint vk) => vk switch
    {
        0x08 => "Backspace", 0x09 => "Tab",    0x0D => "Enter",   0x10 => "Shift",
        0x11 => "Ctrl",      0x12 => "Alt",     0x13 => "Pause",   0x14 => "CapsLock",
        0x1B => "Escape",    0x20 => "Space",   0x21 => "PageUp",  0x22 => "PageDown",
        0x23 => "End",       0x24 => "Home",    0x25 => "Left",    0x26 => "Up",
        0x27 => "Right",     0x28 => "Down",    0x2C => "PrtSc",   0x2D => "Insert",
        0x2E => "Delete",    0x30 => "0",       0x31 => "1",       0x32 => "2",
        0x33 => "3",         0x34 => "4",       0x35 => "5",       0x36 => "6",
        0x37 => "7",         0x38 => "8",       0x39 => "9",
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        0x5B => "L.Win",     0x5C => "R.Win",   0x5D => "Menu",
        >= 0x70 and <= 0x7B => $"F{vk - 0x6F}",
        0x91 => "ScrLk",     0xA0 => "L.Shift", 0xA1 => "R.Shift",
        0xA2 => "L.Ctrl",    0xA3 => "R.Ctrl",  0xA4 => "L.Alt",   0xA5 => "R.Alt",
        0xBA => ";",         0xBB => "=",       0xBC => ",",       0xBD => "-",
        0xBE => ".",         0xBF => "/",       0xC0 => "`",       0xDB => "[",
        0xDC => "\\",        0xDD => "]",       0xDE => "'",
        _ => $"VK_{vk:X2}",
    };

    // ── 버튼 핸들러 ───────────────────────────────────────────────────────

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _log.Clear();
        _testedKeys.Clear();
        _problemKeys.Clear();
        _pressedKeys.Clear();
        _keyState.Clear();
        _totalPresses     = 0;
        _maxSimultaneous  = 0;

        foreach (var b in _keyButtons.Values)
            b.Background = BrIdle;

        TxtCurrentKey.Text  = "——";
        TxtVkCode.Text      = "——";
        TxtScanCode.Text    = "——";
        TxtTotalPresses.Text = "0";
        TxtTestedKeys.Text  = "0 / 104";
        TxtIssues.Text      = "없음";
        TxtIssues.Foreground = BrGreen;
        TxtGhosting.Text    = "동시 입력: 0키";
        TxtNkro.Text        = "";

        if (BdrChattering.Child is TextBlock ct) { ct.Text = "채터링 정상"; ct.Foreground = BrGreen; }
        if (BdrSticking.Child is TextBlock st)   { st.Text = "고착 감지 대기 중"; st.Foreground = BrPurple; }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title    = "리포트 저장",
            Filter   = "HTML|*.html|텍스트|*.txt",
            FileName = $"KeyTest_{DateTime.Now:yyyyMMdd_HHmmss}.html",
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine("<title>Key.Test 리포트</title>");
        sb.AppendLine("<style>body{background:#111;color:#ccc;font-family:Consolas,monospace;}</style></head><body>");
        sb.AppendLine($"<h2>Key.Test 키보드 진단 리포트</h2><p>생성: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine($"<p>총 키 입력: {_totalPresses} | 테스트 키: {_testedKeys.Count}/104 | 최대 동시 입력: {_maxSimultaneous}키</p>");

        if (_problemKeys.Count > 0)
        {
            sb.AppendLine("<h3 style='color:#FF6B6B'>문제 감지 키</h3><ul>");
            foreach (var vk in _problemKeys)
                sb.AppendLine($"<li style='color:#FF6B6B'>{GetKeyName(vk)} (VK 0x{vk:X2})</li>");
            sb.AppendLine("</ul>");
        }
        else
        {
            sb.AppendLine("<p style='color:#6EFF6E'>✓ 문제 없음</p>");
        }

        sb.AppendLine("<h3>이벤트 로그 (최근 100개)</h3><table border='0' style='border-collapse:collapse'>");
        foreach (var entry in _log.Take(100))
            sb.AppendLine($"<tr><td style='color:#333;padding:2px 8px'>{entry.Time}</td><td>{entry.Message}</td></tr>");
        sb.AppendLine("</table></body></html>");

        System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
        TxtStatus.Text = $"리포트 저장 완료: {dlg.FileName}";
    }
}
