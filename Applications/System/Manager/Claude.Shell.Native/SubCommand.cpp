#include "SubCommand.h"
#include <shlobj.h>
#include <shellapi.h>
#include <new>
#include <objbase.h>

// normal sub: {7A1B2C3D-4E5F-6789-ABCD-EF0123456789}
static const GUID GUID_CmdNormal = {
    0x7A1B2C3D, 0x4E5F, 0x6789,
    { 0xAB, 0xCD, 0xEF, 0x01, 0x23, 0x45, 0x67, 0x89 }
};
// dangerous sub: {8B2C3D4E-5F6A-789A-BCDE-F01234567890}
static const GUID GUID_CmdDangerous = {
    0x8B2C3D4E, 0x5F6A, 0x789A,
    { 0xBC, 0xDE, 0xF0, 0x12, 0x34, 0x56, 0x78, 0x90 }
};

static std::wstring GetFolderFromItemArray(IShellItemArray* psia)
{
    if (!psia) return {};
    IShellItem* psi = nullptr;
    if (FAILED(psia->GetItemAt(0, &psi))) return {};
    LPWSTR pszPath = nullptr;
    psi->GetDisplayName(SIGDN_FILESYSPATH, &pszPath);
    psi->Release();
    if (!pszPath) return {};
    std::wstring path = pszPath;
    CoTaskMemFree(pszPath);
    return path;
}

static void LaunchClaude(const std::wstring& folder, bool dangerous)
{
    const wchar_t* claudeArg = dangerous
        ? L"claude --dangerously-skip-permissions"
        : L"claude";

    WCHAR args[MAX_PATH * 2 + 64] = {};
    if (folder.empty())
        swprintf_s(args, L"/k %s", claudeArg);
    else
        swprintf_s(args, L"/k cd /d \"%s\" && %s", folder.c_str(), claudeArg);

    SHELLEXECUTEINFOW sei = {};
    sei.cbSize       = sizeof(sei);
    sei.lpVerb       = L"open";
    sei.lpFile       = L"cmd.exe";
    sei.lpParameters = args;
    sei.nShow        = SW_SHOWNORMAL;
    ShellExecuteExW(&sei);
}

// ── ClaudeSubCommand ──────────────────────────────────────────────────────────

ClaudeSubCommand::ClaudeSubCommand(bool dangerous)
    : m_cRef(1), m_dangerous(dangerous) {}

STDMETHODIMP ClaudeSubCommand::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;
    if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, IID_IExplorerCommand))
        *ppv = static_cast<IExplorerCommand*>(this);
    else return E_NOINTERFACE;
    AddRef(); return S_OK;
}
STDMETHODIMP_(ULONG) ClaudeSubCommand::AddRef()  { return InterlockedIncrement(&m_cRef); }
STDMETHODIMP_(ULONG) ClaudeSubCommand::Release()
{
    ULONG r = InterlockedDecrement(&m_cRef);
    if (!r) delete this;
    return r;
}

STDMETHODIMP ClaudeSubCommand::GetTitle(IShellItemArray*, LPWSTR* ppszName)
{
    const wchar_t* title = m_dangerous
        ? L"Claude Code \xC5F4\xAE30 (\xAD8C\xD55C \xAC74\xB108\xB871)"
        : L"Claude Code \xC5F4\xAE30";
    SIZE_T cb = (wcslen(title) + 1) * sizeof(WCHAR);
    *ppszName = static_cast<LPWSTR>(CoTaskMemAlloc(cb));
    if (!*ppszName) return E_OUTOFMEMORY;
    wcscpy_s(*ppszName, cb / sizeof(WCHAR), title);
    return S_OK;
}
STDMETHODIMP ClaudeSubCommand::GetIcon(IShellItemArray*, LPWSTR* ppszIcon)
    { *ppszIcon = nullptr; return S_FALSE; }
STDMETHODIMP ClaudeSubCommand::GetToolTip(IShellItemArray*, LPWSTR* ppszTip)
    { *ppszTip = nullptr; return S_FALSE; }
STDMETHODIMP ClaudeSubCommand::GetCanonicalName(GUID* pguid)
    { *pguid = m_dangerous ? GUID_CmdDangerous : GUID_CmdNormal; return S_OK; }
STDMETHODIMP ClaudeSubCommand::GetState(IShellItemArray*, BOOL, EXPCMDSTATE* pState)
    { *pState = ECS_ENABLED; return S_OK; }
STDMETHODIMP ClaudeSubCommand::GetFlags(EXPCMDFLAGS* pFlags)
    { *pFlags = ECF_DEFAULT; return S_OK; }
STDMETHODIMP ClaudeSubCommand::EnumSubCommands(IEnumExplorerCommand** ppEnum)
    { *ppEnum = nullptr; return E_NOTIMPL; }

STDMETHODIMP ClaudeSubCommand::Invoke(IShellItemArray* psia, IBindCtx*)
{
    LaunchClaude(GetFolderFromItemArray(psia), m_dangerous);
    return S_OK;
}

// ── ClaudeEnumSubCommands ─────────────────────────────────────────────────────

ClaudeEnumSubCommands::ClaudeEnumSubCommands()
    : m_cRef(1), m_idx(0)
{
    m_cmds[0] = new ClaudeSubCommand(false);
    m_cmds[1] = new ClaudeSubCommand(true);
}
ClaudeEnumSubCommands::~ClaudeEnumSubCommands()
{
    m_cmds[0]->Release();
    m_cmds[1]->Release();
}

STDMETHODIMP ClaudeEnumSubCommands::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;
    if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, IID_IEnumExplorerCommand))
        *ppv = static_cast<IEnumExplorerCommand*>(this);
    else return E_NOINTERFACE;
    AddRef(); return S_OK;
}
STDMETHODIMP_(ULONG) ClaudeEnumSubCommands::AddRef()  { return InterlockedIncrement(&m_cRef); }
STDMETHODIMP_(ULONG) ClaudeEnumSubCommands::Release()
{
    ULONG r = InterlockedDecrement(&m_cRef);
    if (!r) delete this;
    return r;
}

STDMETHODIMP ClaudeEnumSubCommands::Next(ULONG celt, IExplorerCommand** rgelt, ULONG* pceltFetched)
{
    ULONG fetched = 0;
    while (fetched < celt && m_idx < 2)
    {
        rgelt[fetched] = m_cmds[m_idx];
        m_cmds[m_idx]->AddRef();
        fetched++;
        m_idx++;
    }
    if (pceltFetched) *pceltFetched = fetched;
    return (fetched == celt) ? S_OK : S_FALSE;
}
STDMETHODIMP ClaudeEnumSubCommands::Skip(ULONG celt)
{
    m_idx = min(m_idx + celt, 2u);
    return S_OK;
}
STDMETHODIMP ClaudeEnumSubCommands::Reset()
{
    m_idx = 0;
    return S_OK;
}
STDMETHODIMP ClaudeEnumSubCommands::Clone(IEnumExplorerCommand** ppenum)
{
    auto* p = new (std::nothrow) ClaudeEnumSubCommands();
    if (!p) return E_OUTOFMEMORY;
    p->m_idx = m_idx;
    *ppenum = p;
    return S_OK;
}
