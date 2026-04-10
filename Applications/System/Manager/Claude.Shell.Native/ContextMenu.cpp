#include "ContextMenu.h"
#include <shlwapi.h>
#include <shellapi.h>
#include <winreg.h>
#include <new>
#include <objbase.h>

static const UINT CMD_NORMAL    = 0;
static const UINT CMD_DANGEROUS = 1;
static const UINT CMD_COUNT     = 2;

HBITMAP   ClaudeContextMenu::s_hBitmap  = nullptr;
INIT_ONCE ClaudeContextMenu::s_initOnce = INIT_ONCE_STATIC_INIT;

ClaudeContextMenu::ClaudeContextMenu(bool dangerous)
    : m_cRef(1), m_dangerous(dangerous), m_fileArgCount(0), m_extraDirCount(0), m_hSubMenu(nullptr)
{
    InterlockedIncrement(&g_cDllRef);
}
ClaudeContextMenu::~ClaudeContextMenu()
{
    if (m_hSubMenu) { DestroyMenu(m_hSubMenu); m_hSubMenu = nullptr; }
    InterlockedDecrement(&g_cDllRef);
}

// ── 공유 유틸: claude.exe 설치 경로 반환 (없으면 빈 문자열) ──────────────────
static std::wstring GetClaudeExePath()
{
    WCHAR buf[MAX_PATH] = L"claude.exe";
    if (PathFindOnPathW(buf, nullptr)) return buf;

    WCHAR userProfile[MAX_PATH] = {}, localAppData[MAX_PATH] = {};
    ExpandEnvironmentStringsW(L"%USERPROFILE%",  userProfile,  MAX_PATH);
    ExpandEnvironmentStringsW(L"%LOCALAPPDATA%", localAppData, MAX_PATH);

    struct { const wchar_t* base; const wchar_t* rel; } exes[] = {
        { userProfile,  L".local\\bin\\claude.exe"         },
        { localAppData, L"AnthropicClaude\\claude.exe"     },
        { localAppData, L"Programs\\claude\\claude.exe"    },
        { localAppData, L"Programs\\Claude\\Claude.exe"    },
    };
    for (auto& e : exes)
    {
        WCHAR path[MAX_PATH] = {};
        PathCombineW(path, e.base, e.rel);
        if (PathFileExistsW(path)) return path;
    }
    return {};
}

bool FindClaudeExe() { return !GetClaudeExePath().empty(); }

// ── 공유 유틸: Windows Terminal(wt.exe) 탐색 ─────────────────────────────────
bool FindWindowsTerminal()
{
    WCHAR buf[MAX_PATH] = L"wt.exe";
    if (PathFindOnPathW(buf, nullptr)) return true;

    WCHAR localAppData[MAX_PATH] = {};
    ExpandEnvironmentStringsW(L"%LOCALAPPDATA%", localAppData, MAX_PATH);
    WCHAR path[MAX_PATH] = {};
    PathCombineW(path, localAppData, L"Microsoft\\WindowsApps\\wt.exe");
    return PathFileExistsW(path) == TRUE;
}

// ── 공유 유틸: PowerShell 7(pwsh.exe) 탐색 ───────────────────────────────────
bool FindPowerShell7()
{
    WCHAR buf[MAX_PATH] = L"pwsh.exe";
    if (PathFindOnPathW(buf, nullptr)) return true;

    WCHAR programFiles[MAX_PATH] = {}, programFilesX86[MAX_PATH] = {};
    ExpandEnvironmentStringsW(L"%ProgramFiles%",      programFiles,    MAX_PATH);
    ExpandEnvironmentStringsW(L"%ProgramFiles(x86)%", programFilesX86, MAX_PATH);

    struct { const wchar_t* base; const wchar_t* rel; } paths[] = {
        { programFiles,    L"PowerShell\\7\\pwsh.exe"   },
        { programFiles,    L"PowerShell\\7-preview\\pwsh.exe" },
        { programFilesX86, L"PowerShell\\7\\pwsh.exe"   },
    };
    for (auto& p : paths)
    {
        WCHAR full[MAX_PATH] = {};
        PathCombineW(full, p.base, p.rel);
        if (PathFileExistsW(full)) return true;
    }
    return false;
}

