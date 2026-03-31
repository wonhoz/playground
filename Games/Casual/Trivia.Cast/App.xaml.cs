namespace TriviaCast;

public partial class App : Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "TriviaCast_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("Trivia.Cast가 이미 실행 중입니다.", "Trivia.Cast", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
