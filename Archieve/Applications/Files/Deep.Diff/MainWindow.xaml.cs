using System.Runtime.InteropServices;
using System.Windows.Interop;
using DeepDiff.Views;

namespace DeepDiff;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly List<TabEntry> _tabs = [];
    private TabEntry? _activeTab;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyDarkTitle();
        Loaded += (_, _) =>
        {
            var home = new TabEntry("🏠 홈", new HomeView(this), isHome: true);
            AddTab(home);
            ActivateTab(home);
        };
    }

    private void ApplyDarkTitle()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }

    // ─── 탭 관리 ───────────────────────────────────────────────

    public void OpenCompare(CompareMode mode, string? leftPath = null, string? rightPath = null)
    {
        UserControl view = mode switch
        {
            CompareMode.Folder    => new FolderCompareView(this, leftPath, rightPath),
            CompareMode.Text      => new TextCompareView(this, leftPath, rightPath),
            CompareMode.Image     => new ImageCompareView(this, leftPath, rightPath),
            CompareMode.Hex       => new HexCompareView(this, leftPath, rightPath),
            CompareMode.Clipboard => new ClipboardCompareView(this),
            _ => new HomeView(this)
        };

        string title = mode switch
        {
            CompareMode.Folder    => "📁 폴더 비교",
            CompareMode.Text      => "📄 텍스트 비교",
            CompareMode.Image     => "🖼 이미지 비교",
            CompareMode.Hex       => "🔢 HEX 비교",
            CompareMode.Clipboard => "📋 클립보드 비교",
            _ => "새 탭"
        };

        if (leftPath != null)
        {
            var name = Path.GetFileName(leftPath);
            if (!string.IsNullOrEmpty(name)) title = $"{ModeEmoji(mode)} {name}";
        }

        var tab = new TabEntry(title, view);
        AddTab(tab);
        ActivateTab(tab);
    }

    private static string ModeEmoji(CompareMode mode) => mode switch
    {
        CompareMode.Folder => "📁", CompareMode.Text => "📄",
        CompareMode.Image  => "🖼", CompareMode.Hex  => "🔢",
        _ => "📋"
    };

    private void AddTab(TabEntry tab)
    {
        _tabs.Add(tab);

        var btn = new TabButton(tab);
        btn.Clicked  += () => ActivateTab(tab);
        btn.Closed   += () => CloseTab(tab);
        tab.Button    = btn;

        TabStrip.Children.Add(btn);
    }

    public void ActivateTab(TabEntry tab)
    {
        _activeTab = tab;
        ContentArea.Child = tab.View;

        foreach (var t in _tabs)
            t.Button?.SetActive(t == tab);
    }

    public void CloseTab(TabEntry tab)
    {
        if (tab.IsHome) return;
        if (tab.View is ICloseable closeable && !closeable.CanClose()) return;
        int idx = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        TabStrip.Children.Remove(tab.Button);

        if (_activeTab == tab)
        {
            var next = _tabs.ElementAtOrDefault(Math.Max(0, idx - 1));
            if (next != null) ActivateTab(next);
        }
    }

    private void BtnNewTab_Click(object sender, RoutedEventArgs e)
    {
        // 홈 탭으로 이동 (홈에서 새 비교를 시작)
        var home = _tabs.FirstOrDefault(t => t.IsHome);
        if (home != null) ActivateTab(home);
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _activeTab != null && !_activeTab.IsHome)
        {
            CloseTab(_activeTab);
            e.Handled = true;
        }
    }

    public bool TryCloseTab(TabEntry tab)
    {
        if (tab.IsHome) return false;
        if (tab.View is ICloseable closeable && !closeable.CanClose()) return false;
        CloseTab(tab);
        return true;
    }

    // ─── 내부 클래스 ─────────────────────────────────────────

    public interface ICloseable
    {
        /// <summary>탭 닫기 전 확인. false 반환 시 닫기 취소.</summary>
        bool CanClose();
    }

    public class TabEntry(string title, UserControl view, bool isHome = false)
    {
        public string Title   { get; set; } = title;
        public UserControl View { get; } = view;
        public bool IsHome    { get; } = isHome;
        public TabButton? Button { get; set; }
    }
}

/// <summary>탭 스트립의 개별 탭 버튼</summary>
public class TabButton : Border
{
    public event Action? Clicked;
    public event Action? Closed;

    private readonly TextBlock _label;
    private bool _active;

    public TabButton(MainWindow.TabEntry tab)
    {
        Background     = Brushes.Transparent;
        BorderThickness = new(0, 0, 0, 2);
        BorderBrush    = Brushes.Transparent;
        Padding        = new(12, 8, 12, 8);
        Cursor         = Cursors.Hand;

        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        _label = new TextBlock
        {
            Text       = tab.Title,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0xAA)),
            FontSize   = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        sp.Children.Add(_label);

        if (!tab.IsHome)
        {
            var close = new TextBlock
            {
                Text       = "  ×",
                Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x6A)),
                FontSize   = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor     = Cursors.Hand
            };
            close.MouseEnter  += (_, _) => close.Foreground = new SolidColorBrush(Colors.White);
            close.MouseLeave  += (_, _) => close.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x6A));
            close.MouseLeftButtonDown += (_, e) => { e.Handled = true; Closed?.Invoke(); };
            sp.Children.Add(close);
        }

        Child = sp;

        MouseEnter  += (_, _) => { if (!_active) Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x3A)); };
        MouseLeave  += (_, _) => { if (!_active) Background = Brushes.Transparent; };
        MouseLeftButtonDown += (_, _) => Clicked?.Invoke();
        MouseDown   += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { e.Handled = true; Closed?.Invoke(); } };
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (active)
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x5B, 0x8F, 0xFF));
            _label.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xF0));
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x2A));
        }
        else
        {
            BorderBrush = Brushes.Transparent;
            _label.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0xAA));
            Background = Brushes.Transparent;
        }
    }
}
