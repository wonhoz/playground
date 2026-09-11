using KeyMacro.Models;
using KeyMacro.Services;

namespace KeyMacro.Views;

/// <summary>실제 키 입력을 그대로 받아 가상 키 코드로 기록하는 다이얼로그</summary>
public partial class KeyCaptureDialog : Window
{
    private readonly KeyboardHook _hook = new();

    // 훅에서 키를 삼키면 시스템 키 상태(GetAsyncKeyState)가 갱신되지 않는다.
    // 그래서 수식 키 눌림은 훅이 본 이벤트로 직접 추적한다.
    private bool _ctrlHeld, _altHeld, _shiftHeld, _winHeld;

    private KeyCaptureDialog(string header, string hint)
    {
        InitializeComponent();
        TxtHeader.Text = header;
        TxtHint.Text = hint;

        _hook.KeyEvent += OnKey;

        Loaded += (_, _) =>
        {
            SyncHeldFromSystem();       // 창을 열기 전부터 누르고 있던 수식 키 반영
            _hook.Install();
            UpdateDisplay();
        };
        Activated   += (_, _) => { SyncHeldFromSystem(); _hook.Install(); };
        Deactivated += (_, _) => _hook.Uninstall();   // 다른 창을 쓰는 동안 키를 가로채지 않도록
        Closed      += (_, _) => _hook.Dispose();
    }

    public int Vk { get; private set; }
    public bool Ctrl { get; private set; }
    public bool Alt { get; private set; }
    public bool Shift { get; private set; }
    public bool Win { get; private set; }

    /// <summary>키 입력을 받아 조합을 돌려준다 (취소 시 false)</summary>
    public static bool TryCapture(Window owner, string header, string hint, out KeyCombo combo)
    {
        var dialog = new KeyCaptureDialog(header, hint) { Owner = owner };
        DarkTitle.Apply(dialog);

        bool ok = dialog.ShowDialog() == true;
        combo = new KeyCombo(dialog.Vk, dialog.Ctrl, dialog.Alt, dialog.Shift, dialog.Win);
        return ok && combo.Vk != 0;
    }

    // ── 수식 키 판별 (저수준 훅은 좌/우가 구분된 코드를 준다) ──────────
    private static bool IsCtrl(int vk)  => vk is 0x11 or 0xA2 or 0xA3;
    private static bool IsAlt(int vk)   => vk is 0x12 or 0xA4 or 0xA5;
    private static bool IsShift(int vk) => vk is 0x10 or 0xA0 or 0xA1;
    private static bool IsWin(int vk)   => vk is 0x5B or 0x5C;

    private static bool SystemHeld(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

    private void SyncHeldFromSystem()
    {
        _ctrlHeld  = SystemHeld(0x11);
        _altHeld   = SystemHeld(0x12);
        _shiftHeld = SystemHeld(0x10);
        _winHeld   = SystemHeld(0x5B) || SystemHeld(0x5C);
    }

    private bool OnKey(HookedKey key)
    {
        // 수식 키는 눌림·뗌을 모두 추적해야 조합을 정확히 알 수 있다
        if (IsCtrl(key.Vk) || IsAlt(key.Vk) || IsShift(key.Vk) || IsWin(key.Vk))
        {
            if (IsCtrl(key.Vk))  _ctrlHeld  = key.IsDown;
            if (IsAlt(key.Vk))   _altHeld   = key.IsDown;
            if (IsShift(key.Vk)) _shiftHeld = key.IsDown;
            if (IsWin(key.Vk))   _winHeld   = key.IsDown;

            Dispatcher.BeginInvoke(UpdateDisplay);
            return true;
        }

        if (!key.IsDown) return true;          // 일반 키는 눌림만 반영하고 입력은 여기서 소비

        Vk    = key.Vk;
        Ctrl  = _ctrlHeld;
        Alt   = _altHeld;
        Shift = _shiftHeld;
        Win   = _winHeld;

        Dispatcher.BeginInvoke(() =>
        {
            UpdateDisplay();
            BtnOk.IsEnabled = true;
        });
        return true;
    }

    private void UpdateDisplay()
    {
        if (Vk == 0)
        {
            var held = new List<string>(4);
            if (_ctrlHeld)  held.Add("Ctrl");
            if (_altHeld)   held.Add("Alt");
            if (_shiftHeld) held.Add("Shift");
            if (_winHeld)   held.Add("Win");

            TxtKey.Text = held.Count == 0 ? "키를 누르세요..." : string.Join(" + ", held) + " + ...";
            return;
        }
        TxtKey.Text = KeyNames.Combo(Vk, Ctrl, Alt, Shift, Win);
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        Vk = 0;
        Ctrl = Alt = Shift = Win = false;
        SyncHeldFromSystem();
        BtnOk.IsEnabled = false;
        UpdateDisplay();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}

/// <summary>캡처된 키 조합</summary>
public readonly record struct KeyCombo(int Vk, bool Ctrl, bool Alt, bool Shift, bool Win);
