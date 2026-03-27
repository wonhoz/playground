#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlobj.h>
#include <string>

// CLSID: {261B2913-8ABA-420B-9280-0061626EDA5A}
extern const CLSID CLSID_ClaudeContextMenu;
extern HINSTANCE   g_hInst;
extern long        g_cDllRef;

class ClaudeContextMenu : public IShellExtInit, public IContextMenu
{
public:
    ClaudeContextMenu();
    ~ClaudeContextMenu();

    // IUnknown
    STDMETHODIMP         QueryInterface(REFIID riid, void** ppv) override;
    STDMETHODIMP_(ULONG) AddRef()  override;
    STDMETHODIMP_(ULONG) Release() override;

    // IShellExtInit
    STDMETHODIMP Initialize(PCIDLIST_ABSOLUTE pidlFolder, IDataObject* pdtobj, HKEY hkeyProgID) override;

    // IContextMenu
    STDMETHODIMP QueryContextMenu(HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags) override;
    STDMETHODIMP InvokeCommand(LPCMINVOKECOMMANDINFO pici) override;
    STDMETHODIMP GetCommandString(UINT_PTR idCmd, UINT uType, UINT* pReserved, CHAR* pszName, UINT cchMax) override;

private:
    long          m_cRef;
    std::wstring  m_folderPath;
    HMENU         m_hSubMenu;

    static HBITMAP s_hBitmap;
    static bool    s_iconLoaded;

    static HBITMAP       GetOrCreateIconBitmap();
    static std::wstring  FindClaudeIconSource();
};
