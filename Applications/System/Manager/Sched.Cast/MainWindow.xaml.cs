using System.Runtime.InteropServices;
using System.Windows.Interop;
using Sched.Cast.Views;

namespace Sched.Cast;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    readonly MainViewModel _vm;

    public MainWindow()
    {
        _vm         = new MainViewModel();
        DataContext = _vm;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    void OnLoaded(object s, RoutedEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
        _vm.Refresh();
    }

    void NewTask_Click(object s, RoutedEventArgs e)
    {
        var dlg = new EditTaskWindow { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result == null) return;
        _vm.Register(dlg.Result);
        UpdateStatus();
    }

    void Run_Click(object s, RoutedEventArgs e)
    {
        _vm.RunSelected();
        UpdateStatus();
    }

    void Stop_Click(object s, RoutedEventArgs e)
    {
        _vm.StopSelected();
        UpdateStatus();
    }

    void Toggle_Click(object s, RoutedEventArgs e)
    {
        _vm.ToggleEnabled();
        UpdateStatus();
    }

    void Delete_Click(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show("선택한 작업을 삭제하시겠습니까?", "확인",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _vm.DeleteSelected();
        UpdateStatus();
    }

    void Refresh_Click(object s, RoutedEventArgs e)
    {
        _vm.Refresh();
        UpdateStatus();
    }

    void UpdateStatus()
    {
        // StatusText는 ViewModel에서 이미 업데이트됨
    }
}
