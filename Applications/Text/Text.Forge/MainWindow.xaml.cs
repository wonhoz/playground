using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using TextForge.Views;

namespace TextForge;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private readonly JsonXmlView    _jsonXml   = new();
    private readonly EncodingView   _encoding  = new();
    private readonly HashView       _hash      = new();
    private readonly JwtView        _jwt       = new();
    private readonly RegexView      _regex     = new();
    private readonly TimestampView  _timestamp = new();
    private readonly CaseView       _caseConv  = new();
    private readonly GeneratorView  _generator = new();

    private static readonly string SettingsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "TextForge", "settings.json");

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바 적용
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            int value = 1;
            DwmSetWindowAttribute(source.Handle, 20, ref value, sizeof(int));
        }

        // 버전 표시
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = ver is not null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v2.0.0";

        // 마지막 탭 복원
        ToolNav.SelectedIndex = LoadLastTab();
    }

    private void ToolNav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ContentArea.Content = ToolNav.SelectedIndex switch
        {
            0 => _jsonXml,
            1 => _encoding,
            2 => _hash,
            3 => _jwt,
            4 => _regex,
            5 => _timestamp,
            6 => _caseConv,
            7 => _generator,
            _ => _jsonXml
        };
        SaveLastTab(ToolNav.SelectedIndex);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers == ModifierKeys.Control
            && e.Key >= Key.D1 && e.Key <= Key.D8)
        {
            ToolNav.SelectedIndex = e.Key - Key.D1;
            e.Handled = true;
            return;
        }
        if (e.Key == Key.F1)
        {
            ToggleHelp();
            e.Handled = true;
        }
        if (e.Key == Key.Escape && HelpOverlay.Visibility == Visibility.Visible)
        {
            HelpOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
    }

    private void HelpBtn_Click(object sender, RoutedEventArgs e) => ToggleHelp();
    private void HelpClose_Click(object sender, RoutedEventArgs e)
        => HelpOverlay.Visibility = Visibility.Collapsed;

    private void ToggleHelp()
    {
        HelpOverlay.Visibility = HelpOverlay.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    // ── 설정 저장/복원 ──────────────────────────────────────────

    private static int LoadLastTab()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return 0;
            var json = File.ReadAllText(SettingsPath);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("lastTab", out var val) ? val.GetInt32() : 0;
        }
        catch { return 0; }
    }

    private static void SaveLastTab(int index)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, $"{{\"lastTab\":{index}}}");
        }
        catch { }
    }
}