// ── 공유 유틸: 레지스트리 HKCU\Software\ClaudeCode 문자열 값 읽기 ─────────────
static std::wstring GetRegistryString(const wchar_t* name)
{
    WCHAR buf[MAX_PATH] = {};
    DWORD sz = sizeof(buf);
    if (RegGetValueW(HKEY_CURRENT_USER, L"Software\\ClaudeCode", name,
                     RRF_RT_REG_SZ, nullptr, buf, &sz) == ERROR_SUCCESS && buf[0])
        return buf;
    return {};
}

// ── 공유 유틸: NewTab 플래그 (HKCU\Software\ClaudeCode\NewTab DWORD) ──────────
static bool GetNewTabFlag()
{
    DWORD val = 0, sz = sizeof(val);
    RegGetValueW(HKEY_CURRENT_USER, L"Software\\ClaudeCode", L"NewTab",
                 RRF_RT_REG_DWORD, nullptr, &val, &sz);
    return val != 0;
}

// ── 레지스트리 캐시 — LaunchClaude 매 호출 시 3회 읽기 방지 ──────────────────
struct RegCache {
    std::wstring terminalPath;
    std::wstring terminalType;
    bool         newTab  = false;
    bool         loaded  = false;
};
static RegCache& GetRegCache()
{
    static RegCache s;
    if (!s.loaded)
    {
        s.terminalPath = GetRegistryString(L"TerminalPath");
        s.terminalType = GetRegistryString(L"TerminalType");
        s.newTab       = GetNewTabFlag();
        s.loaded       = true;
    }
    return s;
}

// ── PowerShell -Command 인자 내 큰따옴표 이스케이프 ───────────────────────────
// "-Command \"...\"" 내부에 이미 큰따옴표가 있으면 \" 로 이스케이프해야 함
static std::wstring EscapeForPwshCommand(const std::wstring& s)
{
    std::wstring r;
    r.reserve(s.size() + 8);
    for (wchar_t c : s)
    {
        if (c == L'"') r += L"\\\"";
        else            r += c;
    }
    return r;
}

