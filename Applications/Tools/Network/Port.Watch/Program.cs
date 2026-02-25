using System.Runtime.InteropServices;

namespace PortWatch;

static class Program
{
    // Win32 네이티브 컨트롤(스크롤바 등) 다크 모드 강제 적용
    [DllImport("uxtheme.dll", EntryPoint = "#135", CharSet = CharSet.Unicode)]
    private static extern int SetPreferredAppMode(int preferredAppMode);

    [DllImport("uxtheme.dll", EntryPoint = "#136")]
    private static extern void FlushMenuThemes();

    [STAThread]
    static void Main()
    {
        SetPreferredAppMode(2); // 2 = ForceDark
        FlushMenuThemes();
        ApplicationConfiguration.Initialize();
        Application.Run(new MainWindow());
    }
}
