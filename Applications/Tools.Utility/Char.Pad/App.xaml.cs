using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace CharPad;

public partial class App : System.Windows.Application
{
    // ── Win32 P/Invoke ──────────────────────────────────────────────────
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private const int HotkeyId  = 9001;
    private const uint MOD_ALT   = 0x0001;
    private const uint MOD_CTRL  = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN   = 0x0008;
    private const uint VK_OEM_1  = 0xBA; // ;
    private const uint VK_SPACE  = 0x20;
    private const uint VK_C      = 0x43;
    private const int WM_HOTKEY  = 0x0312;

    // 미리 정의된 단축키 옵션 (표시명, modifier, vk)
    internal static readonly (string Label, uint Mod, uint Vk)[] HotkeyOptions =
    {
        ("Win+Shift+;",      MOD_WIN | MOD_SHIFT,              VK_OEM_1),
        ("Win+Shift+Space",  MOD_WIN | MOD_SHIFT,              VK_SPACE),
        ("Alt+Shift+C",      MOD_ALT | MOD_SHIFT,              VK_C),
        ("Ctrl+Alt+C",       MOD_CTRL | MOD_ALT,               VK_C),
        ("Ctrl+Alt+Shift+C", MOD_CTRL | MOD_ALT | MOD_SHIFT,  VK_C),
    };

    private NotifyIcon?    _tray;
    private PopupWindow?   _popup;
    private StorageService _storage = null!;
    private HwndSource?    _hwndSource;
    private System.Windows.Window? _hotkeyWindow; // GC 방지 — HWND 수명 보장
    private IntPtr         _prevHwnd;
    private IntPtr         _trayPrevHwnd;  // 트레이 메뉴 즐겨찾기 삽입용 이전 창
    private System.Threading.Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 단일 인스턴스
        _mutex = new System.Threading.Mutex(true, "CharPad_SingleInstance", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        _storage = new StorageService();
        BuildTray();
        RegisterGlobalHotkey();
    }

    // ── 트레이 아이콘 ───────────────────────────────────────────────────
    // pack 리소스로 내장된 app.ico를 System.Drawing.Icon으로 변환
    private static Icon LoadTrayIcon()
    {
        try
        {
            var sri = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Resources/app.ico"));
            if (sri != null)
            {
                using var ms = new System.IO.MemoryStream();
                sri.Stream.CopyTo(ms);
                ms.Position = 0;
                return new Icon(ms);
            }
        }
        catch { }
        return SystemIcons.Application;
    }

    private void BuildTray()
    {
        var hotkeyLabel = _storage.GetSetting("hotkey_label") ?? "Win+Shift+;";
        _tray = new NotifyIcon
        {
            Icon    = LoadTrayIcon(),
            Text    = $"Char.Pad — {hotkeyLabel}",
            Visible = true
        };

        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            AutoSize        = true,
            Font            = new Font("Segoe UI", 9.5f),
            BackColor       = ColorTranslator.FromHtml("#1A1A2E"),
            ForeColor       = ColorTranslator.FromHtml("#E0E0E0"),
            Renderer        = new DarkMenuRenderer()
        };
        menu.Items.Add("⌨  Char.Pad 열기",  null, (_, _) => ShowPopup());
        menu.Items.Add("?  사용 방법",       null, (_, _) => ShowHelp());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("🗑  최근 사용 초기화", null, (_, _) => ClearRecents());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("↑  내보내기 (JSON)",  null, (_, _) => ExportData());
        menu.Items.Add("↓  가져오기 (JSON)",  null, (_, _) => ImportData());
        menu.Items.Add(new ToolStripSeparator());

