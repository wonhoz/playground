using System.Runtime.InteropServices;
using PadForge.Services;
using PadForge.ViewModels;

namespace PadForge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly MainViewModel _vm;

    public MainWindow(ControllerService ctrl, ProfileService profile,
                      VirtualInputService virtualInput, ViGEmService vigem)
    {
        InitializeComponent();
        _vm = new MainViewModel(ctrl, profile, vigem);
        DataContext = _vm;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        EnableDarkTitleBar();
    }

    private void EnableDarkTitleBar()
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // 창 닫기 → 트레이로 최소화 (종료 아님)
        e.Cancel = true;
        Hide();
    }
}
