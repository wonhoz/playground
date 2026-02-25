namespace DnsFlip;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        _mutex = new Mutex(true, "DnsFlip_SingleInstance", out bool isNew);
        if (!isNew)
        {
            _mutex.Dispose();
            MessageBox.Show("DNS.Flip이 이미 실행 중입니다.", "DNS.Flip",
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