// ── 공유 유틸: Claude 실행 ────────────────────────────────────────────────────
// fileArgs: "@file1.txt @file2.cpp" 형식의 파일 인자 (없으면 빈 문자열)
static void LaunchClaude(const std::wstring& folder, bool dangerous,
                         const std::wstring& fileArgs = {})
{
    if (!FindClaudeExe())
    {
        // 브라우저로 다운로드 페이지 열기
        ShellExecuteW(nullptr, L"open",
                      L"https://claude.ai/download",
                      nullptr, nullptr, SW_SHOWNORMAL);
        return;
    }

    // claude 기본 명령 + 파일 인자 조합
    std::wstring claudeCmd = dangerous
        ? L"claude --dangerously-skip-permissions"
        : L"claude";
    if (!fileArgs.empty())
        claudeCmd += L" " + fileArgs;

    // CommandLineToArgvW 호환: 닫는 " 직전 \ 가 있으면 \\ 로 이스케이프
    // ex) "C:\" -> "C:\\" (드라이브 루트 trailing backslash 처리)
    std::wstring quotedFolder = folder;
    if (!quotedFolder.empty() && quotedFolder.back() == L'\\')
        quotedFolder += L'\\';

    SHELLEXECUTEINFOW sei = {};
    sei.cbSize = sizeof(sei);
    sei.lpVerb = L"open";
    sei.nShow  = SW_SHOWNORMAL;

    // 레지스트리 설정 (캐시 사용 — 매 호출 시 3회 읽기 방지)
    const RegCache& rc = GetRegCache();

    // TerminalPath: 커스텀 터미널 경로 (파일 존재 확인)
    std::wstring customTerm = rc.terminalPath;
    if (!customTerm.empty() && !PathFileExistsW(customTerm.c_str()))
        customTerm.clear(); // 경로 무효 → 폴백

    // TerminalType: "pwsh" → PowerShell 7, "cmd" → cmd.exe, 그 외 → wt.exe 호환
    const std::wstring& termType = rc.terminalType;
    bool usePwshArgs = (termType == L"pwsh");
    bool useCmdArgs  = (termType == L"cmd");

    // NewTab: 1 이면 wt.exe 에 --window 0 new-tab 추가
    const std::wstring wtNewTabPrefix = rc.newTab ? L"--window 0 new-tab " : L"";

    std::wstring args;
    if (!customTerm.empty())
    {
        if (usePwshArgs)
        {
            // PowerShell 7 인자 형식: -NoExit [-WorkingDirectory "dir"] -Command "claude [--% args]"
            // --%  stop-parsing prefix 로 @ 스플래팅 방지, 따옴표는 \" 이스케이프
            std::wstring pwshCmd = dangerous
                ? L"claude --% --dangerously-skip-permissions"
                : L"claude --%";
            if (!fileArgs.empty())
                pwshCmd += L" " + EscapeForPwshCommand(fileArgs);
            args = quotedFolder.empty()
                ? std::wstring(L"-NoExit -Command \"") + pwshCmd + L"\""
                : std::wstring(L"-NoExit -WorkingDirectory \"") + quotedFolder + L"\" -Command \"" + pwshCmd + L"\"";
        }
        else if (useCmdArgs)
        {
            // cmd.exe 인자 형식
            args = folder.empty()
                ? std::wstring(L"/k ") + claudeCmd
                : std::wstring(L"/k cd /d \"") + folder + L"\" && " + claudeCmd;
        }
        else
        {
            // wt.exe 호환 인자 형식 (기본)
            args = quotedFolder.empty()
                ? wtNewTabPrefix + L"cmd /k " + claudeCmd
                : wtNewTabPrefix + std::wstring(L"-d \"") + quotedFolder + L"\" cmd /k " + claudeCmd;
        }
        sei.lpFile = customTerm.c_str();
    }
    else if (FindWindowsTerminal())
    {
        args = quotedFolder.empty()
            ? wtNewTabPrefix + L"cmd /k " + claudeCmd
            : wtNewTabPrefix + std::wstring(L"-d \"") + quotedFolder + L"\" cmd /k " + claudeCmd;
        sei.lpFile = L"wt.exe";
    }
    else if (FindPowerShell7())
    {
        // PowerShell 7 자동 감지 폴백 — wt 없을 때 pwsh 사용
        std::wstring pwshCmd = dangerous
            ? L"claude --% --dangerously-skip-permissions"
            : L"claude --%";
        if (!fileArgs.empty())
            pwshCmd += L" " + EscapeForPwshCommand(fileArgs);
        args = quotedFolder.empty()
            ? std::wstring(L"-NoExit -Command \"") + pwshCmd + L"\""
            : std::wstring(L"-NoExit -WorkingDirectory \"") + quotedFolder + L"\" -Command \"" + pwshCmd + L"\"";
        sei.lpFile = L"pwsh.exe";
    }
    else
    {
        // 기본 cmd.exe (cmd 는 따옴표 안 백슬래시를 이스케이프하지 않으므로 원본 경로 사용)
        args = folder.empty()
            ? std::wstring(L"/k ") + claudeCmd
            : std::wstring(L"/k cd /d \"") + folder + L"\" && " + claudeCmd;
        sei.lpFile = L"cmd.exe";
    }
    sei.lpParameters = args.c_str();

    if (!ShellExecuteExW(&sei))
    {
        WCHAR msg[256];
        swprintf_s(msg, L"Claude Code \uC2E4\uD589 \uC2E4\uD328 (\uC624\uB958: %lu)", GetLastError());
        MessageBoxW(nullptr, msg, L"Claude Code", MB_OK | MB_ICONERROR);
    }
}

// ── IUnknown ──────────────────────────────────────────────────────────────────
STDMETHODIMP ClaudeContextMenu::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;
    if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, IID_IShellExtInit))
        *ppv = static_cast<IShellExtInit*>(this);
    else if (IsEqualIID(riid, IID_IContextMenu))
        *ppv = static_cast<IContextMenu*>(this);
    else if (IsEqualIID(riid, IID_IExplorerCommand))
        *ppv = static_cast<IExplorerCommand*>(this);
    else
        return E_NOINTERFACE;
    AddRef(); return S_OK;
}
STDMETHODIMP_(ULONG) ClaudeContextMenu::AddRef()  { return InterlockedIncrement(&m_cRef); }
STDMETHODIMP_(ULONG) ClaudeContextMenu::Release()
{
    ULONG r = InterlockedDecrement(&m_cRef);
    if (!r) delete this;
    return r;
}

