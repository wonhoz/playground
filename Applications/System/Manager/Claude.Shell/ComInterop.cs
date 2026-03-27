using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Claude.Shell
{
    // ── HRESULT ──────────────────────────────────────────────────────────────
    internal static class HR
    {
        public const int S_OK      = 0;
        public const int S_FALSE   = 1;
        public const int E_NOTIMPL = unchecked((int)0x80004001);
        public const int E_FAIL    = unchecked((int)0x80004005);
    }

    // ── Win32 메뉴 상수 ───────────────────────────────────────────────────────
    internal static class MF
    {
        public const uint BYPOSITION = 0x00000400;
        public const uint POPUP      = 0x00000010;
        public const uint STRING     = 0x00000000;
        public const uint SEPARATOR  = 0x00000800;
    }

    // ── Win32 API ─────────────────────────────────────────────────────────────
    internal static class NativeMethods
    {
        [DllImport("user32.dll")] public static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll")] public static extern bool   DestroyMenu(IntPtr hMenu);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool InsertMenuW(IntPtr hMenu, uint uPosition, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern bool SHGetPathFromIDListW(IntPtr pidl, StringBuilder pszPath);
    }

    // ── CMINVOKECOMMANDINFO (InvokeCommand 파라미터) ──────────────────────────
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct CMINVOKECOMMANDINFO
    {
        public uint   cbSize;
        public uint   fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int    nShow;
        public uint   dwHotKey;
        public IntPtr hIcon;
    }

    // ── OLE 구조체 ────────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    public struct FORMATETC
    {
        public short  cfFormat;
        public IntPtr ptd;
        public uint   dwAspect;
        public int    lindex;
        public uint   tymed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STGMEDIUM
    {
        public uint   tymed;
        public IntPtr unionmember;
        public IntPtr pUnkForRelease;
    }

    // ── IDataObject (OLE) ────────────────────────────────────────────────────
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010E-0000-0000-C000-000000000046")]
    public interface IDataObject
    {
        void GetData(ref FORMATETC pformatetcIn, out STGMEDIUM pmedium);
        void GetDataHere(ref FORMATETC pformatetc, ref STGMEDIUM pmedium);
        [PreserveSig] int QueryGetData(ref FORMATETC pformatetc);
        [PreserveSig] int GetCanonicalFormatEtc(ref FORMATETC pformatectIn, out FORMATETC pformatetcOut);
        void SetData(ref FORMATETC pformatetc, ref STGMEDIUM pmedium, bool fRelease);
        [PreserveSig] int EnumFormatEtc(uint dwDirection, out IntPtr ppenumFormatEtc);
        [PreserveSig] int DAdvise(ref FORMATETC pformatetc, uint advf, IntPtr pAdvSink, out uint pdwConnection);
        void DUnadvise(uint dwConnection);
        [PreserveSig] int EnumDAdvise(out IntPtr ppenumAdvise);
    }

    // ── IShellExtInit ─────────────────────────────────────────────────────────
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E8-0000-0000-C000-000000000046")]
    public interface IShellExtInit
    {
        void Initialize(IntPtr pidlFolder, IDataObject pdtobj, IntPtr hkeyProgID);
    }

    // ── IContextMenu ──────────────────────────────────────────────────────────
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E4-0000-0000-C000-000000000046")]
    public interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(IntPtr pici);
        [PreserveSig] int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
    }
}