        // 단축키 변경 서브메뉴
        var hotkeyMenu = new ToolStripMenuItem("⌨  단축키 변경")
        {
            ForeColor = ColorTranslator.FromHtml("#E0E0E0"),
        };
        var currentHotkeyLabel = _storage.GetSetting("hotkey_label") ?? "Win+Shift+;";
        foreach (var (label, _, _) in HotkeyOptions)
        {
            var item = new ToolStripMenuItem(label)
            {
                ForeColor = ColorTranslator.FromHtml("#E0E0E0"),
                Checked   = label == currentHotkeyLabel,
            };
            item.Click += (_, _) => ChangeHotkey(label);
            hotkeyMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(hotkeyMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("✕  종료",            null, (_, _) => Shutdown());

        // 즐겨찾기 서브메뉴 (DropDownOpening 시 동적 갱신)
        var favMenu = new ToolStripMenuItem("⭐  즐겨찾기 빠른 삽입")
        {
            ForeColor = ColorTranslator.FromHtml("#E0E0E0"),
        };
        favMenu.DropDownOpening += (_, _) => RebuildFavMenuItems(favMenu);
        menu.Items.Insert(0, favMenu);
        menu.Items.Insert(1, new ToolStripSeparator());

        _tray.ContextMenuStrip = menu;
        // 트레이 클릭 전 이전 포그라운드 창 캡처 (즐겨찾기 빠른 삽입용)
        _tray.MouseDown += (_, _) => _trayPrevHwnd = GetForegroundWindow();
        _tray.MouseClick += (_, ev) =>
        {
            if (ev.Button == MouseButtons.Left) ShowPopup();
        };

        _tray.ShowBalloonTip(2000, "Char.Pad", $"{hotkeyLabel} 로 특수문자 입력", ToolTipIcon.Info);
    }

    // ── 전역 단축키 등록 ────────────────────────────────────────────────
    private void RegisterGlobalHotkey()
    {
        // 메시지 훅을 위한 숨김 창 사용 (_hotkeyWindow 필드 저장으로 GC 방지)
        _hotkeyWindow = new System.Windows.Window { Width = 0, Height = 0, WindowStyle = System.Windows.WindowStyle.None, ShowInTaskbar = false, Opacity = 0 };
        var helper = new WindowInteropHelper(_hotkeyWindow);
        helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);

        var (_, mod, vk) = GetCurrentHotkey();
        RegisterHotKey(helper.Handle, HotkeyId, mod, vk);
    }

    private (string Label, uint Mod, uint Vk) GetCurrentHotkey()
    {
        var label = _storage.GetSetting("hotkey_label") ?? "Win+Shift+;";
        return Array.Find(HotkeyOptions, h => h.Label == label) is { Label: not null } found
            ? found
            : HotkeyOptions[0];
    }