// ── IShellExtInit ─────────────────────────────────────────────────────────────
STDMETHODIMP ClaudeContextMenu::Initialize(PCIDLIST_ABSOLUTE pidlFolder, IDataObject* pdtobj, HKEY)
{
    m_folderPath.clear();
    m_selectedFiles.clear();
    m_fileArgCount  = 0;
    m_extraDirCount = 0;
    if (pdtobj)
    {
        FORMATETC fe = { CF_HDROP, nullptr, DVASPECT_CONTENT, -1, TYMED_HGLOBAL };
        STGMEDIUM stg = {};
        if (SUCCEEDED(pdtobj->GetData(&fe, &stg)))
        {
            HDROP hDrop = reinterpret_cast<HDROP>(GlobalLock(stg.hGlobal));
            if (hDrop)
            {
                UINT count = DragQueryFileW(hDrop, 0xFFFFFFFF, nullptr, 0);
                std::wstring fileArgs;
                UINT dirCount = 0;
                for (UINT i = 0; i < count; i++)
                {
                    WCHAR buf[MAX_PATH] = {};
                    if (!DragQueryFileW(hDrop, i, buf, MAX_PATH)) continue;

                    bool isDir = PathIsDirectoryW(buf) != FALSE;
                    if (isDir) dirCount++;

                    if (i == 0)
                    {
                        // 첫 번째 항목으로 작업 폴더 결정
                        WCHAR folder[MAX_PATH] = {};
                        wcscpy_s(folder, buf);
                        if (!isDir) PathRemoveFileSpecW(folder);
                        m_folderPath = folder;
                    }
                    // 파일 선택 시 "@파일명" 인자 수집 (폴더는 제외)
                    if (!isDir)
                    {
                        if (!fileArgs.empty()) fileArgs += L" ";
                        std::wstring fname = PathFindFileNameW(buf);
                        // 공백 포함 파일명은 따옴표로 감쌈 — cmd/wt 가 단일 인자로 전달
                        if (fname.find(L' ') != std::wstring::npos)
                            fileArgs += L"\"@" + fname + L"\"";
                        else
                            fileArgs += L"@" + fname;
                        m_fileArgCount++;
                    }
                }
                // 두 번째 이상의 디렉터리 수 (경고용)
                m_extraDirCount = (dirCount > 1) ? (dirCount - 1) : 0;
                m_selectedFiles = fileArgs;
                GlobalUnlock(stg.hGlobal);
            }
            ReleaseStgMedium(&stg);
        }
    }
    if (m_folderPath.empty() && pidlFolder)
    {
        WCHAR buf[MAX_PATH] = {};
        if (SHGetPathFromIDListW(pidlFolder, buf)) m_folderPath = buf;
    }
    return S_OK;
}

// ── IContextMenu (구 컨텍스트 — Normal 인스턴스에서 서브메뉴 2개 제공) ──────
STDMETHODIMP ClaudeContextMenu::QueryContextMenu(HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT, UINT uFlags)
{
    if (uFlags & CMF_DEFAULTONLY) return MAKE_HRESULT(SEVERITY_SUCCESS, 0, 0);
    if (m_hSubMenu) { DestroyMenu(m_hSubMenu); m_hSubMenu = nullptr; }
    m_hSubMenu = CreatePopupMenu();
    InsertMenuW(m_hSubMenu, 0, MF_BYPOSITION | MF_STRING, idCmdFirst + CMD_NORMAL,
                L"Claude Code \uC5F4\uAE30");
    InsertMenuW(m_hSubMenu, 1, MF_BYPOSITION | MF_STRING, idCmdFirst + CMD_DANGEROUS,
                L"Claude Code \uC5F4\uAE30 (\uAD8C\uD55C \uAC74\uB108\uB220)");
    InsertMenuW(hmenu, indexMenu, MF_BYPOSITION | MF_POPUP,
                reinterpret_cast<UINT_PTR>(m_hSubMenu),
                L"Claude Code \uC5F4\uAE30");
    m_hSubMenu = nullptr; // 부모 hmenu에 소유권 이전 — 소멸자에서 재파괴 금지

    HBITMAP hbmp = GetOrCreateIconBitmap();
    if (hbmp)
    {
        MENUITEMINFOW mii = {};
        mii.cbSize   = sizeof(mii);
        mii.fMask    = MIIM_BITMAP;
        mii.hbmpItem = hbmp;
        SetMenuItemInfoW(hmenu, indexMenu, TRUE, &mii);
    }
    return MAKE_HRESULT(SEVERITY_SUCCESS, 0, CMD_COUNT);
}

