using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace HexPeek;

public partial class CompareWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private readonly HexDocument _docA;
    private readonly HexDocument _docB;
    private bool _syncScroll = true;

    public CompareWindow(HexDocument docA, HexDocument docB)
    {
        _docA = docA;
        _docB = docB;

        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));

        TxtFileA.Text = System.IO.Path.GetFileName(_docA.FilePath);
        TxtFileB.Text = System.IO.Path.GetFileName(_docB.FilePath);

        ListA.ItemsSource = new HexRowList(_docA);
        ListB.ItemsSource = new HexRowList(_docB);

        // 차이 통계
        long diffCount = CountDifferences();
        long maxLen    = Math.Max(_docA.Length, _docB.Length);
        TxtCmpStatus.Text = $"파일 A: {_docA.Length:N0} bytes  |  파일 B: {_docB.Length:N0} bytes  |  " +
                            $"다른 바이트: {diffCount:N0} / {maxLen:N0}";
    }

    private long CountDifferences()
    {
        long min  = Math.Min(_docA.Length, _docB.Length);
        long diff = Math.Abs(_docA.Length - _docB.Length);
        for (long i = 0; i < min; i++)
            if (_docA.ReadByte(i) != _docB.ReadByte(i)) diff++;
        return diff;
    }

    // 우측 스크롤 → 좌측 동기화
    private void ListB_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        if (!_syncScroll) return;
        var svA = GetScrollViewer(ListA);
        svA?.ScrollToVerticalOffset(e.VerticalOffset);
    }

    private static System.Windows.Controls.ScrollViewer? GetScrollViewer(
        System.Windows.DependencyObject o)
    {
        if (o is System.Windows.Controls.ScrollViewer sv) return sv;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(o); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(o, i);
            var result = GetScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }
}
