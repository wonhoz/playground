using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Claude.Shell
{
    [ComVisible(true)]
    [Guid("261B2913-8ABA-420B-9280-0061626EDA5A")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("Claude.Shell.ClaudeContextMenuCommand")]
    public sealed class ClaudeContextMenuCommand : IShellExtInit, IContextMenu
    {
        // 서브메뉴 커맨드 ID 오프셋
        private const uint CMD_NORMAL    = 0;
        private const uint CMD_DANGEROUS = 1;
        private const uint CMD_COUNT     = 2;

        private string _folderPath;
        private IntPtr _hSubMenu = IntPtr.Zero;

        // ── IShellExtInit::Initialize ─────────────────────────────────────────
        public void Initialize(IntPtr pidlFolder, IDataObject pdtobj, IntPtr hkeyProgID)
        {
            _folderPath = null;

            // 1) IDataObject에서 경로 추출 (폴더 아이콘 우클릭)
            if (pdtobj != null)
            {
                try
                {
                    _folderPath = GetPathFromDataObject(pdtobj);
                }
                catch { }
            }

            // 2) PIDL에서 경로 추출 (폴더 빈 공간 우클릭)
            if (string.IsNullOrEmpty(_folderPath) && pidlFolder != IntPtr.Zero)
            {
                var sb = new StringBuilder(260);
                if (NativeMethods.SHGetPathFromIDListW(pidlFolder, sb))
                    _folderPath = sb.ToString();
            }
        }

        // ── IContextMenu::QueryContextMenu ────────────────────────────────────
        public int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags)
        {
            if ((uFlags & 0x000F) == 0x000F) return HR.S_OK; // CMF_DEFAULTONLY

            // 기존 서브메뉴 핸들 해제
            if (_hSubMenu != IntPtr.Zero) { NativeMethods.DestroyMenu(_hSubMenu); _hSubMenu = IntPtr.Zero; }

            // 서브메뉴 생성
            _hSubMenu = NativeMethods.CreatePopupMenu();
            NativeMethods.InsertMenuW(_hSubMenu, 0, MF.BYPOSITION | MF.STRING, (UIntPtr)(idCmdFirst + CMD_NORMAL),    "Claude Code 열기");
            NativeMethods.InsertMenuW(_hSubMenu, 1, MF.BYPOSITION | MF.STRING, (UIntPtr)(idCmdFirst + CMD_DANGEROUS), "Claude Code 열기 (권한 건너뜀)");

            // 루트 항목으로 서브메뉴 삽입
            NativeMethods.InsertMenuW(hmenu, indexMenu, MF.BYPOSITION | MF.POPUP, (UIntPtr)_hSubMenu.ToInt64(), "Claude Code에서 열기");

            return HR.S_OK | (int)CMD_COUNT; // 추가한 커맨드 수 반환
        }

        // ── IContextMenu::InvokeCommand ───────────────────────────────────────
        public int InvokeCommand(IntPtr pici)
        {
            var ici = Marshal.PtrToStructure<CMINVOKECOMMANDINFO>(pici);

            // lpVerb 상위 워드가 0이면 숫자 ID, 아니면 동사 문자열
            if ((ici.lpVerb.ToInt64() >> 16) != 0) return HR.E_FAIL;

            uint cmdId = (uint)(ici.lpVerb.ToInt64() & 0xFFFF);
            bool dangerous = (cmdId == CMD_DANGEROUS);

            string claudeArg = dangerous ? "claude --dangerously-skip-permissions" : "claude";
            string args      = string.IsNullOrEmpty(_folderPath)
                ? $"/k {claudeArg}"
                : $"/k cd /d \"{_folderPath}\" && {claudeArg}";

            Process.Start(new ProcessStartInfo("cmd.exe", args) { UseShellExecute = true });
            return HR.S_OK;
        }

        // ── IContextMenu::GetCommandString ────────────────────────────────────
        public int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax)
            => HR.E_NOTIMPL;

        // ── 헬퍼: IDataObject → 폴더 경로 ────────────────────────────────────
        private static string GetPathFromDataObject(IDataObject dataObject)
        {
            // CFSTR_SHELLIDLIST 또는 CF_HDROP 으로 경로 획득 시도
            var fmt = new FORMATETC
            {
                cfFormat = 15, // CF_HDROP
                ptd      = IntPtr.Zero,
                dwAspect = 1,  // DVASPECT_CONTENT
                lindex   = -1,
                tymed    = 4   // TYMED_HGLOBAL
            };

            dataObject.GetData(ref fmt, out STGMEDIUM stg);
            if (stg.unionmember == IntPtr.Zero) return null;

            try
            {
                IntPtr ptr = GlobalLock(stg.unionmember);
                if (ptr == IntPtr.Zero) return null;
                try
                {
                    var sb = new StringBuilder(260);
                    DragQueryFileW(ptr, 0, sb, sb.Capacity);
                    return sb.ToString();
                }
                finally { GlobalUnlock(stg.unionmember); }
            }
            finally { ReleaseStgMedium(ref stg); }
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] private static extern bool   GlobalUnlock(IntPtr hMem);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint DragQueryFileW(IntPtr hDrop, uint iFile, StringBuilder lpszFile, int cch);
        [DllImport("ole32.dll")]
        private static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);
    }

}
