using System.Threading;
using System.Windows;

namespace Stock.Catch;

public partial class App : Application
{
    // 사용자 세션 단위 단일 인스턴스 보장(Local\ 접두사 = 현재 세션).
    private const string MutexName = @"Local\Stock.Catch.SingleInstance";
    private const string ShowEventName = @"Local\Stock.Catch.ShowWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // 이미 실행 중 → 기존 인스턴스에 "창 띄우기" 신호를 보내고 이 인스턴스는 종료.
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); }
            catch { /* 신호 실패해도 중복 실행만 막으면 됨 */ }
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // 이후 다른 인스턴스가 보내는 신호를 받아 창을 띄우는 백그라운드 리스너.
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (_showEvent.WaitOne())
                Dispatcher.Invoke(ShowMainWindow);
        })
        { IsBackground = true, Name = "SingleInstanceListener" };
        listener.Start();

        var win = new MainWindow();
        MainWindow = win;
        win.Show();
    }

    /// <summary>트레이로 숨겨진(또는 최소화된) 메인 창을 다시 띄우고 활성화.</summary>
    private void ShowMainWindow()
    {
        if (MainWindow is not { } w) return;
        w.Show();
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Activate();
        w.Topmost = true;
        w.Topmost = false;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showEvent?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
