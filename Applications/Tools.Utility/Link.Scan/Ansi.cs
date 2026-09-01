using System.Runtime.InteropServices;

namespace LinkScan;

/// <summary>
/// ANSI 색상 상수. 콘솔이 VT 시퀀스를 지원하지 않거나 출력이 리다이렉트된 경우
/// 모든 상수가 빈 문자열이 되어 파일에 제어 문자가 섞이지 않는다.
/// </summary>
internal static class Ansi
{
    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    public static readonly bool Enabled = Initialize();

    public static string Reset => Enabled ? "\e[0m" : string.Empty;
    public static string Bold => Enabled ? "\e[1m" : string.Empty;
    public static string Dim => Enabled ? "\e[90m" : string.Empty;
    public static string Cyan => Enabled ? "\e[96m" : string.Empty;
    public static string Green => Enabled ? "\e[92m" : string.Empty;
    public static string Red => Enabled ? "\e[91m" : string.Empty;
    public static string Yellow => Enabled ? "\e[93m" : string.Empty;

    private static bool Initialize()
    {
        // 파이프·파일로 리다이렉트된 출력에는 색상을 넣지 않는다.
        if (Console.IsOutputRedirected)
            return false;

        if (Environment.GetEnvironmentVariable("NO_COLOR") is not null)
            return false;

        if (!OperatingSystem.IsWindows())
            return true;

        return TryEnableVirtualTerminal();
    }

    private static bool TryEnableVirtualTerminal()
    {
        try
        {
            var handle = GetStdHandle(StdOutputHandle);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                return false;

            if (!GetConsoleMode(handle, out var mode))
                return false;

            if ((mode & EnableVirtualTerminalProcessing) != 0)
                return true;

            return SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}
