namespace CharPad.Services;

public static class InputHelper
{
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const int INPUT_KEYBOARD = 1;
    private const int KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V       = 0x56;

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public INPUTUNION u; }

    [StructLayout(LayoutKind.Explicit)]
    struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT    mi;
        [FieldOffset(0)] public KEYBDINPUT    ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT  { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT  { public int dx, dy, mouseData; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    struct HARDWAREINPUT { public uint uMsg; public ushort wParamL, wParamH; }

    public static void PasteToWindow(IntPtr targetHwnd)
    {
        if (targetHwnd == IntPtr.Zero) return;
        SetForegroundWindow(targetHwnd);
        System.Threading.Thread.Sleep(50);

        SendInput(4, new[]
        {
            MakeKey(VK_CONTROL, 0),
            MakeKey(VK_V,       0),
            MakeKey(VK_V,       KEYEVENTF_KEYUP),
            MakeKey(VK_CONTROL, KEYEVENTF_KEYUP),
        }, Marshal.SizeOf<INPUT>());
    }

    private static INPUT MakeKey(ushort vk, uint flags) => new INPUT
    {
        type = INPUT_KEYBOARD,
        u    = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = flags } }
    };
}
