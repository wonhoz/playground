namespace WebShot;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // 미처리 예외 처리
        DispatcherUnhandledException += (s, ex) =>
        {
            MessageBox.Show($"오류: {ex.Exception.Message}", "Web.Shot",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };
    }
}
