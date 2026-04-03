using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Prompt.Forge.Views;

public partial class SettingsDialog : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    readonly AppSettings _settings;
    public bool RestartRequired { get; private set; }

    public SettingsDialog(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    void OnLoaded(object s, RoutedEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        int v = 1;
        DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));

        TxtDbPath.Text  = _settings.DbPath;
        TxtPat.Text     = _settings.GithubPat;
        TxtGistId.Text  = _settings.GistId;
        TxtCurrentPath.Text = $"현재: {_settings.ResolvedDbPath}";
    }

    void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "DB 파일 위치 선택",
            Filter     = "SQLite DB (*.db)|*.db",
            FileName   = "prompts.db",
            DefaultExt = ".db"
        };
        if (!string.IsNullOrEmpty(TxtDbPath.Text))
            dlg.InitialDirectory = Path.GetDirectoryName(TxtDbPath.Text);

        if (dlg.ShowDialog() == true)
        {
            TxtDbPath.Text = dlg.FileName;
            TxtCurrentPath.Text = $"현재: {_settings.ResolvedDbPath}  →  변경 예정: {dlg.FileName}";
        }
    }

    void ResetDbPath_Click(object sender, RoutedEventArgs e)
    {
        TxtDbPath.Text = "";
        TxtCurrentPath.Text = $"현재: {_settings.ResolvedDbPath}";
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        var newDbPath = TxtDbPath.Text.Trim();
        RestartRequired = newDbPath != _settings.DbPath;

        _settings.DbPath    = newDbPath;
        _settings.GithubPat = TxtPat.Text.Trim();
        _settings.GistId    = TxtGistId.Text.Trim();
        _settings.Save();

        if (RestartRequired)
        {
            var r = MessageBox.Show(
                "DB 경로가 변경되었습니다.\n지금 앱을 재시작하면 적용됩니다.\n\n지금 재시작하시겠습니까?",
                "재시작 필요", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(
                    System.Diagnostics.Process.GetCurrentProcess()!.MainModule!.FileName!);
                Application.Current.Shutdown();
                return;
            }
        }

        DialogResult = true;
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
