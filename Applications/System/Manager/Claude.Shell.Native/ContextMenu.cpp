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
    : m_cRef(1), m_dangerous(dangerous), m_hSubMenu(nullptr)
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

// ── 공유 유틸: 레지스트리 커스텀 터미널 경로 반환 ─────────────────────────────
// HKCU\Software\ClaudeCode 의 TerminalPath 값 (없으면 빈 문자열)
static std::wstring GetCustomTerminal()
{
    WCHAR buf[MAX_PATH] = {};
    DWORD sz = sizeof(buf);
    if (RegGetValueW(HKEY_CURRENT_USER, L"Software\\ClaudeCode", L"TerminalPath",
                     RRF_RT_REG_SZ, nullptr, buf, &sz) == ERROR_SUCCESS && buf[0])
        return buf;
    return {};
}

// ── 공유 유틸: Claude 실행 ────────────────────────────────────────────────────
static void LaunchClaude(const std::wstring& folder, bool dangerous)
{
    if (!FindClaudeExe())
    {
        MessageBoxW(nullptr,
            L"Claude Code\uAC00 \uC124\uCE58\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.\n"
            L"https://claude.ai/download \uC5D0\uC11C \uC124\uCE58 \uD6C4 \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.",
            L"Claude Code", MB_OK | MB_ICONINFORMATION);
        return;
    }

    const wchar_t* claudeArg = dangerous
        ? L"claude --dangerously-skip-permissions" : L"claude";

    SHELLEXECUTEINFOW sei = {};
    sei.cbSize = sizeof(sei);
    sei.lpVerb = L"open";
    sei.nShow  = SW_SHOWNORMAL;

    // CommandLineToArgvW 호환: 닫는 " 직전 \ 가 있으면 \\ 로 이스케이프
    // ex) "C:\" -> "C:\\" (드라이브 루트 trailing backslash 처리)
    std::wstring quotedFolder = folder;
    if (!quotedFolder.empty() && quotedFolder.back() == L'\\')
        quotedFolder += L'\\';

    std::wstring args;
    std::wstring customTerm = GetCustomTerminal();
    if (!customTerm.empty())
    {
        // 레지스트리 커스텀 터미널: wt.exe 호환 인자 형식 사용
        args = quotedFolder.empty()
            ? std::wstring(L"cmd /k ") + claudeArg
            : std::wstring(L"-d \"") + quotedFolder + L"\" cmd /k " + claudeArg;
        sei.lpFile = customTerm.c_str();
    }
    else if (FindWindowsTerminal())
    {
        args = quotedFolder.empty()
            ? std::wstring(L"cmd /k ") + claudeArg
            : std::wstring(L"-d \"") + quotedFolder + L"\" cmd /k " + claudeArg;
        sei.lpFile = L"wt.exe";
    }
    else
    {
        // 기본 cmd.exe (cmd 는 따옴표 안 백슬래시를 이스케이프하지 않으므로 원본 경로 사용)
        args = folder.empty()
            ? std::wstring(L"/k ") + claudeArg
            : std::wstring(L"/k cd /d \"") + folder + L"\" && " + claudeArg;
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
    if (pdtobj)
    {
        FORMATETC fe = { CF_HDROP, nullptr, DVASPECT_CONTENT, -1, TYMED_HGLOBAL };
        STGMEDIUM stg = {};
        if (SUCCEEDED(pdtobj->GetData(&fe, &stg)))
        {
            HDROP hDrop = reinterpret_cast<HDROP>(GlobalLock(stg.hGlobal));
            if (hDrop)
            {
                WCHAR buf[MAX_PATH] = {};
                if (DragQueryFileW(hDrop, 0, buf, MAX_PATH))
                {
                    // 파일 선택 시 부모 폴더로 정규화
                    if (!PathIsDirectoryW(buf))
                        PathRemoveFileSpecW(buf);
                    m_folderPath = buf;
                }
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
    LaunchClaude(m_folderPath, LOWORD(pici->lpVerb) == CMD_DANGEROUS);
    return S_OK;
}
STDMETHODIMP ClaudeContextMenu::GetCommandString(UINT_PTR, UINT, UINT*, CHAR*, UINT)
    { return E_NOTIMPL; }

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
    std::wstring src = FindClaudeIconSource();
    if (src.empty()) { *ppszIcon = nullptr; return S_FALSE; }
    SIZE_T cb = (src.size() + 1) * sizeof(WCHAR);
    *ppszIcon = static_cast<LPWSTR>(CoTaskMemAlloc(cb));
    if (!*ppszIcon) return E_OUTOFMEMORY;
    wcscpy_s(*ppszIcon, cb / sizeof(WCHAR), src.c_str());
    return S_OK;
}
STDMETHODIMP ClaudeContextMenu::GetToolTip(IShellItemArray*, LPWSTR* ppszTip)
    { *ppszTip = nullptr; return S_FALSE; }
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
    if (psia)
    {
        IShellItem* psi = nullptr;
        if (SUCCEEDED(psia->GetItemAt(0, &psi)) && psi)
        {
            LPWSTR pszPath = nullptr;
            if (SUCCEEDED(psi->GetDisplayName(SIGDN_FILESYSPATH, &pszPath)) && pszPath)
            {
                folder = pszPath;
                CoTaskMemFree(pszPath);
                // 파일 선택 시 부모 폴더로 정규화
                if (!PathIsDirectoryW(folder.c_str()))
                {
                    WCHAR buf[MAX_PATH] = {};
                    wcscpy_s(buf, folder.c_str());
                    PathRemoveFileSpecW(buf);
                    folder = buf;
                }
            }
            psi->Release();
        }
    }
    if (folder.empty()) folder = m_folderPath;
    LaunchClaude(folder, m_dangerous);
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
