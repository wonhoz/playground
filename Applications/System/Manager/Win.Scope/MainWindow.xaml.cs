using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WinScope.Services;

namespace WinScope;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(nint hWnd, uint crKey, byte bAlpha, uint dwFlags);
    [DllImport("user32.dll")] private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hWnd);

    private static readonly nint HWND_TOP = nint.Zero;
    private static readonly nint HWND_BOTTOM = new(1);
    private static readonly nint HWND_TOPMOST = new(-1);
    private static readonly nint HWND_NOTOPMOST = new(-2);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_LAYERED = 0x80000;
    private const uint LWA_ALPHA = 0x2;

    private List<WindowInfo> _allWindows = [];
    private WindowInfo? _selected;
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _suppressSlider = false;

    public MainWindow()
    {
        InitializeComponent();
        _refreshTimer.Tick += (_, _) => RefreshList();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int val = 1;
        DwmSetWindowAttribute(hwnd, 20, ref val, sizeof(int));

        // 아이콘: XAML TypeConverter 오류 방지를 위해 코드에서 로드
        try
        {
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                new Uri("pack://application:,,,/Resources/app.ico"));
        }
        catch { }

        RefreshList();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // ══════════════════════════════════════════════════════
    //  목록 관리
    // ══════════════════════════════════════════════════════

    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshList();

    private void RefreshList()
    {
        var selectedHandle = _selected?.Handle ?? nint.Zero;
        _allWindows = WindowEnumerator.GetWindows();
        ApplyFilter();

        // 선택 복원
        if (selectedHandle != nint.Zero)
        {
            var idx = WindowList.Items.Cast<WindowInfo>()
                                 .ToList().FindIndex(w => w.Handle == selectedHandle);
            if (idx >= 0) WindowList.SelectedIndex = idx;
        }
        LblWindowCount.Text = $"{_allWindows.Count}개 창";
        StatusBar.Text = $"마지막 갱신: {DateTime.Now:HH:mm:ss}";
    }

    private void ApplyFilter()
    {
        var query = TxtSearch.Text.Trim().ToLowerInvariant();
        var filtered = string.IsNullOrEmpty(query)
            ? _allWindows
            : _allWindows.Where(w =>
                w.DisplayTitle.ToLowerInvariant().Contains(query) ||
                w.ProcessName.ToLowerInvariant().Contains(query)).ToList();
        WindowList.ItemsSource = filtered;
        if (filtered.Count > 0)
            WindowList.DisplayMemberPath = "DisplayTitle";
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ChkAutoRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (ChkAutoRefresh.IsChecked == true) _refreshTimer.Start();
        else _refreshTimer.Stop();
    }

    // ══════════════════════════════════════════════════════
    //  창 선택 → 속성 로드
    // ══════════════════════════════════════════════════════

    private void WindowList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = WindowList.SelectedItem as WindowInfo;
        PropPanel.IsEnabled = _selected != null;
        if (_selected == null) return;
        LoadProperties(_selected);
    }

    private void LoadProperties(WindowInfo w)
    {
        LblTitle.Text = w.DisplayTitle;
        LblProcess.Text = $"{w.ProcessName} (PID {w.ProcessId})  Z={w.ZOrder}" +
                          (w.IsTopMost ? "  [항상 위]" : "");
        _suppressSlider = true;
        SliderOpacity.Value = (int)(w.Opacity / 255.0 * 100 + 0.5);
        LblOpacity.Text = $"{SliderOpacity.Value:0}%";
        ChkTopMost.IsChecked = w.IsTopMost;
        _suppressSlider = false;
    }

    // ══════════════════════════════════════════════════════
    //  투명도
    // ══════════════════════════════════════════════════════

    private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSlider || _selected == null || !IsLoaded) return;
        var pct = (int)SliderOpacity.Value;
        LblOpacity.Text = $"{pct}%";
        var alpha = (byte)(pct / 100.0 * 255);
        ApplyOpacity(_selected.Handle, alpha);
        _selected.Opacity = alpha;
    }

    private void ApplyOpacity(nint hWnd, byte alpha)
    {
        if (!IsWindow(hWnd)) return;
        var exStyle = (long)GetWindowLongPtr(hWnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_LAYERED) == 0)
        {
            SetWindowLongPtr(hWnd, GWL_EXSTYLE, new nint(exStyle | WS_EX_LAYERED));
            // WS_EX_LAYERED 추가 후 스타일 변경을 즉시 적용
            SetWindowPos(hWnd, nint.Zero, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }
        SetLayeredWindowAttributes(hWnd, 0, alpha, LWA_ALPHA);
    }

    // ══════════════════════════════════════════════════════
    //  항상 위
    // ══════════════════════════════════════════════════════

    private void ChkTopMost_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var topmost = ChkTopMost.IsChecked == true;
        var insertAfter = topmost ? HWND_TOPMOST : HWND_NOTOPMOST;
        SetWindowPos(_selected.Handle, insertAfter, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        _selected.IsTopMost = topmost;
        LblProcess.Text = $"{_selected.ProcessName} (PID {_selected.ProcessId})  Z={_selected.ZOrder}" +
                          (topmost ? "  [항상 위]" : "");
    }

    // ══════════════════════════════════════════════════════
    //  Z-Order 이동
    // ══════════════════════════════════════════════════════

    private void BtnZTop_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var handle = _selected.Handle;
        StatusBar.Text = $"맨 위로 이동: {_selected.DisplayTitle}";
        // 버튼 클릭으로 Win.Scope가 활성화된 후 처리해야 Z-order가 덮이지 않음
        // TOPMOST 설정 후 NOTOPMOST로 해제하면 비-topmost 창 중 최상위에 위치
        Dispatcher.InvokeAsync(() =>
        {
            if (!IsWindow(handle)) return;
            SetWindowPos(handle, HWND_TOPMOST,  0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
            SetWindowPos(handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void BtnZBottom_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var handle = _selected.Handle;
        StatusBar.Text = $"맨 아래로 이동: {_selected.DisplayTitle}";
        Dispatcher.InvokeAsync(() =>
        {
            if (!IsWindow(handle)) return;
            SetWindowPos(handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void BtnZUp_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        // 바로 위 창 위로 이동 (HWND_TOP 이동 후 refocus 방지)
        SetForegroundWindow(_selected.Handle);
        SetWindowPos(_selected.Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        StatusBar.Text = $"위로 이동: {_selected.DisplayTitle}";
    }

    private void BtnZDown_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var windows = WindowList.Items.Cast<WindowInfo>().ToList();
        var idx = windows.FindIndex(w => w.Handle == _selected.Handle);
        if (idx < windows.Count - 1)
        {
            var next = windows[idx + 1];
            SetWindowPos(_selected.Handle, next.Handle, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        }
        StatusBar.Text = $"아래로 이동: {_selected.DisplayTitle}";
    }

    // ══════════════════════════════════════════════════════
    //  창 상태
    // ══════════════════════════════════════════════════════

    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        ShowWindow(_selected.Handle, 9); // SW_RESTORE
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        ShowWindow(_selected.Handle, 6); // SW_MINIMIZE
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        if (MessageBox.Show($"'{_selected.DisplayTitle}' 창을 닫습니까?", "창 닫기",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        ShowWindow(_selected.Handle, 0); // SW_HIDE
        // WM_CLOSE 전송
        [DllImport("user32.dll")] static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);
        PostMessage(_selected.Handle, 0x0010, nint.Zero, nint.Zero); // WM_CLOSE
        Dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(500);
            RefreshList();
        });
    }

    private void BtnFocus_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        ShowWindow(_selected.Handle, 9);
        SetForegroundWindow(_selected.Handle);
    }
}