STDMETHODIMP ClaudeContextMenu::InvokeCommand(LPCMINVOKECOMMANDINFO pici)
{
    if (HIWORD(pici->lpVerb) != 0) return E_FAIL;

    // 다중 폴더 선택 경고 — 첫 번째 폴더만 사용됨
    if (m_extraDirCount > 0)
    {
        WCHAR msg[MAX_PATH + 128];
        swprintf_s(msg, L"%u\uAC1C \uD3F4\uB354\uAC00 \uC120\uD0DD\uB418\uC5C8\uC2B5\uB2C8\uB2E4.\n"
                        L"\uCCAB \uBC88\uC9F8 \uD3F4\uB354\uC5D0\uC11C\uB9CC \uC5F4\uB9BD\uB2C8\uB2E4:\n%s",
                   m_extraDirCount + 1, m_folderPath.c_str());
        MessageBoxW(nullptr, msg, L"Claude Code", MB_OK | MB_ICONINFORMATION);
    }

    // 파일 100개 이상 선택 시 확인
    if (m_fileArgCount >= 100)
    {
        WCHAR msg[128];
        swprintf_s(msg, L"%u\uAC1C \uD30C\uC77C\uC774 \uC120\uD0DD\uB418\uC5C8\uC2B5\uB2C8\uB2E4.\n"
                        L"\uACC4\uC18D\uD558\uC2DC\uACA0\uC2B5\uB2C8\uAE4C?", m_fileArgCount);
        if (MessageBoxW(nullptr, msg, L"Claude Code", MB_YESNO | MB_ICONWARNING) != IDYES)
            return S_OK;
    }

    LaunchClaude(m_folderPath, LOWORD(pici->lpVerb) == CMD_DANGEROUS, m_selectedFiles);
    return S_OK;
}

STDMETHODIMP ClaudeContextMenu::GetCommandString(UINT_PTR idCmd, UINT uType, UINT*, CHAR* pszName, UINT cchMax)
{
    if (uType == GCS_HELPTEXTW)
    {
        const wchar_t* text = (idCmd == CMD_DANGEROUS)
            ? L"Claude Code \uC5F4\uAE30 (\uC704\uD5D8: \uAD8C\uD55C \uAC74\uB108\uB220)"
            : L"Claude Code \uC5F4\uAE30";
        wcsncpy_s(reinterpret_cast<wchar_t*>(pszName), cchMax, text, _TRUNCATE);
        return S_OK;
    }
    return E_NOTIMPL;
}

// ── IExplorerCommand (신 컨텍스트 메뉴 — ECF_DEFAULT, m_dangerous 로 분기) ──
STDMETHODIMP ClaudeContextMenu::GetTitle(IShellItemArray*, LPWSTR* ppszName)
{
    const wchar_t* title = m_dangerous
        ? L"Claude Code \uC5F4\uAE30 (\uAD8C\uD55C \uAC74\uB108\uB220)"
        : L"Claude Code \uC5F4\uAE30";
    SIZE_T cb = (wcslen(title) + 1) * sizeof(WCHAR);
    *ppszName = static_cast<LPWSTR>(CoTaskMemAlloc(cb));
    if (!*ppszName) return E_OUTOFMEMORY;
    wcscpy_s(*ppszName, cb / sizeof(WCHAR), title);
    return S_OK;
}
STDMETHODIMP ClaudeContextMenu::GetIcon(IShellItemArray*, LPWSTR* ppszIcon)
{
    // 프로세스 수명 동안 캐시 — Explorer 컨텍스트 메뉴 표시마다 파일시스템 탐색 방지
    static std::wstring s_src;
    static bool s_checked = false;
    if (!s_checked) { s_src = FindClaudeIconSource(); s_checked = true; }
    if (s_src.empty()) { *ppszIcon = nullptr; return S_FALSE; }
    SIZE_T cb = (s_src.size() + 1) * sizeof(WCHAR);
    *ppszIcon = static_cast<LPWSTR>(CoTaskMemAlloc(cb));
    if (!*ppszIcon) return E_OUTOFMEMORY;
    wcscpy_s(*ppszIcon, cb / sizeof(WCHAR), s_src.c_str());
    return S_OK;
}
STDMETHODIMP ClaudeContextMenu::GetToolTip(IShellItemArray*, LPWSTR* ppszTip)
{
    // 미설치 시 다운로드 안내, 설치된 경우 실행 파일 경로 표시
    std::wstring exePath = FindClaudeIconSource(); // claude.exe or icon fallback
    const wchar_t* tip = exePath.empty()
        ? L"Claude Code \uAC00 \uC124\uCE58\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4. claude.ai/download"
        : exePath.c_str();
    SIZE_T cb = (wcslen(tip) + 1) * sizeof(WCHAR);
    *ppszTip = static_cast<LPWSTR>(CoTaskMemAlloc(cb));
    if (!*ppszTip) return E_OUTOFMEMORY;
    wcscpy_s(*ppszTip, cb / sizeof(WCHAR), tip);
    return S_OK;
}
STDMETHODIMP ClaudeContextMenu::GetCanonicalName(GUID* pguid)
{
    *pguid = m_dangerous ? CLSID_ClaudeContextMenuDangerous : CLSID_ClaudeContextMenu;
    return S_OK;
}
STDMETHODIMP ClaudeContextMenu::GetState(IShellItemArray*, BOOL, EXPCMDSTATE* pState)
    { *pState = ECS_ENABLED; return S_OK; }

