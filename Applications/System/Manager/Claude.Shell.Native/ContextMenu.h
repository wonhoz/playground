#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <string>

extern const CLSID CLSID_ClaudeContextMenu;
extern HINSTANCE   g_hInst;
extern long        g_cDllRef;

// 공유 유틸 (ContextMenu.cpp 에서 구현)
std::wstring FindClaudeIconSource();

class ClaudeContextMenu : public IShellExtInit, public IContextMenu, public IExplorerCommand
{
public:
    ClaudeContextMenu();
    ~ClaudeContextMenu();

    // IUnknown
    STDMETHODIMP         QueryInterface(REFIID riid, void** ppv) override;
    STDMETHODIMP_(ULONG) AddRef()  override;
    STDMETHODIMP_(ULONG) Release() override;

    // IShellExtInit
    STDMETHODIMP Initialize(PCIDLIST_ABSOLUTE pidlFolder, IDataObject* pdtobj, HKEY) override;

    // IContextMenu (구 컨텍스트 메뉴)
    STDMETHODIMP QueryContextMenu(HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags) override;
    STDMETHODIMP InvokeCommand(LPCMINVOKECOMMANDINFO pici) override;
    STDMETHODIMP GetCommandString(UINT_PTR idCmd, UINT uType, UINT*, CHAR*, UINT) override;

    // IExplorerCommand (신 컨텍스트 메뉴 — 부모 노드)
    STDMETHODIMP GetTitle(IShellItemArray*, LPWSTR* ppszName) override;
    STDMETHODIMP GetIcon(IShellItemArray*, LPWSTR* ppszIcon) override;
    STDMETHODIMP GetToolTip(IShellItemArray*, LPWSTR* ppszTip) override;
    STDMETHODIMP GetCanonicalName(GUID* pguid) override;
    STDMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* pState) override;
    STDMETHODIMP Invoke(IShellItemArray*, IBindCtx*) override;
    STDMETHODIMP GetFlags(EXPCMDFLAGS* pFlags) override;
    STDMETHODIMP EnumSubCommands(IEnumExplorerCommand** ppEnum) override;

private:
    long          m_cRef;
    std::wstring  m_folderPath;
    HMENU         m_hSubMenu;

    static HBITMAP s_hBitmap;
    static bool    s_iconLoaded;

    static HBITMAP GetOrCreateIconBitmap();
};
