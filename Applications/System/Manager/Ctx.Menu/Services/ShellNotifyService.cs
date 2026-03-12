namespace CtxMenu.Services;

public static class ShellNotifyService
{
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const int  SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST       = 0x0000;

    public static void NotifyAssocChanged() =>
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
}
