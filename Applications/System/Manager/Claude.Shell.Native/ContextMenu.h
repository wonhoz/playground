#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <string>

// CLSID_Normal   = {261B2913-8ABA-420B-9280-0061626EDA5A}  구/신 컨텍스트 공통
// CLSID_Dangerous= {261B2913-8ABA-420B-9280-0061626EDA5B}  신 컨텍스트 두 번째 항목
extern const CLSID CLSID_ClaudeContextMenu;
extern const CLSID CLSID_ClaudeContextMenuDangerous;
extern HINSTANCE   g_hInst;
extern LONG        g_cDllRef;

// 공유 유틸 (ContextMenu.cpp 에서 구현)
std::wstring FindClaudeIconSource();
bool         FindClaudeExe();        // claude.exe 설치 여부 확인 (PATH + 하드코딩 경로)
bool         FindWindowsTerminal();  // wt.exe 탐색 (PATH + WindowsApps 경로)

class ClaudeContextMenu : public IShellExtInit, public IContextMenu, public IExplorerCommand
{
public:
    explicit ClaudeContextMenu(bool dangerous = false);
    ~ClaudeContextMenu();

    // IUnknown
    STDMETHODIMP         QueryInterface(REFIID riid, void** ppv) override;
    STDMETHODIMP_(ULONG) AddRef()  override;
    STDMETHODIMP_(ULONG) Release() override;

    // IShellExtInit
    STDMETHODIMP Initialize(PCIDLIST_ABSOLUTE pidlFolder, IDataObject* pdtobj, HKEY) override;

    // IContextMenu (구 컨텍스트 메뉴 — Normal 인스턴스에서 서브메뉴 2개 제공)
    STDMETHODIMP QueryContextMenu(HMENU hmenu, UINT indexMenu, UINT idCmdFirst, UINT idCmdLast, UINT uFlags) override;
    STDMETHODIMP InvokeCommand(LPCMINVOKECOMMANDINFO pici) override;
    STDMETHODIMP GetCommandString(UINT_PTR idCmd, UINT uType, UINT*, CHAR*, UINT) override;

    // IExplorerCommand (신 컨텍스트 메뉴 — ECF_DEFAULT, 서브메뉴 없음)
    STDMETHODIMP GetTitle(IShellItemArray*, LPWSTR* ppszName) override;
    STDMETHODIMP GetIcon(IShellItemArray*, LPWSTR* ppszIcon) override;
    STDMETHODIMP GetToolTip(IShellItemArray*, LPWSTR* ppszTip) override;
    STDMETHODIMP GetCanonicalName(GUID* pguid) override;
    STDMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* pState) override;
    STDMETHODIMP Invoke(IShellItemArray*, IBindCtx*) override;
    STDMETHODIMP GetFlags(EXPCMDFLAGS* pFlags) override;
    STDMETHODIMP EnumSubCommands(IEnumExplorerCommand** ppEnum) override;

    // DLL 언로드 시 정적 GDI 리소스 해제 (dllmain DLL_PROCESS_DETACH 에서 호출)
    static void ReleaseStaticResources();

private:
    LONG          m_cRef;
    bool          m_dangerous;    // true = --dangerously-skip-permissions
    std::wstring  m_folderPath;
    std::wstring  m_selectedFiles; // space-joined "@file" args for file selections
    HMENU         m_hSubMenu;

    static HBITMAP   s_hBitmap;
    static INIT_ONCE s_initOnce; // 스레드 안전 1회 초기화

    static HBITMAP GetOrCreateIconBitmap();
    static BOOL CALLBACK InitBitmapOnce(PINIT_ONCE, PVOID, PVOID*);
};
