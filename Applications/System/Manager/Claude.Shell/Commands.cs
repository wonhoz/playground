using System;
using System.Diagnostics;
using System.IO;
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
        private const uint CMD_NORMAL    = 0;
        private const uint CMD_DANGEROUS = 1;
        private const uint CMD_COUNT     = 2;
        private const uint MIIM_BITMAP   = 0x80;

        private string _folderPath;
        private IntPtr _hSubMenu = IntPtr.Zero;

        // 아이콘 비트맵: DLL 수명 동안 공유 (한 번만 생성)
        private static IntPtr s_hBitmap    = IntPtr.Zero;
        private static bool   s_iconLoaded = false;

        // ── IShellExtInit::Initialize ─────────────────────────────────────────
        public void Initialize(IntPtr pidlFolder, IDataObject pdtobj, IntPtr hkeyProgID)
        {
            _folderPath = null;

            if (pdtobj != null)
                try { _folderPath = GetPathFromDataObject(pdtobj); } catch { }

            if (string.IsNullOrEmpty(_folderPath) && pidlFolder != IntPtr.Zero)
            {
                var sb = new StringBuilder(260);
                if (SHGetPathFromIDListW(pidlFolder, sb)) _folderPath = sb.ToString();
            }
        }

        // ── IContextMenu::QueryContextMenu ────────────────────────────────────
        public int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags)
        {
            if ((uFlags & 0x000F) == 0x000F) return HR.S_OK; // CMF_DEFAULTONLY

            if (_hSubMenu != IntPtr.Zero) { DestroyMenu(_hSubMenu); _hSubMenu = IntPtr.Zero; }

            _hSubMenu = CreatePopupMenu();
            InsertMenuW(_hSubMenu, 0, MF.BYPOSITION | MF.STRING, (UIntPtr)(idCmdFirst + CMD_NORMAL),    "Claude Code \xC5F4\xAE30");          // 열기
            InsertMenuW(_hSubMenu, 1, MF.BYPOSITION | MF.STRING, (UIntPtr)(idCmdFirst + CMD_DANGEROUS), "Claude Code \xC5F4\xAE30 (\xAD8C\xD55C \xAC74\xB108\xB871)"); // 열기 (권한 건너뜀)
            InsertMenuW(hmenu, indexMenu, MF.BYPOSITION | MF.POPUP, (UIntPtr)_hSubMenu.ToInt64(), "Claude Code\xC5D0\xC11C \xC5F4\xAE30"); // 에서 열기

            // 루트 항목에 아이콘 설정
            IntPtr hbmp = GetOrCreateIconBitmap();
            if (hbmp != IntPtr.Zero)
            {
                var mii = new MENUITEMINFO { cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(), fMask = MIIM_BITMAP, hbmpItem = hbmp };
                SetMenuItemInfoW(hmenu, indexMenu, true, ref mii);
            }

            return HR.S_OK | (int)CMD_COUNT;
        }

        // ── IContextMenu::InvokeCommand ───────────────────────────────────────
        public int InvokeCommand(IntPtr pici)
        {
            var ici = Marshal.PtrToStructure<CMINVOKECOMMANDINFO>(pici);
            if ((ici.lpVerb.ToInt64() >> 16) != 0) return HR.E_FAIL;

            uint cmdId    = (uint)(ici.lpVerb.ToInt64() & 0xFFFF);
            bool dangerous = (cmdId == CMD_DANGEROUS);
            string claudeArg = dangerous ? "claude --dangerously-skip-permissions" : "claude";
            string args = string.IsNullOrEmpty(_folderPath)
                ? $"/k {claudeArg}"
                : $"/k cd /d \"{_folderPath}\" && {claudeArg}";

            Process.Start(new ProcessStartInfo("cmd.exe", args) { UseShellExecute = true });
            return HR.S_OK;
        }

        // ── IContextMenu::GetCommandString ────────────────────────────────────
        public int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax)
            => HR.E_NOTIMPL;

        // ── 아이콘 비트맵 (16×16, 한 번만 생성) ──────────────────────────────
        private static IntPtr GetOrCreateIconBitmap()
        {
            if (s_iconLoaded) return s_hBitmap;
            s_iconLoaded = true;

            string iconSrc = FindClaudeIconSource();
            if (iconSrc == null) return IntPtr.Zero;

            IntPtr[] large = new IntPtr[1], small = new IntPtr[1];
            ExtractIconExW(iconSrc, 0, large, small, 1);

            IntPtr hIcon = small[0] != IntPtr.Zero ? small[0] : large[0];
            if (large[0] != IntPtr.Zero && large[0] != hIcon) DestroyIcon(large[0]);
            if (hIcon == IntPtr.Zero) return IntPtr.Zero;

            try
            {
                int size = 16;
                IntPtr hdcScr = GetDC(IntPtr.Zero);
                IntPtr hdcMem = CreateCompatibleDC(hdcScr);
                IntPtr hbmp   = CreateCompatibleBitmap(hdcScr, size, size);
                IntPtr hOld   = SelectObject(hdcMem, hbmp);
                DrawIconEx(hdcMem, 0, 0, hIcon, size, size, 0, IntPtr.Zero, 0x0003);
                SelectObject(hdcMem, hOld);
                DeleteDC(hdcMem);
                ReleaseDC(IntPtr.Zero, hdcScr);
                s_hBitmap = hbmp;
                return hbmp;
            }
            finally { DestroyIcon(hIcon); }
        }

        // Claude 아이콘 소스 파일 탐색 (exe → ico 순서)
        private static string FindClaudeIconSource()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string[] candidates = {
                // scoop / volta / mise 등 .local\bin 설치
                Path.Combine(userProfile, ".local", "bin", "claude.exe"),
                // Anthropic 공식 데스크톱 앱
                Path.Combine(local, "AnthropicClaude", "claude.exe"),
                // 일반 Programs 설치
                Path.Combine(local, "Programs", "claude", "claude.exe"),
                Path.Combine(local, "Programs", "Claude", "Claude.exe"),
            };
            foreach (string p in candidates) if (File.Exists(p)) return p;

            // npm global 경로: AppData\Roaming\npm\node_modules\@anthropic-ai\claude-code\resources\app.ico 등
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] npmCandidates = {
                Path.Combine(appData, "npm", "node_modules", "@anthropic-ai", "claude-code", "resources", "app.ico"),
                Path.Combine(appData, "npm", "node_modules", "@anthropic-ai", "claude-code", "resources", "icon.ico"),
            };
            foreach (string p in npmCandidates) if (File.Exists(p)) return p;

            return null;
        }

        // ── 헬퍼: IDataObject → 폴더 경로 ────────────────────────────────────
        private static string GetPathFromDataObject(IDataObject dataObject)
        {
            var fmt = new FORMATETC { cfFormat = 15, ptd = IntPtr.Zero, dwAspect = 1, lindex = -1, tymed = 4 };
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

        // ── P/Invoke ──────────────────────────────────────────────────────────
        [DllImport("user32.dll")]   static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll")]   static extern bool   DestroyMenu(IntPtr hMenu);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool InsertMenuW(IntPtr hMenu, uint uPos, uint uFlags, UIntPtr uIDNew, string lpNew);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool SetMenuItemInfoW(IntPtr hMenu, uint item, bool fByPos, ref MENUITEMINFO lpmii);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern uint ExtractIconExW(string file, int idx, IntPtr[] large, IntPtr[] small, uint n);
        [DllImport("user32.dll")]   static extern bool   DestroyIcon(IntPtr hIcon);
        [DllImport("user32.dll")]   static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")]   static extern int    ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")]    static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]    static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);
        [DllImport("gdi32.dll")]    static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")]    static extern bool   DeleteDC(IntPtr hdc);
        [DllImport("user32.dll")]   static extern bool   DrawIconEx(IntPtr hdc, int x, int y, IntPtr hIcon, int cx, int cy, uint step, IntPtr hbr, uint flags);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern bool SHGetPathFromIDListW(IntPtr pidl, StringBuilder sb);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] static extern bool   GlobalUnlock(IntPtr hMem);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern uint DragQueryFileW(IntPtr hDrop, uint i, StringBuilder sb, int cch);
        [DllImport("ole32.dll")]    static extern void   ReleaseStgMedium(ref STGMEDIUM stg);

        [StructLayout(LayoutKind.Sequential)]
        struct MENUITEMINFO
        {
            public uint   cbSize, fMask, fType, fState, wID;
            public IntPtr hSubMenu, hbmpChecked, hbmpUnchecked;
            public UIntPtr dwItemData;
            public IntPtr dwTypeData;
            public uint   cch;
            public IntPtr hbmpItem;
        }
    }
}
