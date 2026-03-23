using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace SoundBoard.Services;

/// <summary>RegisterHotKey 기반 전역 단축키 관리. MainWindow Loaded 후 초기화하세요.</summary>
public class GlobalHotkeyService : IDisposable
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WM_HOTKEY = 0x0312;
    // 수식키 상수 (user32.dll MOD_*)
    public const int MOD_ALT     = 0x0001;
    public const int MOD_CONTROL = 0x0002;
    public const int MOD_SHIFT   = 0x0004;
    public const int MOD_NOREPEAT = 0x4000;

    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, Action> _map = [];
    private int _nextId = 0xC000;

    public GlobalHotkeyService(IntPtr hwnd) => _hwnd = hwnd;

    /// <summary>단축키를 등록하고 등록 ID를 반환합니다. 실패 시 -1.</summary>
    public int Register(int mods, int vk, Action callback)
    {
        if (vk == 0) return -1;
        var id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, (uint)(mods | MOD_NOREPEAT), (uint)vk)) return -1;
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
        foreach (var id in _map.Keys)
            UnregisterHotKey(_hwnd, id);
        _map.Clear();
    }

    /// <summary>WndProc에서 WM_HOTKEY 메시지를 전달하세요.</summary>
    public bool HandleMessage(IntPtr wParam)
    {
        int id = wParam.ToInt32();
        if (_map.TryGetValue(id, out var cb)) { cb(); return true; }
        return false;
    }

    public void Dispose() => UnregisterAll();
}