    internal void ChangeHotkey(string newLabel)
    {
        if (_hwndSource == null) return;
        var (_, newMod, newVk) = Array.Find(HotkeyOptions, h => h.Label == newLabel) is { Label: not null } found
            ? found : HotkeyOptions[0];

        UnregisterHotKey(_hwndSource.Handle, HotkeyId);
        RegisterHotKey(_hwndSource.Handle, HotkeyId, newMod, newVk);
        _storage.SetSetting("hotkey_label", newLabel);

        // 트레이 아이콘 툴팁 + 메뉴 체크 상태 업데이트
        if (_tray != null) _tray.Text = $"Char.Pad — {newLabel}";
        if (_tray?.ContextMenuStrip != null)
        {
            foreach (ToolStripItem item in _tray.ContextMenuStrip.Items)
            {
                if (item is ToolStripMenuItem hotkeyMenu && (hotkeyMenu.Text?.Contains("단축키") == true))
                {
                    foreach (ToolStripItem sub in hotkeyMenu.DropDownItems)
                    {
                        if (sub is ToolStripMenuItem subItem)
                            subItem.Checked = subItem.Text == newLabel;
                    }
                    break;
                }
            }
        }
        _tray?.ShowBalloonTip(1500, "Char.Pad", $"단축키 변경됨: {newLabel}", ToolTipIcon.Info);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            ShowPopup();
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ── 팝업 열기 ───────────────────────────────────────────────────────
    internal void ShowPopup()
    {
        // 핫키 토글: 팝업이 이미 열려 있으면 닫기
        if (_popup != null && _popup.IsVisible)
        {
            _popup.Hide();
            return;
        }

        _prevHwnd = GetForegroundWindow();

        if (_popup == null || !_popup.IsLoaded)
        {
            _popup = new PopupWindow(_storage);
            _popup.Closed += (_, _) => _popup = null;
        }

        _popup.ShowAt(_prevHwnd);
    }

    internal Task PasteToWindowAsync(IntPtr targetHwnd) => InputHelper.PasteToWindowAsync(targetHwnd);

    private void ClearRecents()
    {
        var confirm = DarkMessageBox.Show(
            "최근 사용 목록을 모두 지우시겠습니까?",
            "초기화 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        _storage.ClearRecents();
        _popup?.RefreshIfRecentTab();
        _tray?.ShowBalloonTip(1500, "Char.Pad", "최근 사용 목록이 초기화되었습니다", ToolTipIcon.Info);
    }

    // ── 내보내기 / 가져오기 ─────────────────────────────────────────────
    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ExportData()
    {
        using var dlg = new SaveFileDialog
        {
            Title      = "Char.Pad 데이터 내보내기",
            Filter     = "JSON 파일 (*.json)|*.json",
            FileName   = $"charpad_export_{DateTime.Now:yyyyMMdd}.json",
            DefaultExt = "json"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            var data = _storage.Export();
            var json = JsonSerializer.Serialize(new
            {
                favorites   = data.Favorites,
                customChars = data.CustomChars.Select(x => new { x.Char, x.Name })
            }, _jsonOpts);
            System.IO.File.WriteAllText(dlg.FileName, json, System.Text.Encoding.UTF8);
            _tray?.ShowBalloonTip(2000, "Char.Pad", $"내보내기 완료: {data.Favorites.Count}개 즐겨찾기, {data.CustomChars.Count}개 사용자 문자", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            DarkMessageBox.Show($"내보내기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportData()
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Char.Pad 데이터 가져오기",
            Filter = "JSON 파일 (*.json)|*.json"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            var json = System.IO.File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
            using var doc = JsonDocument.Parse(json);

            var favorites   = doc.RootElement.GetProperty("favorites").EnumerateArray()
                .Select(e => e.GetString()).Where(s => s != null).Select(s => s!).ToList();
            var customChars = doc.RootElement.GetProperty("customChars").EnumerateArray()
                .Select(e => (e.GetProperty("Char").GetString() ?? "", e.GetProperty("Name").GetString() ?? ""))
                .Where(t => t.Item1.Length > 0 && t.Item2.Length > 0)
                .ToList();

            var overwrite = DarkMessageBox.Show(
                $"즐겨찾기 {favorites.Count}개, 사용자 문자 {customChars.Count}개를 가져옵니다.\n중복 항목을 덮어쓰시겠습니까?",
                "가져오기 확인", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (overwrite == MessageBoxResult.Cancel) return;

            _storage.Import(new(favorites, customChars), overwrite == MessageBoxResult.Yes);
            _popup?.Refresh();
            _tray?.ShowBalloonTip(2000, "Char.Pad", $"가져오기 완료: {favorites.Count}개 즐겨찾기, {customChars.Count}개 사용자 문자", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            DarkMessageBox.Show($"가져오기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 트레이 즐겨찾기 서브메뉴 동적 갱신 ─────────────────────────────
    private void RebuildFavMenuItems(ToolStripMenuItem favMenu)
    {
        favMenu.DropDownItems.Clear();
        var favorites = _storage.GetFavorites().Take(8).ToList();
        if (favorites.Count == 0)
        {
            favMenu.DropDownItems.Add(new ToolStripMenuItem("(즐겨찾기 없음)")
            {
                Enabled   = false,
                ForeColor = ColorTranslator.FromHtml("#888888"),
            });
            return;
        }
        foreach (var ch in favorites)
        {
            var name        = CharDatabase.AllByChar.TryGetValue(ch, out var ent) ? ent.Name : ch;
            var displayName = name.Length > 20 ? name[..20] + "…" : name;
            var item        = new ToolStripMenuItem($"{ch}  {displayName}")
            {
                ForeColor = ColorTranslator.FromHtml("#E0E0E0"),
            };
            var captured = ch;
            item.Click += (_, _) => _ = InsertFavoriteFromTrayAsync(captured);
            favMenu.DropDownItems.Add(item);
        }
    }

    private async Task InsertFavoriteFromTrayAsync(string ch)
    {
        var target = _trayPrevHwnd;
        if (target == IntPtr.Zero) return;
        try
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                () => System.Windows.Clipboard.SetText(ch));
            _storage.AddRecent(ch);
            await Task.Delay(80);
            await InputHelper.PasteToWindowAsync(target);
        }
        catch { }
    }

    private void ShowHelp()
    {
        _prevHwnd = GetForegroundWindow();
        if (_popup == null || !_popup.IsLoaded)
        {
            _popup = new PopupWindow(_storage);
            _popup.Closed += (_, _) => _popup = null;
        }
        _popup.ShowAt(_prevHwnd);
        _popup.ShowHelpOverlay();
    }

    // ── 종료 ─────────────────────────────────────────────────────────────
    protected override void OnExit(ExitEventArgs e)
    {
        if (_hwndSource != null)
            UnregisterHotKey(_hwndSource.Handle, HotkeyId);
        _tray?.Dispose();
        _storage?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

// ── 다크 메뉴 렌더러 ────────────────────────────────────────────────────
internal class DarkMenuRenderer : ToolStripRenderer
{
    private static readonly System.Drawing.Color BgColor    = ColorTranslator.FromHtml("#1A1A2E");
    private static readonly System.Drawing.Color HoverColor = ColorTranslator.FromHtml("#1A3550");
    private static readonly System.Drawing.Color TextColor  = ColorTranslator.FromHtml("#E0E0E0");
    private static readonly System.Drawing.Color SepColor   = ColorTranslator.FromHtml("#1A3A55");

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        => e.Graphics.Clear(BgColor);

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            using var brush = new SolidBrush(HoverColor);
            var r = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
            e.Graphics.FillRectangle(brush, r);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = TextColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new DrawingPen(SepColor);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }
}
