using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SysClean.Models;
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

    private static SolidColorBrush Hex(string hex)
    {
        var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        return new SolidColorBrush(c);
    }

    // ── 디스크 상태 (전체 드라이브) ──────────────────────────────────
    internal void UpdateDiskInfo()
    {
        DriveInfoPanel.Children.Clear();

        // 오늘 해제량 표시
        var todayCleaned = new CleanHistoryService()
            .Load()
            .Where(e => e.Time.Date == DateTime.Today)
            .Sum(e => e.CleanedBytes);
        TbTodaySaved.Text = todayCleaned > 0
            ? $"오늘 해제: {FormatSize(todayCleaned)}"
            : "";

        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList();

            foreach (var drive in drives)
            {
                try
                {
                    long total = drive.TotalSize;
                    long free = drive.AvailableFreeSpace;
                    long used = total - free;
                    double pct = total > 0 ? (double)used / total * 100 : 0;

                    string barColor = pct >= 90 ? "#EF5350" : pct >= 75 ? "#FFA726" : "#FF6B35";

                    var header = new System.Windows.Controls.Grid { Margin = new Thickness(0, 0, 0, 3) };
                    header.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    header.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

                    var labelBlock = new TextBlock { Text = drive.Name.TrimEnd('\\'), FontSize = 10, Foreground = Hex("#555") };
                    var pctBlock   = new TextBlock { Text = $"{pct:F0}%",             FontSize = 10, Foreground = Hex("#555") };
                    System.Windows.Controls.Grid.SetColumn(pctBlock, 1);
                    header.Children.Add(labelBlock);
                    header.Children.Add(pctBlock);

                    var pb = new System.Windows.Controls.ProgressBar
                    {
                        Height = 3, Value = pct, Maximum = 100,
                        Background = Hex("#2A2A2A"), Foreground = Hex(barColor),
                        BorderThickness = new Thickness(0)
                    };

                    var freeBlock = new TextBlock
                    {
                        Text = $"여유 {FormatSize(free)}  /  {FormatSize(total)}",
                        FontSize = 10, Foreground = Hex("#444"),
                        Margin = new Thickness(0, 2, 0, 6)
                    };

                    DriveInfoPanel.Children.Add(header);
                    DriveInfoPanel.Children.Add(pb);
                    DriveInfoPanel.Children.Add(freeBlock);
                }
                catch { /* 드라이브 접근 실패 무시 */ }
            }
        }
        catch { /* 드라이브 목록 조회 실패 무시 */ }
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
