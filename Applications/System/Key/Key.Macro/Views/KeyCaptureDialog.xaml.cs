using KeyMacro.Models;
using KeyMacro.Services;

namespace KeyMacro.Views;

/// <summary>실제 키 입력을 그대로 받아 가상 키 코드로 기록하는 다이얼로그</summary>
public partial class KeyCaptureDialog : Window
{
    private readonly KeyboardHook _hook = new();

    private KeyCaptureDialog(string header, string hint)
    {
        InitializeComponent();
        TxtHeader.Text = header;
        TxtHint.Text = hint;

        _hook.KeyEvent += OnKey;

        Loaded      += (_, _) => _hook.Install();
        Activated   += (_, _) => _hook.Install();
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

    private bool OnKey(HookedKey key)
    {
        if (!key.IsDown) return true;          // 눌림만 반영하고 모든 키 입력은 여기서 소비

        if (KeyNames.IsModifier(key.Vk))
        {
            Dispatcher.BeginInvoke(UpdateDisplay);
            return true;
        }

        Vk    = key.Vk;
        Ctrl  = Down(0x11);
        Alt   = Down(0x12);
        Shift = Down(0x10);
        Win   = Down(0x5B) || Down(0x5C);

        Dispatcher.BeginInvoke(() =>
        {
            UpdateDisplay();
            BtnOk.IsEnabled = true;
        });
        return true;

        static bool Down(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    private void UpdateDisplay()
    {
        if (Vk == 0)
        {
            var held = new List<string>(4);
            if ((NativeMethods.GetAsyncKeyState(0x11) & 0x8000) != 0) held.Add("Ctrl");
            if ((NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0) held.Add("Alt");
            if ((NativeMethods.GetAsyncKeyState(0x10) & 0x8000) != 0) held.Add("Shift");
            if ((NativeMethods.GetAsyncKeyState(0x5B) & 0x8000) != 0) held.Add("Win");

            TxtKey.Text = held.Count == 0 ? "키를 누르세요..." : string.Join(" + ", held) + " + ...";
            return;
        }
        TxtKey.Text = KeyNames.Combo(Vk, Ctrl, Alt, Shift, Win);
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        Vk = 0;
        Ctrl = Alt = Shift = Win = false;
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
