using System.Drawing;
using System.Windows.Forms;
using ServeCast.Views;
using WinApplication = System.Windows.Application;

namespace ServeCast;

public partial class App : WinApplication
{
    private NotifyIcon?         _tray;
    private ContextMenuStrip?   _menu;
    private MainWindow?         _mainWindow;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        InitTray();

        _mainWindow = new MainWindow();
        _mainWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            _mainWindow.Hide();
        };
        _mainWindow.Show();
    }

    private void InitTray()
    {
        _menu = new ContextMenuStrip
        {
            Renderer        = new DarkMenuRenderer(),
            AutoSize        = true,
            ShowImageMargin = false,
            Font            = new Font("Segoe UI", 9.5f),
            BackColor       = System.Drawing.Color.FromArgb(0x25, 0x25, 0x35),
            ForeColor       = System.Drawing.Color.FromArgb(0xCD, 0xD6, 0xF4),
            Padding         = new Padding(0, 4, 0, 4)
        };

        var openItem = new ToolStripMenuItem("Serve.Cast 열기");
        openItem.Click += (_, _) => ShowMain();

        var exitItem = new ToolStripMenuItem("종료");
        exitItem.Click += (_, _) => Quit();

        _menu.Items.Add(openItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(exitItem);

        _tray = new NotifyIcon
        {
            Text            = "Serve.Cast",
            Icon            = TryLoadIcon(),
            ContextMenuStrip = _menu,
            Visible         = true
        };

        _tray.DoubleClick += (_, _) => ShowMain();
    }

    private void ShowMain()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = System.Windows.WindowState.Normal;
        _mainWindow.Activate();
    }

    private void Quit()
    {
        _tray?.Dispose();
        _menu?.Dispose();
        _mainWindow?.Close();
        Shutdown();
    }

    private static Icon TryLoadIcon()
    {
        try
        {
            var path = System.IO.Path.Combine(
                AppContext.BaseDirectory, "Resources", "app.ico");
            if (System.IO.File.Exists(path))
                return new Icon(path);
        }
        catch { }

        // 폴백 아이콘
        return SystemIcons.Application;
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _tray?.Dispose();
        _menu?.Dispose();
        base.OnExit(e);
    }
}
