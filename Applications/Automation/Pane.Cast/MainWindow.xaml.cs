using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinForms = System.Windows.Forms;
// WPF 우선 using 별칭 (WinForms와 충돌 방지)
using Button         = System.Windows.Controls.Button;
using ComboBox       = System.Windows.Controls.ComboBox;
using TextBox        = System.Windows.Controls.TextBox;
using MessageBox     = System.Windows.MessageBox;
using Clipboard      = System.Windows.Clipboard;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Color          = System.Windows.Media.Color;
using Brushes        = System.Windows.Media.Brushes;
using FontFamily     = System.Windows.Media.FontFamily;
using ColorConverter = System.Windows.Media.ColorConverter;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment   = System.Windows.VerticalAlignment;

namespace PaneCast;

// ──────────────────────────────────────────────────────────────────────────────
// 데이터 모델
// ──────────────────────────────────────────────────────────────────────────────
public enum SplitDir { None, Right, Bottom }

public class PaneEntry : INotifyPropertyChanged
{
    private string _profile  = "PowerShell";
    private string _directory = "";
    private SplitDir _split  = SplitDir.None;
    private string _title    = "";

    public string Profile
    {
        get => _profile;
        set { _profile = value; OnPropChanged(); }
    }

    public string Directory
    {
        get => _directory;
        set { _directory = value; OnPropChanged(); }
    }

    public SplitDir Split
    {
        get => _split;
        set { _split = value; OnPropChanged(); }
    }

    [JsonIgnore]
    public bool IsFirstPane => Split == SplitDir.None;

