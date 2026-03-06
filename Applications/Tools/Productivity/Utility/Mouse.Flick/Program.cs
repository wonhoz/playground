namespace MouseFlick;

internal static class Program
{
    // static 필드: GC 조기 해제 방지
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        _mutex = new Mutex(true, "MouseFlick_SingleInstance", out bool isNew);
        if (!isNew)
        {
            _mutex.Dispose();
            MessageBox.Show("Mouse.Flick가 이미 실행 중입니다.", "Mouse.Flick",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