STDMETHODIMP ClaudeContextMenu::Invoke(IShellItemArray* psia, IBindCtx*)
{
    std::wstring folder;
    std::wstring fileArgs;
    UINT fileArgCount  = 0;
    UINT extraDirCount = 0;
    if (psia)
    {
        DWORD count = 0;
        psia->GetCount(&count);
        UINT dirCount = 0;
        for (DWORD i = 0; i < count; i++)
        {
            IShellItem* psi = nullptr;
            if (!SUCCEEDED(psia->GetItemAt(i, &psi)) || !psi) continue;

            LPWSTR pszPath = nullptr;
            if (SUCCEEDED(psi->GetDisplayName(SIGDN_FILESYSPATH, &pszPath)) && pszPath)
            {
                bool isDir = PathIsDirectoryW(pszPath) != FALSE;
                if (isDir) dirCount++;

                if (i == 0)
                {
                    // 첫 번째 항목으로 작업 폴더 결정
                    folder = pszPath;
                    if (!isDir)
                    {
                        WCHAR buf[MAX_PATH] = {};
                        wcscpy_s(buf, folder.c_str());
                        PathRemoveFileSpecW(buf);
                        folder = buf;
                    }
                }
                // 파일 선택 시 "@파일명" 인자 수집 (폴더는 제외)
                if (!isDir)
                {
                    if (!fileArgs.empty()) fileArgs += L" ";
                    std::wstring fname = PathFindFileNameW(pszPath);
                    // 공백 포함 파일명은 따옴표로 감쌈 — cmd/wt 가 단일 인자로 전달
                    if (fname.find(L' ') != std::wstring::npos)
                        fileArgs += L"\"@" + fname + L"\"";
                    else
                        fileArgs += L"@" + fname;
                    fileArgCount++;
                }
                CoTaskMemFree(pszPath);
            }
            psi->Release();
        }
        extraDirCount = (dirCount > 1) ? (dirCount - 1) : 0;
    }
    if (folder.empty()) folder = m_folderPath;

    // 다중 폴더 선택 경고
    if (extraDirCount > 0)
    {
        WCHAR msg[MAX_PATH + 128];
        swprintf_s(msg, L"%u\uAC1C \uD3F4\uB354\uAC00 \uC120\uD0DD\uB418\uC5C8\uC2B5\uB2C8\uB2E4.\n"
                        L"\uCCAB \uBC88\uC9F8 \uD3F4\uB354\uC5D0\uC11C\uB9CC \uC5F4\uB9BD\uB2C8\uB2E4:\n%s",
                   extraDirCount + 1, folder.c_str());
        MessageBoxW(nullptr, msg, L"Claude Code", MB_OK | MB_ICONINFORMATION);
    }

    // 파일 100개 이상 선택 시 확인
    UINT fc = fileArgCount > 0 ? fileArgCount : m_fileArgCount;
    if (fc >= 100)
    {
        WCHAR msg[128];
        swprintf_s(msg, L"%u\uAC1C \uD30C\uC77C\uC774 \uC120\uD0DD\uB418\uC5C8\uC2B5\uB2C8\uB2E4.\n"
                        L"\uACC4\uC18D\uD558\uC2DC\uACA0\uC2B5\uB2C8\uAE4C?", fc);
        if (MessageBoxW(nullptr, msg, L"Claude Code", MB_YESNO | MB_ICONWARNING) != IDYES)
            return S_OK;
    }

    LaunchClaude(folder, m_dangerous, fileArgs.empty() ? m_selectedFiles : fileArgs);
    return S_OK;
}