    public string Title
    {
        get => _title;
        set { _title = value; OnPropChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class PaneTab : INotifyPropertyChanged
{
    private string _title = "Tab";

    public string Title
    {
        get => _title;
        set { _title = value; OnPropChanged(); }
    }

    public ObservableCollection<PaneEntry> Panes { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class SessionPreset : INotifyPropertyChanged
{
    private string _name = "새 세션";

    public string Name
    {
        get => _name;
        set { _name = value; OnPropChanged(); OnPropChanged(nameof(Description)); }
    }

    public ObservableCollection<PaneTab> Tabs { get; set; } = new();

    [JsonIgnore]
    public string Description
    {
        get
        {
            int tabCount  = Tabs.Count;
            int paneCount = Tabs.Sum(t => t.Panes.Count);
            return $"{tabCount}탭 / {paneCount}패인";
        }
    }

    public void RefreshDescription() => OnPropChanged(nameof(Description));

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// ──────────────────────────────────────────────────────────────────────────────
// 메인 윈도우
// ──────────────────────────────────────────────────────────────────────────────
public partial class MainWindow : Window
{
    private readonly ObservableCollection<SessionPreset> _sessions = new();
    private SessionPreset? _current;
    private WinForms.NotifyIcon? _tray;
    private bool _initialized;

    private static readonly string DataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PaneCast", "sessions.json");

    // 알려진 WT 프로파일 목록 (자동완성용)
    private static readonly string[] KnownProfiles =
    [
        "PowerShell", "Windows PowerShell", "Command Prompt",
        "Git Bash", "Ubuntu", "Debian", "WSL", "Azure Cloud Shell"
    ];

    public MainWindow()
    {
        InitializeComponent();
        SessionList.ItemsSource = _sessions;
    }

    // DWM 다크 타이틀바
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int val = 1;
        NativeMethods.DwmSetWindowAttribute(hwnd, 20, ref val, sizeof(int));
    }

    public void InitTray()
    {
        _tray = new WinForms.NotifyIcon
        {
            Text    = "Pane.Cast",
            Visible = true,
        };

        // 아이콘 설정
        var icoPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");
        if (File.Exists(icoPath))
            _tray.Icon = new System.Drawing.Icon(icoPath);

        _tray.DoubleClick += (_, _) => ShowMainWindow();
        _tray.BalloonTipTitle = "Pane.Cast";
        _tray.BalloonTipText  = "트레이에서 세션을 빠르게 실행하세요. 더블클릭하면 창이 열립니다.";
        _tray.ShowBalloonTip(3000);

        RebuildTrayMenu();
    }

    private void RebuildTrayMenu()
    {
        if (_tray == null) return;

        var menu = new WinForms.ContextMenuStrip
        {
            ShowImageMargin = false,
            AutoSize = true,
            Font = new System.Drawing.Font("Segoe UI", 9.5f),
            BackColor = System.Drawing.Color.FromArgb(30, 30, 55),
            ForeColor = System.Drawing.Color.FromArgb(220, 220, 220),
            Renderer = new DarkMenuRenderer(),
        };

        // 세션 항목
        foreach (var s in _sessions)
        {
            var session = s;
            var item = new WinForms.ToolStripMenuItem($"▶  {session.Name}")
            {
                ForeColor = System.Drawing.Color.FromArgb(200, 220, 200),
            };
            item.Click += (_, _) => LaunchSession(session);
            menu.Items.Add(item);
        }

        if (_sessions.Count > 0)
            menu.Items.Add(new WinForms.ToolStripSeparator());

        var openItem = new WinForms.ToolStripMenuItem("⬛  Pane.Cast 열기");
        openItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(openItem);

        var exitItem = new WinForms.ToolStripMenuItem("✖  종료");
        exitItem.Click += (_, _) => { _tray?.Dispose(); System.Windows.Application.Current.Shutdown(); };
        menu.Items.Add(exitItem);

        _tray.ContextMenuStrip = menu;
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSessions();
        _initialized = true;
        if (_sessions.Count > 0)
            SessionList.SelectedIndex = 0;
        else
            EditorPanel.IsEnabled = false;

        StatusBar.Text = "세션을 선택하거나 새 세션을 추가하세요.";
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    // ─── 세션 저장 / 불러오기 ──────────────────────────────────────────────────
    private void LoadSessions()
    {
        if (!File.Exists(DataPath)) { AddDefaultSession(); return; }
        try
        {
            var json = File.ReadAllText(DataPath);
            var list = JsonSerializer.Deserialize<List<SessionPreset>>(json);
            if (list == null || list.Count == 0) { AddDefaultSession(); return; }
            foreach (var s in list) _sessions.Add(s);
        }
        catch
        {
            AddDefaultSession();
        }
    }

    private void SaveSessions()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(DataPath, JsonSerializer.Serialize(_sessions.ToList(), opts), new UTF8Encoding(true));
    }

    private void AddDefaultSession()
    {
        var s = new SessionPreset { Name = "기본 세션" };
        var tab = new PaneTab { Title = "작업" };
        tab.Panes.Add(new PaneEntry { Profile = "PowerShell", Directory = "%USERPROFILE%" });
        s.Tabs.Add(tab);
        _sessions.Add(s);
        SaveSessions();
    }

    // ─── 세션 목록 이벤트 ──────────────────────────────────────────────────────
    private void SessionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _current = SessionList.SelectedItem as SessionPreset;
        EditorPanel.IsEnabled = _current != null;
        if (_current != null) RenderEditor();
    }

    private void BtnAddSession_Click(object sender, RoutedEventArgs e)
    {
        var s = new SessionPreset { Name = $"세션 {_sessions.Count + 1}" };
        var tab = new PaneTab { Title = "메인" };
        tab.Panes.Add(new PaneEntry { Profile = "PowerShell", Directory = "%USERPROFILE%" });
        s.Tabs.Add(tab);
        _sessions.Add(s);
        SessionList.SelectedItem = s;
        SaveSessions();
        RebuildTrayMenu();
    }

    private void BtnDeleteSession_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        if (MessageBox.Show($"'{_current.Name}' 세션을 삭제할까요?", "삭제 확인",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _sessions.Remove(_current);
        _current = null;
        EditorPanel.IsEnabled = false;
        TabPaneList.ItemsSource = null;
        TxtWtCommand.Text = "";
        SaveSessions();
        RebuildTrayMenu();
        StatusBar.Text = "세션이 삭제되었습니다.";
    }

    // ─── 편집기 렌더링 ──────────────────────────────────────────────────────────
    private void RenderEditor()
    {
        if (_current == null) return;
        TxtSessionName.Text = _current.Name;
        BuildTabPaneUI();
        UpdateWtCommand();
    }

    private void BuildTabPaneUI()
    {
        if (_current == null) return;
        var panel = new StackPanel();

        foreach (var (tab, ti) in _current.Tabs.Select((t, i) => (t, i)))
        {
            // 탭 헤더
            var tabHeader = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x2A)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x38)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 6),
            };
            var tabGrid = new Grid();
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tabLabel = new TextBlock
            {
                Text = $"탭 {ti + 1}",
                FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xCC)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            };
            Grid.SetColumn(tabLabel, 0);

            var tabNameBox = new TextBox
            {
                Text = tab.Title, FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x30)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x55)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 3, 4, 3),
                VerticalContentAlignment = VerticalAlignment.Center,
                CaretBrush = Brushes.White,
            };
            tabNameBox.TextChanged += (_, _) => { tab.Title = tabNameBox.Text; UpdateWtCommand(); };
            Grid.SetColumn(tabNameBox, 1);

            var tabIdxCapture = ti;
            var delTabBtn = MakeSmallBtn("✕ 탭 삭제", "#2A1A1A", "#6A3A3A");
            delTabBtn.Click += (_, _) =>
            {
                _current?.Tabs.RemoveAt(tabIdxCapture);
                BuildTabPaneUI();
                UpdateWtCommand();
                SaveSessions();
            };
            Grid.SetColumn(delTabBtn, 2);

            tabGrid.Children.Add(tabLabel);
            tabGrid.Children.Add(tabNameBox);
            tabGrid.Children.Add(delTabBtn);
            tabHeader.Child = tabGrid;
            panel.Children.Add(tabHeader);

            // 패인 목록
            foreach (var (pane, pi) in tab.Panes.Select((p, i) => (p, i)))
            {
                panel.Children.Add(BuildPaneRow(tab, pane, ti, pi));
            }

            // 이 탭에 패인 추가 버튼
            var addPaneBorder = new Border { Margin = new Thickness(20, 0, 0, 10) };
            var addPaneBtn = MakeSmallBtn($"+ 탭 {ti + 1}에 패인 추가", null, null);
            var tabCapture = tab;
            addPaneBtn.Click += (_, _) =>
            {
                tabCapture.Panes.Add(new PaneEntry { Profile = "PowerShell", Directory = "%USERPROFILE%", Split = SplitDir.Right });
                BuildTabPaneUI();
                UpdateWtCommand();
                SaveSessions();
            };
            addPaneBorder.Child = addPaneBtn;
            panel.Children.Add(addPaneBorder);
        }

        TabPaneList.ItemsSource = null;
        TabPaneList.Items.Clear();
        var scrollHost = new StackPanel();
        scrollHost.Children.Add(panel);
        TabPaneList.ItemsSource = new[] { panel };

        // ItemsControl 대신 ContentControl 사용
        var cc = new ContentControl { Content = panel };
        TabPaneList.ItemsSource = null;
        // 직접 패널로 대체
        var sv = TabPaneList.Parent as ScrollViewer;
        if (sv != null) sv.Content = panel;
    }

    private Border BuildPaneRow(PaneTab tab, PaneEntry pane, int tabIdx, int paneIdx)
    {
        var row = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x22)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x38)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(20, 0, 0, 4),
            Padding = new Thickness(8, 6, 8, 6),
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });        // 인덱스
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });    // 프로파일
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 디렉토리
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });    // 분할방향
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });        // 삭제

        var paneLabel = new TextBlock
        {
            Text = paneIdx == 0 ? "기본" : $"분할 {paneIdx}",
            FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0xAA)),
            VerticalAlignment = VerticalAlignment.Center,
            Width = 40,
        };
        Grid.SetColumn(paneLabel, 0);

        // 프로파일 ComboBox
        var profileBox = new ComboBox
        {
            IsEditable = true,
            Text = pane.Profile,
            Margin = new Thickness(6, 0, 6, 0),
            FontSize = 11,
        };
        foreach (var p in KnownProfiles) profileBox.Items.Add(p);
        profileBox.Text = pane.Profile;
        profileBox.SelectionChanged += (_, _) => { pane.Profile = profileBox.Text; UpdateWtCommand(); SaveSessions(); };
        profileBox.LostFocus         += (_, _) => { pane.Profile = profileBox.Text; UpdateWtCommand(); SaveSessions(); };
        Grid.SetColumn(profileBox, 1);

        // 디렉토리 TextBox
        var dirBox = new TextBox
        {
            Text = pane.Directory,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Margin = new Thickness(0, 0, 6, 0),
        };
        dirBox.TextChanged += (_, _) => { pane.Directory = dirBox.Text; UpdateWtCommand(); SaveSessions(); };
        Grid.SetColumn(dirBox, 2);

        // 분할 방향 ComboBox (첫 패인은 None으로 고정)
        var splitBox = new ComboBox { FontSize = 11 };
        splitBox.Items.Add("없음 (기본 패인)");
        splitBox.Items.Add("오른쪽 분할 (-V)");
        splitBox.Items.Add("아래 분할 (-H)");
        splitBox.SelectedIndex = paneIdx == 0 ? 0 : (int)pane.Split;
        splitBox.IsEnabled = paneIdx != 0;
        splitBox.SelectionChanged += (_, _) =>
        {
            pane.Split = (SplitDir)splitBox.SelectedIndex;
            UpdateWtCommand();
            SaveSessions();
        };
        Grid.SetColumn(splitBox, 3);

        // 삭제 버튼 (첫 패인은 비활성)
        var delBtn = MakeSmallBtn("✕", "#2A1A1A", "#6A3A3A");
        delBtn.IsEnabled = paneIdx != 0;
        var tabCapture  = tab;
        var paneCapture = pane;
        delBtn.Click += (_, _) =>
        {
            tabCapture.Panes.Remove(paneCapture);
            BuildTabPaneUI();
            UpdateWtCommand();
            SaveSessions();
        };
        Grid.SetColumn(delBtn, 4);

        grid.Children.Add(paneLabel);
        grid.Children.Add(profileBox);
        grid.Children.Add(dirBox);
        grid.Children.Add(splitBox);
        grid.Children.Add(delBtn);
        row.Child = grid;
        return row;
    }

    private static Button MakeSmallBtn(string text, string? bg, string? border)
    {
        var b = new Button
        {
            Content = text,
            FontSize = 10,
            Padding = new Thickness(6, 3, 6, 3),
            Margin  = new Thickness(6, 0, 0, 0),
            Cursor  = System.Windows.Input.Cursors.Hand,
            BorderThickness = new Thickness(1),
        };
        if (bg     != null) b.Background  = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
        if (border != null) b.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(border));

        var tmpl = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        factory.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        factory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        factory.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(contentFactory);
        tmpl.VisualTree = factory;
        b.Template = tmpl;
        return b;
    }

    // ─── 탭/패인 추가 버튼 ─────────────────────────────────────────────────────
    private void BtnAddTab_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        var tab = new PaneTab { Title = $"탭 {_current.Tabs.Count + 1}" };
        tab.Panes.Add(new PaneEntry { Profile = "PowerShell", Directory = "%USERPROFILE%" });
        _current.Tabs.Add(tab);
        BuildTabPaneUI();
        UpdateWtCommand();
        SaveSessions();
        _current.RefreshDescription();
    }

    private void BtnAddPane_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null || _current.Tabs.Count == 0) return;
        _current.Tabs.Last().Panes.Add(new PaneEntry
        {
            Profile   = "PowerShell",
            Directory = "%USERPROFILE%",
            Split     = SplitDir.Right,
        });
        BuildTabPaneUI();
        UpdateWtCommand();
        SaveSessions();
    }

    // ─── 세션 이름 편집 ──────────────────────────────────────────────────────────
    private void TxtSessionName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized || _current == null) return;
        _current.Name = TxtSessionName.Text;
        SaveSessions();
        RebuildTrayMenu();
    }

    // ─── wt 명령어 생성 ────────────────────────────────────────────────────────
    private void UpdateWtCommand()
    {
        if (_current == null) return;
        TxtWtCommand.Text = BuildWtCommand(_current);
    }

    private static string BuildWtCommand(SessionPreset session)
    {
        if (session.Tabs.Count == 0) return "wt";

        var parts = new List<string>();
        bool first = true;

        foreach (var tab in session.Tabs)
        {
            foreach (var pane in tab.Panes)
            {
                var profile = string.IsNullOrWhiteSpace(pane.Profile) ? "PowerShell" : pane.Profile;
                var dir     = string.IsNullOrWhiteSpace(pane.Directory) ? "%USERPROFILE%" : pane.Directory;
                var dirArg  = $"--startingDirectory \"{dir}\"";
                var profArg = $"--profile \"{profile}\"";

                if (first)
                {
                    var titleArg = string.IsNullOrWhiteSpace(tab.Title) ? "" : $"--title \"{tab.Title}\" ";
                    parts.Add($"new-tab {titleArg}{profArg} {dirArg}");
                    first = false;
                }
                else if (pane.Split == SplitDir.None)
                {
                    var titleArg = string.IsNullOrWhiteSpace(tab.Title) ? "" : $"--title \"{tab.Title}\" ";
                    parts.Add($"new-tab {titleArg}{profArg} {dirArg}");
                }
                else
                {
                    var splitFlag = pane.Split == SplitDir.Right ? "-V" : "-H";
                    parts.Add($"split-pane {splitFlag} {profArg} {dirArg}");
                }
            }
        }

        return "wt " + string.Join(" ; ", parts);
    }

    // ─── 세션 실행 ─────────────────────────────────────────────────────────────
    private void BtnLaunch_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        LaunchSession(_current);
    }

    private void LaunchSession(SessionPreset session)
    {
        var cmd = BuildWtCommand(session);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName  = "cmd.exe",
                Arguments = $"/c {cmd}",
                UseShellExecute = true,
            });
            StatusBar.Text = $"세션 '{session.Name}' 실행 완료.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"실행 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCopyCmd_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtWtCommand.Text))
        {
            Clipboard.SetText(TxtWtCommand.Text);
            StatusBar.Text = "wt 명령어가 클립보드에 복사되었습니다.";
        }
    }

    // ─── 가져오기 / 내보내기 ──────────────────────────────────────────────────
    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "JSON 파일|*.json", FileName = "panecast_sessions" };
        if (dlg.ShowDialog() != true) return;
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(_sessions.ToList(), opts), new UTF8Encoding(true));
        StatusBar.Text = $"내보내기 완료: {dlg.FileName}";
    }

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "JSON 파일|*.json" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var list = JsonSerializer.Deserialize<List<SessionPreset>>(json);
            if (list == null) { StatusBar.Text = "파일 파싱 오류."; return; }
            foreach (var s in list) _sessions.Add(s);
            SaveSessions();
            RebuildTrayMenu();
            StatusBar.Text = $"{list.Count}개 세션을 가져왔습니다.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"가져오기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// 트레이 다크 렌더러
// ──────────────────────────────────────────────────────────────────────────────
internal class DarkMenuRenderer : WinForms.ToolStripRenderer
{
    protected override void OnRenderToolStripBackground(WinForms.ToolStripRenderEventArgs e)
    {
        using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(22, 22, 42));
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderMenuItemBackground(WinForms.ToolStripItemRenderEventArgs e)
    {
        var color = e.Item.Selected
            ? System.Drawing.Color.FromArgb(50, 50, 80)
            : System.Drawing.Color.FromArgb(22, 22, 42);
        using var brush = new System.Drawing.SolidBrush(color);
        e.Graphics.FillRectangle(brush, new System.Drawing.Rectangle(System.Drawing.Point.Empty, e.Item.Size));
    }

    protected override void OnRenderSeparator(WinForms.ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(50, 50, 70));
        int y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    protected override void OnRenderItemText(WinForms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = System.Drawing.Color.FromArgb(220, 220, 220);
        base.OnRenderItemText(e);
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// DWM
// ──────────────────────────────────────────────────────────────────────────────
internal static partial class NativeMethods
{
    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(
        nint hwnd, int attr, ref int attrValue, int attrSize);
}
