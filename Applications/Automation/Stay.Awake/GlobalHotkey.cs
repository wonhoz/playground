using System.Runtime.InteropServices;

namespace StayAwake
{
    /// <summary>
    /// 전역 단축키 등록 (RegisterHotKey API)
    /// NativeWindow의 WndProc에서 WM_HOTKEY를 수신해 등록된 핸들러 호출
    /// </summary>
    public class GlobalHotkey : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_NOREPEAT = 0x4000;

        private readonly HotkeyWindow _window;
        private readonly Dictionary<int, Action> _handlers = new();
        private int _nextId = 1;
        private bool _disposed;

        public GlobalHotkey()
        {
            _window = new HotkeyWindow();
        }

        /// <summary>
        /// 단축키 등록. 다른 앱이 이미 점유한 조합이면 false 반환.
        /// </summary>
        public bool Register(uint modifiers, Keys key, Action handler)
        {
            var id = _nextId++;
            if (!RegisterHotKey(_window.Handle, id, modifiers | MOD_NOREPEAT, (uint)key))
            {
                _nextId--;
                return false;
            }
            _handlers[id] = handler;
            _window.HotkeyPressed += OnHotkeyPressed;
            return true;
        }

        public void UnregisterAll()
        {
            foreach (var id in _handlers.Keys.ToList())
            {
                try { UnregisterHotKey(_window.Handle, id); } catch (Exception ex) { Logger.LogException("GlobalHotkey.Unregister", ex); }
            }
            _handlers.Clear();
        }

        private void OnHotkeyPressed(int id)
        {
            if (_handlers.TryGetValue(id, out var handler))
            {
                try { handler(); } catch (Exception ex) { Logger.LogException("GlobalHotkey.Handler", ex); }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            UnregisterAll();
            try { _window.DestroyHandle(); } catch { }
        }

        private class HotkeyWindow : NativeWindow
        {
            public event Action<int>? HotkeyPressed;

            public HotkeyWindow()
            {
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    HotkeyPressed?.Invoke(m.WParam.ToInt32());
                }
                base.WndProc(ref m);
            }
        }
    }
}
