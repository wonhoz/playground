using System.Windows.Forms;
using PadForge.Services;
using PadForge.Views;

namespace PadForge;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private NotifyIcon? _trayIcon;
    private TrayService? _trayService;
    private ControllerService? _controllerService;
    private ProfileService? _profileService;
    private VirtualInputService? _virtualInputService;
    private ProcessWatchService? _processWatchService;
    private ViGEmService? _vigemService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 서비스 초기화
        _profileService = new ProfileService();
        _virtualInputService = new VirtualInputService();
        _vigemService = new ViGEmService();
        _controllerService = new ControllerService(_vigemService);
        _processWatchService = new ProcessWatchService(_profileService);

        // 메인 창 생성
        _mainWindow = new MainWindow(_controllerService, _profileService, _virtualInputService, _vigemService);

        // 트레이 서비스 초기화
        _trayService = new TrayService(_mainWindow, _controllerService);
        _trayService.Initialize();

        _mainWindow.Show();

        // 컨트롤러 서비스 시작
        _controllerService.Start(_virtualInputService);
        _processWatchService.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _processWatchService?.Stop();
        _controllerService?.Stop();
        _trayService?.Dispose();
        _vigemService?.Dispose();
        base.OnExit(e);
    }
}
