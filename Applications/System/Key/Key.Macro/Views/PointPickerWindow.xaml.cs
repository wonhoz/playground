using System.Windows.Threading;
using KeyMacro.Services;

namespace KeyMacro.Views;

/// <summary>화면 아무 곳이나 클릭해 그 좌표를 받아오는 지점 선택 오버레이</summary>
public partial class PointPickerWindow : Window
{
    private readonly MouseHook _mouse = new();
    private readonly KeyboardHook _keyboard = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };

    private PointPickerWindow()
    {
        InitializeComponent();

        _mouse.ButtonDown += OnButtonDown;
        _keyboard.KeyEvent += OnKey;
        _timer.Tick += (_, _) => UpdateCursor();

        Loaded += (_, _) =>
        {
            _mouse.Install();
            _keyboard.Install();
            _timer.Start();
            UpdateCursor();
        };

        Closed += (_, _) =>
        {
            _timer.Stop();
            _mouse.Dispose();
            _keyboard.Dispose();
        };
    }

    public int PickedX { get; private set; }
    public int PickedY { get; private set; }

    /// <summary>지점 선택을 띄우고 확정된 좌표를 돌려준다 (취소 시 false)</summary>
    public static bool TryPick(Window owner, out int x, out int y)
    {
        var picker = new PointPickerWindow { Owner = owner };
        DarkTitle.Apply(picker);
        PlaceAwayFromCursor(picker);

        bool ok = picker.ShowDialog() == true;
        x = picker.PickedX;
        y = picker.PickedY;
        return ok;
    }

    /// <summary>안내 창이 찍으려는 지점을 가리지 않도록 커서 반대편에 배치</summary>
    private static void PlaceAwayFromCursor(Window window)
    {
        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;

        NativeMethods.GetCursorPos(out var pt);
        bool cursorOnLeft = pt.X < NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN) / 2;

        window.Left = cursorOnLeft ? screenW - 360 : 40;
        window.Top  = screenH - 200;
    }

    private void UpdateCursor()
    {
        if (!NativeMethods.GetCursorPos(out var pt)) return;
        TxtX.Text = pt.X.ToString();
        TxtY.Text = pt.Y.ToString();
    }

    private bool OnButtonDown(int x, int y)
    {
        PickedX = x;
        PickedY = y;
        _mouse.SwallowNextUp();

        Dispatcher.BeginInvoke(() =>
        {
            DialogResult = true;
            Close();
        });
        return true;   // 대상 프로그램에는 클릭을 넘기지 않는다
    }

    private bool OnKey(HookedKey key)
    {
        if (!key.IsDown || key.Vk != MacroEngine.AbortVk) return false;

        Dispatcher.BeginInvoke(() =>
        {
            DialogResult = false;
            Close();
        });
        return true;
    }
}
