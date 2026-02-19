using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WorkspaceSwitcher.Dialogs;
using WorkspaceSwitcher.Models;
using WorkspaceSwitcher.Services;

namespace WorkspaceSwitcher;

public partial class MainWindow : Window
{
    private SwitcherSettings _settings;

    public MainWindow()
    {
        InitializeComponent();
        _settings = SettingsService.Load();
        Loaded += (_, _) => RebuildBoard();
    }

    // ── 보드 렌더링 ───────────────────────────────────────────────────────────

    private void RebuildBoard()
    {
        WorkspaceBoard.Children.Clear();

        if (_settings.Workspaces.Count == 0)
        {
            WorkspaceBoard.Children.Add(CreateEmptyHint());
            return;
        }

        foreach (var ws in _settings.Workspaces)
            WorkspaceBoard.Children.Add(CreateCard(ws));
    }

    private UIElement CreateCard(Workspace ws)
    {
        var bg = (Brush)new BrushConverter().ConvertFrom(ws.Color)!;

        // 이모지 + 이름
        var emoji = new TextBlock
        {
            Text = ws.Emoji, FontSize = 38,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 4)
        };
        var name = new TextBlock
        {
            Text = ws.Name, FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White, TextAlignment = TextAlignment.Center,
            MaxWidth = 140, TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var appCount = new TextBlock
        {
            Text = $"{ws.Apps.Count}개 앱",
            FontSize = 11, Opacity = 0.7, Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        };

        // 실행 버튼
        var launchBtn = new Button
        {
            Content = "▶ 실행", FontSize = 12, FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            Foreground = Brushes.White, BorderThickness = new Thickness(0),
            Padding = new Thickness(16, 5, 16, 5), Cursor = Cursors.Hand,
            Margin = new Thickness(0, 8, 0, 2)
        };
        launchBtn.Click += async (_, _) =>
        {
            launchBtn.IsEnabled = false;
            launchBtn.Content   = "실행 중...";
            await WorkspaceLauncher.LaunchAsync(ws);
            launchBtn.IsEnabled = true;
            launchBtn.Content   = "▶ 실행";
        };

        // 하단 편집/삭제 버튼 행
        var editBtn   = MakeIconBtn("✏", "편집",   () => EditWorkspace(ws));
        var deleteBtn = MakeIconBtn("🗑", "삭제",   () => DeleteWorkspace(ws));
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 6)
        };
        btnRow.Children.Add(editBtn);
        btnRow.Children.Add(deleteBtn);

        var stack = new StackPanel { Margin = new Thickness(10, 4, 10, 0) };
        stack.Children.Add(emoji);
        stack.Children.Add(name);
        stack.Children.Add(appCount);
        stack.Children.Add(launchBtn);
        stack.Children.Add(btnRow);

        var normalShadow = new DropShadowEffect { Color = Colors.Black, Opacity = 0.45, BlurRadius = 10, ShadowDepth = 3 };
        var hoverShadow  = new DropShadowEffect { Color = Colors.White, Opacity = 0.2,  BlurRadius = 14, ShadowDepth = 0 };

        var card = new Border
        {
            Width = 170, Margin = new Thickness(8),
            CornerRadius = new CornerRadius(12), Background = bg,
            Cursor = Cursors.Arrow,
            Effect = normalShadow,
            Child = stack
        };

        card.MouseEnter += (_, _) => card.Effect = hoverShadow;
        card.MouseLeave += (_, _) => card.Effect = normalShadow;

        return card;
    }

    private static Button MakeIconBtn(string icon, string tip, Action onClick)
    {
        var b = new Button
        {
            Content = icon, FontSize = 16, Width = 34, Height = 34,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, ToolTip = tip, Margin = new Thickness(3)
        };
        b.Click += (_, _) => onClick();
        return b;
    }

    private UIElement CreateEmptyHint()
    {
        return new TextBlock
        {
            Text = "+ 새 워크스페이스 버튼으로 첫 번째 워크스페이스를 만들어보세요!",
            FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(120, 200, 200, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Margin = new Thickness(40)
        };
    }

    // ── 워크스페이스 CRUD ─────────────────────────────────────────────────────

    private void AddWorkspace_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new EditWorkspaceDialog { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _settings.Workspaces.Add(dlg.Result);
        SettingsService.Save(_settings);
        RebuildBoard();
    }

    private void EditWorkspace(Workspace ws)
    {
        var dlg = new EditWorkspaceDialog(ws) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        var idx = _settings.Workspaces.FindIndex(w => w.Id == ws.Id);
        if (idx >= 0) _settings.Workspaces[idx] = dlg.Result;
        SettingsService.Save(_settings);
        RebuildBoard();
    }

    private void DeleteWorkspace(Workspace ws)
    {
        var res = MessageBox.Show($"'{ws.Name}' 워크스페이스를 삭제하시겠습니까?",
            "Workspace.Switcher", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        _settings.Workspaces.RemoveAll(w => w.Id == ws.Id);
        SettingsService.Save(_settings);
        RebuildBoard();
    }

    // ── 현재 실행 중인 앱으로 워크스페이스 생성 ──────────────────────────────

    private void CaptureApps_Click(object sender, RoutedEventArgs e)
    {
        var apps = WindowCapture.GetRunningApps();
        if (apps.Count == 0)
        {
            MessageBox.Show("실행 중인 앱 창을 찾을 수 없습니다.", "Workspace.Switcher",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ws = new Workspace
        {
            Name = "캡처된 워크스페이스",
            Emoji = "📸",
            Apps  = apps
        };

        var dlg = new EditWorkspaceDialog(ws) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _settings.Workspaces.Add(dlg.Result);
        SettingsService.Save(_settings);
        RebuildBoard();
    }

    // ── 종료 ──────────────────────────────────────────────────────────────────

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        SettingsService.Save(_settings);
    }
}
