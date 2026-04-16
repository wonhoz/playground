using System.IO;
using System.Runtime.InteropServices;

namespace MarkView.Services;

// COM IFileDialog 기반 네이티브 폴더 선택기 (추가 패키지 불필요)
public static class FolderPicker
{
    public static string? Show(string? initialDir = null)
    {
        try
        {
            var dialog = (IFileOpenDialog)new NativeFileOpenDialog();
            dialog.SetOptions(FOS.PICKFOLDERS | FOS.FORCEFILESYSTEM | FOS.PATHMUSTEXIST);

            if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
            {
                SHCreateItemFromParsingName(initialDir, nint.Zero, ref _iidIShellItem, out var folder);
                if (folder != null) dialog.SetFolder(folder);
            }

            int hr = dialog.Show(nint.Zero);
            if (hr != 0) return null; // 취소

            dialog.GetResult(out var item);
            item.GetDisplayName(SIGDN.FILESYSPATH, out var path);
            return path;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath, nint pbc, ref Guid riid, out IShellItem? ppv);

    private static Guid _iidIShellItem = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");

    [Flags]
    private enum FOS : uint
    {
        FORCEFILESYSTEM = 0x00000040,
        PICKFOLDERS     = 0x00000020,
        PATHMUSTEXIST   = 0x00000800,
    }

    private enum SIGDN : uint
    {
        FILESYSPATH = 0x80058000,
    }

    [ComImport, ClassInterface(ClassInterfaceType.None),
     Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class NativeFileOpenDialog { }

    [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(nint parent);
        void SetFileTypes(uint cFileTypes, nint rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(nint pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(FOS fos);
        void GetOptions(out FOS pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(nint pFilter);
        void GetResults(out nint ppenum);
        void GetSelectedItems(out nint ppsai);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(nint pbc, ref Guid bhid, ref Guid riid, out nint ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }
}
