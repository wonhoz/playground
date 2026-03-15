namespace VpnCast;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        _mutex = new Mutex(true, "VpnCast_SingleInstance", out bool isNew);
        if (!isNew)
        {
            _mutex.Dispose();
            MessageBox.Show("VPN.Cast가 이미 실행 중입니다.", "VPN.Cast",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApp());
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