STDMETHODIMP ClaudeContextMenu::GetFlags(EXPCMDFLAGS* pFlags)
    { *pFlags = ECF_DEFAULT; return S_OK; }
STDMETHODIMP ClaudeContextMenu::EnumSubCommands(IEnumExplorerCommand** ppEnum)
    { *ppEnum = nullptr; return E_NOTIMPL; }

// ── 아이콘 비트맵 (스레드 안전 INIT_ONCE) ────────────────────────────────────
// 32-bit DIBSection + pre-multiplied alpha — 다크 테마 Explorer 투명 렌더링 지원
BOOL CALLBACK ClaudeContextMenu::InitBitmapOnce(PINIT_ONCE, PVOID, PVOID*)
{
    std::wstring src = FindClaudeIconSource();
    if (src.empty()) return TRUE;

    HICON large = nullptr, small1 = nullptr;
    ExtractIconExW(src.c_str(), 0, &large, &small1, 1);
    HICON hIcon = small1 ? small1 : large;
    if (large && large != hIcon) DestroyIcon(large);
    if (!hIcon) return TRUE;

    const int sz = GetSystemMetrics(SM_CXSMICON); // DPI 인식 아이콘 크기

    BITMAPV4HEADER bmi = {};
    bmi.bV4Size          = sizeof(bmi);
    bmi.bV4Width         = sz;
    bmi.bV4Height        = -sz; // top-down
    bmi.bV4Planes        = 1;
    bmi.bV4BitCount      = 32;
    bmi.bV4V4Compression = BI_BITFIELDS;
    bmi.bV4RedMask       = 0x00FF0000;
    bmi.bV4GreenMask     = 0x0000FF00;
    bmi.bV4BlueMask      = 0x000000FF;
    bmi.bV4AlphaMask     = 0xFF000000;

    void* pBits = nullptr;
    HDC hdcScreen = GetDC(nullptr);
    if (!hdcScreen) { DestroyIcon(hIcon); return TRUE; }
    HDC hdcMem = CreateCompatibleDC(hdcScreen);
    ReleaseDC(nullptr, hdcScreen);
    if (!hdcMem) { DestroyIcon(hIcon); return TRUE; }

    HBITMAP hbmp = CreateDIBSection(hdcMem,
        reinterpret_cast<const BITMAPINFO*>(&bmi),
        DIB_RGB_COLORS, &pBits, nullptr, 0);
    if (!hbmp) { DeleteDC(hdcMem); DestroyIcon(hIcon); return TRUE; }

    // pBits 는 0 초기화 (alpha=0, 완전 투명) — DrawIconEx 가 alpha 올바르게 설정
    HGDIOBJ hOld = SelectObject(hdcMem, hbmp);
    DrawIconEx(hdcMem, 0, 0, hIcon, sz, sz, 0, nullptr, DI_NORMAL);
    SelectObject(hdcMem, hOld);
    DeleteDC(hdcMem);
    DestroyIcon(hIcon);
    s_hBitmap = hbmp;
    return TRUE;
}

HBITMAP ClaudeContextMenu::GetOrCreateIconBitmap()
{
    InitOnceExecuteOnce(&s_initOnce, InitBitmapOnce, nullptr, nullptr);
    return s_hBitmap;
}

void ClaudeContextMenu::ReleaseStaticResources()
{
    if (s_hBitmap) { DeleteObject(s_hBitmap); s_hBitmap = nullptr; }
}

// ── 공유 유틸: Claude 아이콘 소스 탐색 ───────────────────────────────────────
std::wstring FindClaudeIconSource()
{
    // claude.exe 경로가 있으면 EXE 자체에서 아이콘 추출
    std::wstring exePath = GetClaudeExePath();
    if (!exePath.empty()) return exePath;

    // npm 전역 설치 아이콘 파일 폴백
    WCHAR appData[MAX_PATH] = {};
    ExpandEnvironmentStringsW(L"%APPDATA%", appData, MAX_PATH);

    struct { const wchar_t* rel; } icos[] = {
        { L"npm\\node_modules\\@anthropic-ai\\claude-code\\resources\\app.ico"  },
        { L"npm\\node_modules\\@anthropic-ai\\claude-code\\resources\\icon.ico" },
    };
    for (auto& e : icos)
    {
        WCHAR path[MAX_PATH] = {};
        PathCombineW(path, appData, e.rel);
        if (PathFileExistsW(path)) return path;
    }
    return {};
}
