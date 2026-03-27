using System;
using System.Runtime.InteropServices;

namespace Claude.Shell
{
    public static class SIGDN    { public const uint FILESYSPATH = 0x80058000; }
    public static class ECF      { public const uint DEFAULT = 0; public const uint HASSUBCOMMANDS = 1; }
    public static class ECS      { public const uint ENABLED = 0; }
    public static class HR       { public const int S_OK = 0; public const int S_FALSE = 1; public const int E_NOTIMPL = unchecked((int)0x80004001); }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    public interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppvOut);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
    public interface IShellItemArray
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppvOut);
        void GetPropertyStore(int flags, ref Guid riid, out IntPtr ppv);
        void GetPropertyDescriptionList(IntPtr keyType, ref Guid riid, out IntPtr ppv);
        void GetAttributes(uint dwAttribFlags, uint sfgaoMask, out uint psfgaoAttribs);
        void GetCount(out uint pdwNumItems);
        void GetItemAt(uint dwIndex, out IShellItem ppsi);
        void EnumItems(out IntPtr ppenumShellItems);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("a88826f8-2ea9-4d00-8af1-27a3b6e47a00")]
    public interface IEnumExplorerCommand
    {
        [PreserveSig] int Next(uint celt, out IExplorerCommand pUICommand, out uint pceltFetched);
        [PreserveSig] int Skip(uint celt);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumExplorerCommand ppenum);
    }

    // vtable 순서 (IUnknown 이후): GetTitle, GetIcon, GetToolTip, GetCanonicalName, GetState, Invoke, GetFlags, EnumSubCommands
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("a08ce4d0-fa25-44ab-b57c-c7240667d1a1")]
    public interface IExplorerCommand
    {
        [PreserveSig] int GetTitle(IShellItemArray psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        [PreserveSig] int GetIcon(IShellItemArray psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszIcon);
        [PreserveSig] int GetToolTip(IShellItemArray psiItemArray, [MarshalAs(UnmanagedType.LPWStr)] out string ppszInfotip);
        [PreserveSig] int GetCanonicalName(out Guid pguidCommandName);
        [PreserveSig] int GetState(IShellItemArray psiItemArray, bool fOkToBeSlow, out uint pCmdState);
        [PreserveSig] int Invoke(IShellItemArray psiItemArray, IntPtr pbc);
        [PreserveSig] int GetFlags(out uint pFlags);
        [PreserveSig] int EnumSubCommands(out IEnumExplorerCommand ppEnum);
    }
}
