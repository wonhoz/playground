#include "ContextMenu.h"
#include <shlwapi.h>
#include <shellapi.h>
#include <new>

static const UINT CMD_NORMAL    = 0;
static const UINT CMD_DANGEROUS = 1;
static const UINT CMD_COUNT     = 2;
static const UINT MIIM_BITMAP_  = 0x80;

HBITMAP ClaudeContextMenu::s_hBitmap    = nullptr;
bool    ClaudeContextMenu::s_iconLoaded = false;

ClaudeContextMenu::ClaudeContextMenu()
    : m_cRef(1), m_hSubMenu(nullptr)
{
    InterlockedIncrement(&g_cDllRef);
}

ClaudeContextMenu::~ClaudeContextMenu()
{
    if (m_hSubMenu) { DestroyMenu(m_hSubMenu); m_hSubMenu = nullptr; }
    InterlockedDecrement(&g_cDllRef);
}

STDMETHODIMP ClaudeContextMenu::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;
    if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, IID_IShellExtInit))
        *ppv = static_cast<IShellExtInit*>(this);
    else if (IsEqualIID(riid, IID_IContextMenu))
        *ppv = static_cast<IContextMenu*>(this);
    else
        return E_NOINTERFACE;
    AddRef();
    return S_OK;
}

STDMETHODIMP_(ULONG) ClaudeContextMenu::AddRef()
{
    return InterlockedIncrement(&m_cRef);
}

STDMETHODIMP_(ULONG) ClaudeContextMenu::Release()
{
    ULONG ref = InterlockedDecrement(&m_cRef);
    if (ref == 0) delete this;
    return ref;
}

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
                    m_folderPath = buf;
                GlobalUnlock(stg.hGlobal);
            }
            ReleaseStgMedium(&stg);
        }
    }

    if (m_folderPath.empty() && pidlFolder)
    {
        WCHAR buf[MAX_PATH] = {};
        if (SHGetPathFromIDListW(pidlFolder, buf))
            m_folderPath = buf;
    }

    return S_OK;
}

STDMETHODIMP ClaudeContextMenu::QueryContextMenu(HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags)
{
    if ((uFlags & 0x000F) == CMF_DEFAULTONLY) return MAKE_HRESULT(SEVERITY_SUCCESS, 0, 0);

    if (m_hSubMenu) { DestroyMenu(m_hSubMenu); m_hSubMenu = nullptr; }
    m_hSubMenu = CreatePopupMenu();

    InsertMenuW(m_hSubMenu, 0, MF_BYPOSITION | MF_STRING, idCmdFirst + CMD_NORMAL,
                L"Claude Code \xC5F4\xAE30");
    InsertMenuW(m_hSubMenu, 1, MF_BYPOSITION | MF_STRING, idCmdFirst + CMD_DANGEROUS,
                L"Claude Code \xC5F4\xAE30 (\xAD8C\xD55C \xAC74\xB108\xB871)");
    InsertMenuW(hmenu, indexMenu, MF_BYPOSITION | MF_POPUP,
                reinterpret_cast<UINT_PTR>(m_hSubMenu),
                L"Claude Code\xC5D0\xC11C \xC5F4\xAE30");

    HBITMAP hbmp = GetOrCreateIconBitmap();
    if (hbmp)
    {
        MENUITEMINFOW mii  = {};
        mii.cbSize         = sizeof(mii);
        mii.fMask          = MIIM_BITMAP_;
        mii.hbmpItem       = hbmp;
        SetMenuItemInfoW(hmenu, indexMenu, TRUE, &mii);
    }

    return MAKE_HRESULT(SEVERITY_SUCCESS, 0, CMD_COUNT);
}

STDMETHODIMP ClaudeContextMenu::InvokeCommand(LPCMINVOKECOMMANDINFO pici)
{
    if (HIWORD(pici->lpVerb) != 0) return E_FAIL;

    UINT  cmdId     = LOWORD(pici->lpVerb);
    bool  dangerous = (cmdId == CMD_DANGEROUS);
    const wchar_t* claudeArg = dangerous
        ? L"claude --dangerously-skip-permissions"
        : L"claude";

    WCHAR args[MAX_PATH * 2 + 64] = {};
    if (m_folderPath.empty())
        swprintf_s(args, L"/k %s", claudeArg);
    else
        swprintf_s(args, L"/k cd /d \"%s\" && %s", m_folderPath.c_str(), claudeArg);

    SHELLEXECUTEINFOW sei = {};
    sei.cbSize       = sizeof(sei);
    sei.lpVerb       = L"open";
    sei.lpFile       = L"cmd.exe";
    sei.lpParameters = args;
    sei.nShow        = SW_SHOWNORMAL;
    ShellExecuteExW(&sei);

    return S_OK;
}

STDMETHODIMP ClaudeContextMenu::GetCommandString(UINT_PTR, UINT, UINT*, CHAR*, UINT)
{
    return E_NOTIMPL;
}

HBITMAP ClaudeContextMenu::GetOrCreateIconBitmap()
{
    if (s_iconLoaded) return s_hBitmap;
    s_iconLoaded = true;

    std::wstring src = FindClaudeIconSource();
    if (src.empty()) return nullptr;

    HICON large1 = nullptr, small1 = nullptr;
    ExtractIconExW(src.c_str(), 0, &large1, &small1, 1);

    HICON hIcon = small1 ? small1 : large1;
    if (large1 && large1 != hIcon) DestroyIcon(large1);
    if (!hIcon) return nullptr;

    int     sz   = 16;
    HDC     hdcS = GetDC(nullptr);
    HDC     hdcM = CreateCompatibleDC(hdcS);
    HBITMAP hbmp = CreateCompatibleBitmap(hdcS, sz, sz);
    HGDIOBJ hOld = SelectObject(hdcM, hbmp);
    DrawIconEx(hdcM, 0, 0, hIcon, sz, sz, 0, nullptr, DI_NORMAL);
    SelectObject(hdcM, hOld);
    DeleteDC(hdcM);
    ReleaseDC(nullptr, hdcS);
    DestroyIcon(hIcon);

    s_hBitmap = hbmp;
    return hbmp;
}

std::wstring ClaudeContextMenu::FindClaudeIconSource()
{
    WCHAR userProfile[MAX_PATH] = {};
    WCHAR localAppData[MAX_PATH] = {};
    WCHAR appData[MAX_PATH] = {};
    ExpandEnvironmentStringsW(L"%USERPROFILE%",  userProfile,  MAX_PATH);
    ExpandEnvironmentStringsW(L"%LOCALAPPDATA%", localAppData, MAX_PATH);
    ExpandEnvironmentStringsW(L"%APPDATA%",      appData,      MAX_PATH);

    struct { const wchar_t* base; const wchar_t* rel; } exes[] = {
        { userProfile,  L".local\\bin\\claude.exe"          },
        { localAppData, L"AnthropicClaude\\claude.exe"      },
        { localAppData, L"Programs\\claude\\claude.exe"     },
        { localAppData, L"Programs\\Claude\\Claude.exe"     },
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
