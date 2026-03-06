using System.Runtime.InteropServices;

namespace ClipboardStacker.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey  (IntPtr hWnd, int id, int fsModifiers, int vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WM_HOTKEY   = 0x0312;
    public const int MOD_ALT     = 0x0001;
    public const int MOD_CONTROL = 0x0002;
    public const int MOD_SHIFT   = 0x0004;
    public const int MOD_NOREPEAT = 0x4000;

    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, Action> _map = [];
    private int _nextId = 9100;

    public GlobalHotkeyService(IntPtr hwnd) => _hwnd = hwnd;

    public int Register(int mods, int vk, Action callback)
    {
        int id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, mods | MOD_NOREPEAT, vk)) return -1;
        _map[id] = callback;
        return id;
    }

    public void Unregister(int id)
    {
        if (_map.Remove(id))
            UnregisterHotKey(_hwnd, id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _map.Keys.ToList())
            UnregisterHotKey(_hwnd, id);
        _map.Clear();
    }

    public bool HandleMessage(IntPtr wParam)
    {
        int id = wParam.ToInt32();
        if (!_map.TryGetValue(id, out var cb)) return false;
        cb();
        return true;
    }

    public void Dispose() => UnregisterAll();
}
