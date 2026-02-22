using System.Windows;
using AmbientMixer.Models;
using AmbientMixer.Services;

namespace AmbientMixer;

public partial class App : Application
{
    private MixerService?  _mixer;
    private MixerSettings? _settings;
    private MainWindow?    _mainWindow;

    private System.Windows.Forms.NotifyIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 아이콘 생성
        var resDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        IconGenerator.Generate(resDir);

        // 설정 + 믹서 초기화
        _settings = SettingsService.Load();
        _mixer    = new MixerService();

        InitTray(resDir);

        _mainWindow = new MainWindow(_mixer, _settings);
        if (_settings.StartMinimized)
            _mainWindow.Hide();
        else
            _mainWindow.Show();
    }

    private void InitTray(string resDir)
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text    = "Ambient Mixer",
            Visible = true,
        };

        var iconPath = Path.Combine(resDir, IconGenerator.IconFileName);
        if (File.Exists(iconPath))
            _tray.Icon = new System.Drawing.Icon(iconPath);

        // 트레이 메뉴
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Renderer = new DarkMenuRenderer();

        var showItem  = menu.Items.Add("보이기 / 숨기기");
        var playItem  = menu.Items.Add("재생 / 일시정지");
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        var exitItem  = menu.Items.Add("종료");

        showItem.Click += (_, _) => ToggleWindow();
        playItem.Click += (_, _) => Dispatcher.Invoke(() => _mixer?.TogglePlay());
        exitItem.Click += (_, _) => ExitApp();

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ToggleWindow();

        _tray.ShowBalloonTip(
            2000,
            "Ambient Mixer",
            "앰비언트 사운드 믹서가 실행 중입니다.\n더블클릭하면 창을 열 수 있습니다.",
            System.Windows.Forms.ToolTipIcon.Info);
    }

    private void ToggleWindow()
    {
        Dispatcher.Invoke(() =>
        {
            if (_mainWindow == null) return;
            if (_mainWindow.IsVisible)
                _mainWindow.Hide();
            else
            {
                _mainWindow.Show();
                _mainWindow.Activate();
            }
        });
    }

    private void ExitApp()
    {
        if (_settings != null) SettingsService.Save(_settings);
        _tray?.Dispose();
        _mixer?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _mixer?.Dispose();
        base.OnExit(e);
    }
}
