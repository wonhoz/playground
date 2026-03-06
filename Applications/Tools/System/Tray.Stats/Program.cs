using System.Runtime.InteropServices;

namespace Tray.Stats;

static class Program
{
    [DllImport("uxtheme.dll", EntryPoint = "#135", CharSet = CharSet.Unicode)]
    static extern int SetPreferredAppMode(int mode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    static extern void FlushMenuThemes();

    static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        _mutex = new Mutex(true, "Tray.Stats_SingleInstance", out bool isNew);
        if (!isNew) return;

        SetPreferredAppMode(2);  // ForceDark
        FlushMenuThemes();

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
