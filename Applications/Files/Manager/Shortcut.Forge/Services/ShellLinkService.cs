namespace ShortcutForge.Services;

[ComImport]
[Guid("00021401-0000-0000-C000-000000000046")]
internal class ShellLink;

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214F9-0000-0000-C000-000000000046")]
internal interface IShellLinkW
{
    void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile,
                 int cch, IntPtr pfd, uint fFlags);
    void GetIDList(out IntPtr ppidl);
    void SetIDList(IntPtr pidl);
    void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    void GetHotkey(out short pwHotkey);
    void SetHotkey(short wHotkey);
    void GetShowCmd(out int piShowCmd);
    void SetShowCmd(int iShowCmd);
    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath,
                         int cch, out int piIcon);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
    void Resolve(IntPtr hwnd, uint fFlags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

public static class ShellLinkService
{
    /// <summary>새 .lnk 파일을 생성합니다.</summary>
    public static void Create(ShortcutEntry entry)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(entry.TargetPath);
        if (!string.IsNullOrWhiteSpace(entry.Arguments))    link.SetArguments(entry.Arguments);
        if (!string.IsNullOrWhiteSpace(entry.WorkingDir))   link.SetWorkingDirectory(entry.WorkingDir);
        if (!string.IsNullOrWhiteSpace(entry.Description))  link.SetDescription(entry.Description);
        if (!string.IsNullOrWhiteSpace(entry.IconPath))     link.SetIconLocation(entry.IconPath, entry.IconIndex);

        var pf = (IPersistFile)link;
        pf.Save(entry.LnkPath, true);
    }

    /// <summary>기존 .lnk 파일을 읽어 ShortcutEntry를 채웁니다.</summary>
    public static ShortcutEntry Load(string lnkPath)
    {
        var link = (IShellLinkW)new ShellLink();
        var pf   = (IPersistFile)link;
        pf.Load(lnkPath, 0);

        var sb = new System.Text.StringBuilder(260);
        link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
        var target = sb.ToString();

        sb.Clear(); link.GetArguments(sb, sb.Capacity);
        var args = sb.ToString();

        sb.Clear(); link.GetWorkingDirectory(sb, sb.Capacity);
        var wd = sb.ToString();

        sb.Clear(); link.GetDescription(sb, sb.Capacity);
        var desc = sb.ToString();

        sb.Clear(); link.GetIconLocation(sb, sb.Capacity, out int iconIdx);
        var iconPath = sb.ToString();

        var status = ShortcutStatus.Ok;
        if (!string.IsNullOrEmpty(target) && !File.Exists(target) && !Directory.Exists(target))
            status = ShortcutStatus.BrokenTarget;
        else if (!string.IsNullOrEmpty(iconPath) && !File.Exists(iconPath))
            status = ShortcutStatus.BrokenIcon;

        return new ShortcutEntry
        {
            Name        = Path.GetFileNameWithoutExtension(lnkPath),
            LnkPath     = lnkPath,
            TargetPath  = target,
            Arguments   = args,
            WorkingDir  = wd,
            Description = desc,
            IconPath    = iconPath,
            IconIndex   = iconIdx,
            Status      = status
        };
    }

    /// <summary>기존 .lnk 파일을 업데이트합니다 (덮어쓰기).</summary>
    public static void Save(ShortcutEntry entry) => Create(entry);

    /// <summary>.lnk 파일을 삭제합니다.</summary>
    public static void Delete(string lnkPath)
    {
        if (File.Exists(lnkPath)) File.Delete(lnkPath);
    }
}
