using System.Runtime.InteropServices;
using System.Windows.Interop;
using PdfForge.Views;

namespace PdfForge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private Button? _activeNav;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyDarkTitle();
        Loaded += (_, _) => Navigate(BtnMerge, new MergeView());
    }

    private void ApplyDarkTitle()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }

    private void NavBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        UserControl view = btn.Tag switch
        {
            "Merge"     => new MergeView(),
            "Split"     => new SplitView(),
            "Page"      => new PageView(),
            "Compress"  => new CompressView(),
            "Watermark" => new WatermarkView(),
            _           => new MergeView()
        };
        Navigate(btn, view);
    }

    private void Navigate(Button btn, UserControl view)
    {
        if (_activeNav != null)
        {
            _activeNav.Background = Brushes.Transparent;
            _activeNav.BorderBrush = Brushes.Transparent;
            _activeNav.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xF0));
        }
        _activeNav = btn;
        btn.Background  = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x3A));
        btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x50));
        btn.Foreground  = new SolidColorBrush(Color.FromRgb(0x5B, 0x8F, 0xFF));
        ContentArea.Child = view;
    }
}
