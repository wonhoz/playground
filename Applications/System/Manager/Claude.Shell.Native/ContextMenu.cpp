#include "ContextMenu.h"
#include <shlwapi.h>
#include <shellapi.h>
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

// ── 공유 유틸: Claude 실행 ────────────────────────────────────────────────────
static void LaunchClaude(const std::wstring& folder, bool dangerous)
{
    const wchar_t* claudeArg = dangerous
        ? L"claude --dangerously-skip-permissions" : L"claude";
    std::wstring args = folder.empty()
        ? std::wstring(L"/k ") + claudeArg
        : std::wstring(L"/k cd /d \"") + folder + L"\" && " + claudeArg;
    SHELLEXECUTEINFOW sei = {};
    sei.cbSize = sizeof(sei); sei.lpVerb = L"open";
    sei.lpFile = L"cmd.exe"; sei.lpParameters = args.c_str(); sei.nShow = SW_SHOWNORMAL;
    if (!ShellExecuteExW(&sei))
    {
        WCHAR msg[256];
        swprintf_s(msg, L"Claude Code 실행 실패 (오류: %lu)", GetLastError());
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
                if (DragQueryFileW(hDrop, 0, buf, MAX_PATH)) m_folderPath = buf;
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
                L"Claude Code 열기");
    InsertMenuW(m_hSubMenu, 1, MF_BYPOSITION | MF_STRING, idCmdFirst + CMD_DANGEROUS,
                L"Claude Code 열기 (권한 건너뜀)");
    InsertMenuW(hmenu, indexMenu, MF_BYPOSITION | MF_POPUP,
                reinterpret_cast<UINT_PTR>(m_hSubMenu),
                L"Claude Code 열기");

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
        ? L"Claude Code 열기 (권한 건너뜀)"
        : L"Claude Code 열기";
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
BOOL CALLBACK ClaudeContextMenu::InitBitmapOnce(PINIT_ONCE, PVOID, PVOID*)
{
    std::wstring src = FindClaudeIconSource();
    if (src.empty()) return TRUE;
    HICON large1 = nullptr, small1 = nullptr;
    ExtractIconExW(src.c_str(), 0, &large1, &small1, 1);
    HICON hIcon = small1 ? small1 : large1;
    if (large1 && large1 != hIcon) DestroyIcon(large1);
    if (!hIcon) return TRUE;
    int sz = 16;
    HDC hdcS = GetDC(nullptr);
    if (!hdcS) { DestroyIcon(hIcon); return TRUE; }
    HDC hdcM = CreateCompatibleDC(hdcS);
    if (!hdcM) { ReleaseDC(nullptr, hdcS); DestroyIcon(hIcon); return TRUE; }
    HBITMAP hbmp = CreateCompatibleBitmap(hdcS, sz, sz);
    if (!hbmp) { DeleteDC(hdcM); ReleaseDC(nullptr, hdcS); DestroyIcon(hIcon); return TRUE; }
    HGDIOBJ hOld = SelectObject(hdcM, hbmp);
    DrawIconEx(hdcM, 0, 0, hIcon, sz, sz, 0, nullptr, DI_NORMAL);
    SelectObject(hdcM, hOld); DeleteDC(hdcM); ReleaseDC(nullptr, hdcS);
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

// ── 공유 유틸: Claude 실행파일/아이콘 탐색 ────────────────────────────────────
std::wstring FindClaudeIconSource()
{
    WCHAR userProfile[MAX_PATH] = {}, localAppData[MAX_PATH] = {}, appData[MAX_PATH] = {};
    ExpandEnvironmentStringsW(L"%USERPROFILE%",  userProfile,  MAX_PATH);
    ExpandEnvironmentStringsW(L"%LOCALAPPDATA%", localAppData, MAX_PATH);
    ExpandEnvironmentStringsW(L"%APPDATA%",      appData,      MAX_PATH);

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
