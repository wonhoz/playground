using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SysClean.Services;
using SysClean.Views;

namespace SysClean;

public partial class MainWindow : Window
{
    private Button _activeNavBtn;

    public MainWindow()
    {
        InitializeComponent();
        _activeNavBtn = BtnNavCleaner;

        var v = Assembly.GetExecutingAssembly().GetName().Version;
        TbVersion.Text = v != null ? $"v{v.Major}.{v.Minor}.{v.Build}  ·  개발자 도구" : "개발자 도구";

        RestoreSettings();
        UpdateDiskInfo();
    }

    // ── 설정 복원 ──────────────────────────────────────────────────────
    private void RestoreSettings()
    {
        var s = SettingsService.Load();
        if (!double.IsNaN(s.Left) && !double.IsNaN(s.Top))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = s.Left;
            Top = s.Top;
        }
        Width = s.Width;
        Height = s.Height;

        // 마지막 탭 복원
        if (s.LastTab != "Cleaner")
        {
            var btn = s.LastTab switch
            {
                "Registry" => BtnNavRegistry,
                "Startup"  => BtnNavStartup,
                "Programs" => BtnNavPrograms,
                "History"  => BtnNavHistory,
                _          => null
            };
            if (btn != null)
                Nav_Click(btn, new RoutedEventArgs());
        }
    }

    // ── 설정 저장 ──────────────────────────────────────────────────────
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        var activeTag = _activeNavBtn.Tag?.ToString() ?? "Cleaner";
        SettingsService.Save(new AppSettings(Left, Top, Width, Height, activeTag));
    }

    // ── 디스크 상태 ────────────────────────────────────────────────────
    private void UpdateDiskInfo()
    {
        try
        {
            var drive = new DriveInfo("C");
            if (!drive.IsReady) return;

            long total = drive.TotalSize;
            long free = drive.AvailableFreeSpace;
            long used = total - free;
            double pct = total > 0 ? (double)used / total * 100 : 0;

            PbDisk.Value = pct;
            TbDiskUsage.Text = $"{pct:F0}%";
            TbDiskFree.Text = $"여유 {FormatSize(free)}  /  전체 {FormatSize(total)}";
        }
        catch { }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F0} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }

    // ── 키보드 단축키 ──────────────────────────────────────────────────
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.F1)
        {
            ShowHelp();
            e.Handled = true;
            return;
        }

        if (ViewCleaner.Visibility != Visibility.Visible) return;

        switch (e.Key)
        {
            case Key.F5:
                ViewCleaner.TriggerAnalyze();
                e.Handled = true;
                break;
            case Key.Delete when ViewCleaner.IsCleanEnabled:
                ViewCleaner.TriggerClean();
                e.Handled = true;
                break;
            case Key.A when (Keyboard.Modifiers & ModifierKeys.Control) != 0:
                ViewCleaner.TriggerSelectAll();
                e.Handled = true;
                break;
        }
    }

    // ── 도움말 ────────────────────────────────────────────────────────
    private void ShowHelp()
    {
        var dlg = new HelpDialog { Owner = this };
        dlg.ShowDialog();
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e) => ShowHelp();

    // ── 내비게이션 ────────────────────────────────────────────────────
    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag?.ToString();

        _activeNavBtn.Style = (Style)Resources["NavButton"];
        btn.Style = (Style)Resources["NavButtonActive"];
        _activeNavBtn = btn;

        ViewCleaner.Visibility  = Visibility.Collapsed;
        ViewRegistry.Visibility = Visibility.Collapsed;
        ViewStartup.Visibility  = Visibility.Collapsed;
        ViewPrograms.Visibility = Visibility.Collapsed;
        ViewHistory.Visibility  = Visibility.Collapsed;

        switch (tag)
        {
            case "Cleaner":  ViewCleaner.Visibility  = Visibility.Visible; break;
            case "Registry": ViewRegistry.Visibility = Visibility.Visible; break;
            case "Startup":  ViewStartup.Visibility  = Visibility.Visible; break;
            case "Programs": ViewPrograms.Visibility = Visibility.Visible; break;
            case "History":
                ViewHistory.Visibility = Visibility.Visible;
                ViewHistory.Refresh();
                UpdateDiskInfo();
                break;
        }
    }
}
