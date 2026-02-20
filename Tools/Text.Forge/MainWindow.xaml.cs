using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using TextForge.Views;

namespace TextForge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private readonly JsonXmlView    _jsonXml    = new();
    private readonly Base64View     _base64     = new();
    private readonly UrlEncoderView _urlEncoder = new();
    private readonly HashView       _hash       = new();
    private readonly JwtView        _jwt        = new();
    private readonly RegexView      _regex      = new();
    private readonly TimestampView  _timestamp  = new();
    private readonly CaseView       _caseConv   = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바 적용
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            int value = 1;
            DwmSetWindowAttribute(source.Handle, 20, ref value, sizeof(int));
        }

        // 첫 번째 탭 선택 (JSON/XML)
        ToolNav.SelectedIndex = 0;
    }

    private void ToolNav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ContentArea.Content = ToolNav.SelectedIndex switch
        {
            0 => _jsonXml,
            1 => _base64,
            2 => _urlEncoder,
            3 => _hash,
            4 => _jwt,
            5 => _regex,
            6 => _timestamp,
            7 => _caseConv,
            _ => _jsonXml
        };
    }
}
