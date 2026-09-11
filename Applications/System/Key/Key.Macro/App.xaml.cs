using KeyMacro.Services;

namespace KeyMacro;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 시간 변경만 하러 관리자 권한으로 다시 뜬 경우 — 창을 띄우지 않고 처리 후 종료
        if (SystemClock.HandleElevatedRequest(e.Args) is { } exitCode)
        {
            Shutdown(exitCode);
            return;
        }

        base.OnStartup(e);

        // 훅·입력 합성 중 처리하지 못한 예외로 앱이 조용히 죽는 것을 막는다
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"예기치 않은 오류가 발생했습니다.\n\n{args.Exception.Message}",
                "Key.Macro", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
