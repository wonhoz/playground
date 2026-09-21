namespace AIPForge;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"예기치 않은 오류가 발생했습니다.\n\n{args.Exception.Message}",
                "AIP.Forge", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // 탐색기에서 .aip 파일을 끌어다 놓거나 "연결 프로그램"으로 연 경우
        var startFile = e.Args.FirstOrDefault(a => a.EndsWith(".aip", StringComparison.OrdinalIgnoreCase) && File.Exists(a));
        new MainWindow(startFile).Show();
    }
}
