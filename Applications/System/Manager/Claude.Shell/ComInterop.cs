using System;
using System.Runtime.InteropServices;

namespace Claude.Shell
{
    internal static class HR
    {
        public const int S_OK     = 0;
        public const int S_FALSE  = 1;
        public const int E_NOTIMPL = unchecked((int)0x80004001);
        public const int E_FAIL    = unchecked((int)0x80004005);
    }

    internal static class MF
    {
        public const uint BYPOSITION = 0x00000400;
        public const uint POPUP      = 0x00000010;
        public const uint STRING     = 0x00000000;
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct CMINVOKECOMMANDINFO
    {
        public uint   cbSize, fMask;
        public IntPtr hwnd, lpVerb, lpParameters, lpDirectory;
        public int    nShow;
        public uint   dwHotKey;
        public IntPtr hIcon;
    }

    // ── IDataObject ───────────────────────────────────────────────────────────
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010E-0000-0000-C000-000000000046")]
    public interface IDataObject
    {
        void GetData(ref FORMATETC fmt, out STGMEDIUM stg);
        void GetDataHere(ref FORMATETC fmt, ref STGMEDIUM stg);
        [PreserveSig] int QueryGetData(ref FORMATETC fmt);
        [PreserveSig] int GetCanonicalFormatEtc(ref FORMATETC fmtIn, out FORMATETC fmtOut);
        void SetData(ref FORMATETC fmt, ref STGMEDIUM stg, bool fRelease);
        [PreserveSig] int EnumFormatEtc(uint dir, out IntPtr ppenum);
        [PreserveSig] int DAdvise(ref FORMATETC fmt, uint advf, IntPtr pSink, out uint pdwConn);
        void DUnadvise(uint dwConn);
        [PreserveSig] int EnumDAdvise(out IntPtr ppenum);
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
