using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Claude.Shell
{
    // ── 서브 커맨드 공통 베이스 ────────────────────────────────────────────────
    [ComVisible(false)]
    internal abstract class SubCommand : IExplorerCommand
    {
        protected abstract string Title     { get; }
        protected abstract string ClaudeArg { get; }   // "claude" or "claude --dangerously-skip-permissions"

        public int GetTitle(IShellItemArray _, out string name)       { name = Title;      return HR.S_OK; }
        public int GetIcon(IShellItemArray _, out string icon)        { icon = "cmd.exe,0"; return HR.S_OK; }
        public int GetToolTip(IShellItemArray _, out string tip)      { tip  = null;        return HR.E_NOTIMPL; }
        public int GetCanonicalName(out Guid guid)                    { guid = Guid.Empty;  return HR.E_NOTIMPL; }
        public int GetState(IShellItemArray _, bool slow, out uint s) { s = ECS.ENABLED;   return HR.S_OK; }
        public int GetFlags(out uint f)                               { f = ECF.DEFAULT;    return HR.S_OK; }
        public int EnumSubCommands(out IEnumExplorerCommand e)        { e = null;           return HR.E_NOTIMPL; }

        public int Invoke(IShellItemArray psiItemArray, IntPtr pbc)
        {
            string folder = GetFolderPath(psiItemArray);
            string args   = string.IsNullOrEmpty(folder)
                ? $"/k {ClaudeArg}"
                : $"/k cd /d \"{folder}\" && {ClaudeArg}";

            Process.Start(new ProcessStartInfo("cmd.exe", args) { UseShellExecute = true });
            return HR.S_OK;
        }

        private static string GetFolderPath(IShellItemArray psiItemArray)
        {
            try
            {
                psiItemArray.GetItemAt(0, out IShellItem item);
                item.GetDisplayName(SIGDN.FILESYSPATH, out string path);
                return path;
            }
            catch { return null; }
        }
    }

    [ComVisible(false)]
    internal sealed class NormalCommand : SubCommand
    {
        protected override string Title     => "Claude Code 열기";
        protected override string ClaudeArg => "claude";
    }

    [ComVisible(false)]
    internal sealed class DangerousCommand : SubCommand
    {
        protected override string Title     => "Claude Code 열기 (권한 건너뜀)";
        protected override string ClaudeArg => "claude --dangerously-skip-permissions";
    }

    // ── 루트 커맨드 (Windows 11 새 컨텍스트 메뉴에 표시) ──────────────────────
    [ComVisible(true)]
    [Guid("261B2913-8ABA-420B-9280-0061626EDA5A")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("Claude.Shell.ClaudeContextMenuCommand")]
    public sealed class ClaudeContextMenuCommand : IExplorerCommand
    {
        private static readonly IExplorerCommand[] _subCommands =
        {
            new NormalCommand(),
            new DangerousCommand(),
        };

        public int GetTitle(IShellItemArray _, out string name)       { name = "Claude Code에서 열기"; return HR.S_OK; }
        public int GetIcon(IShellItemArray _, out string icon)        { icon = "cmd.exe,0";             return HR.S_OK; }
        public int GetToolTip(IShellItemArray _, out string tip)      { tip  = null;                   return HR.E_NOTIMPL; }
        public int GetCanonicalName(out Guid guid)                    { guid = new Guid("261B2913-8ABA-420B-9280-0061626EDA5A"); return HR.S_OK; }
        public int GetState(IShellItemArray _, bool slow, out uint s) { s = ECS.ENABLED;  return HR.S_OK; }
        public int Invoke(IShellItemArray _, IntPtr pbc)              { return HR.E_NOTIMPL; }   // 서브메뉴가 있으므로 직접 호출 안 됨
        public int GetFlags(out uint f)                               { f = ECF.HASSUBCOMMANDS; return HR.S_OK; }

        public int EnumSubCommands(out IEnumExplorerCommand ppEnum)
        {
            ppEnum = new EnumExplorerCommand(_subCommands);
            return HR.S_OK;
        }
    }
}
