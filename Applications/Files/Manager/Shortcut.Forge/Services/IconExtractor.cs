namespace ShortcutForge.Services;

public static class IconExtractor
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex,
        IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbSFI, uint uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int    iIcon;
        public uint   dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON      = 0x000000100;
    private const uint SHGFI_SMALLICON = 0x000000001;
    private const uint SHGFI_LARGEICON = 0x000000000;

    /// <summary>파일/EXE에서 아이콘 이미지를 추출합니다. null이면 추출 실패.</summary>
    public static ImageSource? Extract(string filePath, int iconIndex = 0, bool large = false)
    {
        if (string.IsNullOrEmpty(filePath)) return null;

        // 직접 아이콘 추출 시도 (ExtractIconEx)
        var largeIcons  = large ? new IntPtr[1] : null;
        var smallIcons  = large ? null : new IntPtr[1];
        uint count = ExtractIconEx(filePath, iconIndex, largeIcons, smallIcons, 1);
        var hIcon = large
            ? (largeIcons != null && largeIcons[0] != IntPtr.Zero ? largeIcons[0] : IntPtr.Zero)
            : (smallIcons != null && smallIcons[0] != IntPtr.Zero ? smallIcons[0] : IntPtr.Zero);

        if (hIcon == IntPtr.Zero)
        {
            // SHGetFileInfo 폴백
            var sfi = new SHFILEINFO();
            SHGetFileInfo(filePath, 0, ref sfi, (uint)Marshal.SizeOf(sfi),
                          SHGFI_ICON | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON));
            hIcon = sfi.hIcon;
        }

        if (hIcon == IntPtr.Zero) return null;

        try
        {
            var img = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            img.Freeze();
            return img;
        }
        catch { return null; }
        finally { DestroyIcon(hIcon); }
    }
}
