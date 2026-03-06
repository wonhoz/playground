using System.IO;
using System.Runtime.InteropServices;

namespace FileDuplicates.Services;

/// <summary>파일을 영구 삭제 대신 휴지통으로 보냅니다.</summary>
public static class RecycleBinHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr  hwnd;
        public uint    wFunc;
        [MarshalAs(UnmanagedType.LPTStr)] public string  pFrom;
        [MarshalAs(UnmanagedType.LPTStr)] public string? pTo;
        public ushort  fFlags;
        [MarshalAs(UnmanagedType.Bool)]   public bool    fAnyOperationsAborted;
        public IntPtr  hNameMappings;
        [MarshalAs(UnmanagedType.LPTStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

    private const uint   FO_DELETE        = 0x0003;
    private const ushort FOF_ALLOWUNDO    = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT       = 0x0004;

    public static void SendToRecycleBin(string path)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc  = FO_DELETE,
            pFrom  = path + '\0' + '\0',
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
        };
        int result = SHFileOperation(ref op);
        if (result != 0)
            throw new IOException($"휴지통으로 이동 실패 (오류 코드: 0x{result:X})");
    }
}
