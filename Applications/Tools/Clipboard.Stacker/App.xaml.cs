using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using ClipboardStacker.Models;
using ClipboardStacker.Services;

namespace ClipboardStacker;

public partial class App : Application
{
    // ── 서비스 ──────────────────────────────────────────────────────
    private AppSettings?          _settings;
    private ClipboardStack?       _stack;
    private ClipboardMonitor?     _clipMonitor;
    private GlobalHotkeyService?  _hotkeys;
    private PopupWindow?          _popup;
    private NotifyIcon?           _tray;

    // ── 트레이 아이콘용 (숨김 Window HWND 확보) ──────────────────────
    private Window? _hwndHost;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 아이콘 생성
        var iconDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        IconGenerator.Generate(iconDir);

        // 설정 로드
        _settings = SettingsService.Load();
        _stack    = new ClipboardStack(_settings.MaxHistory);

        // 숨김 Window로 HWND 확보 (메시지 수신용)
        _hwndHost = new Window
        {
            Width = 0, Height = 0,
            WindowStyle      = WindowStyle.None,
            ShowInTaskbar    = false,
            Visibility       = Visibility.Hidden,
        };
        _hwndHost.Show();
        var helper = new WindowInteropHelper(_hwndHost);
        helper.EnsureHandle();
        var hwnd = helper.Handle;

        // WndProc 훅
        var src = HwndSource.FromHwnd(hwnd);
        src?.AddHook(WndProc);

        // 클립보드 모니터 등록
        _clipMonitor = new ClipboardMonitor();
        _clipMonitor.Attach(hwnd);
        _clipMonitor.ClipboardChanged += OnClipboardChanged;

        // 전역 단축키
        _hotkeys = new GlobalHotkeyService(hwnd);
        RegisterHotkeys();

        // 팝업 윈도우 (숨김 상태로 대기)
        _popup = new PopupWindow(_stack, _settings, PasteFromPopup);
        _popup.Show();
        _popup.Hide();

        // 트레이
        InitTray(iconDir);

        // 풍선 알림
        _tray?.ShowBalloonTip(1500, "Clipboard Stacker",
            "실행되었습니다. Ctrl+C로 복사하면 스택에 쌓입니다.",
            ToolTipIcon.Info);
    }

    // ── 단축키 등록 ───────────────────────────────────────────────
    private void RegisterHotkeys()
    {
        if (_settings is null || _hotkeys is null) return;
        _hotkeys.UnregisterAll();

        // Ctrl+Shift+V → 팝업 토글 / 스택에서 순서대로 붙여넣기
        _hotkeys.Register(_settings.PopupHotkeyMods, _settings.PopupHotkeyVk, OnPopupHotkey);
    }

    private void OnPopupHotkey()
    {
        Dispatcher.Invoke(() =>
        {
            if (_popup?.IsVisible == true)
            {
                // 팝업이 열려 있으면 스택 팝
                DoStackPaste();
            }
            else
            {
                _popup?.TogglePopup();
            }
        });
    }

    // ── 스택 붙여넣기 ─────────────────────────────────────────────
    private async void DoStackPaste()
    {
        if (_stack is null || _clipMonitor is null) return;

        var entry = _stack.PopNext();
        if (entry is null) { _popup?.HidePopup(); return; }

        var text = ApplyTransform(entry.Text);

        _popup?.HidePopup();
        _clipMonitor.IgnoreOnce();
        System.Windows.Clipboard.SetText(text);

        await Task.Delay(60); // 단축키 키업 완료 대기
        PasteService.SimulateCtrlV();
    }

    // 팝업에서 항목 클릭 → 붙여넣기
    private async void PasteFromPopup(string text)
    {
        if (_clipMonitor is null) return;
        _clipMonitor.IgnoreOnce();
        System.Windows.Clipboard.SetText(text);
        await Task.Delay(60);
        PasteService.SimulateCtrlV();
    }

    private string ApplyTransform(string text) => _settings?.Transform switch
    {
        TransformMode.Upper => text.ToUpperInvariant(),
        TransformMode.Lower => text.ToLowerInvariant(),
        TransformMode.Trim  => text.Trim(),
        _                   => text,
    };

    // ── 클립보드 변경 처리 ────────────────────────────────────────
    private void OnClipboardChanged(string text)
    {
        Dispatcher.Invoke(() => _stack?.Push(text));
    }

    // ── WndProc ───────────────────────────────────────────────────
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_clipMonitor?.HandleMessage(msg) == true) { handled = true; return IntPtr.Zero; }
        if (msg == GlobalHotkeyService.WM_HOTKEY && _hotkeys is not null)
            handled = _hotkeys.HandleMessage(wParam);
        return IntPtr.Zero;
    }

    // ── 트레이 ────────────────────────────────────────────────────
    private void InitTray(string iconDir)
    {
        var icoPath = Path.Combine(iconDir, IconGenerator.IconFileName);

        _tray = new NotifyIcon
        {
            Text    = "Clipboard Stacker",
            Visible = true,
            Icon    = File.Exists(icoPath)
                        ? new System.Drawing.Icon(icoPath)
                        : SystemIcons.Application,
        };

        var menu = new ContextMenuStrip { Renderer = new DarkMenuRenderer() };
        menu.Items.Add("스택 보기 / 붙여넣기", null, (_, _) => Dispatcher.Invoke(() => _popup?.TogglePopup()));
        menu.Items.Add("스택 지우기",           null, (_, _) => Dispatcher.Invoke(() => _stack?.Clear()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => Dispatcher.Invoke(ExitApp));

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick     += (_, _) => Dispatcher.Invoke(() => _popup?.TogglePopup());
    }

    private void ExitApp()
    {
        _tray?.Dispose();
        _hotkeys?.Dispose();
        _clipMonitor?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
