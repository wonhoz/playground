#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shobjidl.h>
#include <string>

// -- 서브 커맨드 (잎 노드) --
class ClaudeSubCommand : public IExplorerCommand
{
public:
    explicit ClaudeSubCommand(bool dangerous);

    STDMETHODIMP         QueryInterface(REFIID riid, void** ppv) override;
    STDMETHODIMP_(ULONG) AddRef()  override;
    STDMETHODIMP_(ULONG) Release() override;

    STDMETHODIMP GetTitle(IShellItemArray*, LPWSTR* ppszName) override;
    STDMETHODIMP GetIcon(IShellItemArray*, LPWSTR* ppszIcon) override;
    STDMETHODIMP GetToolTip(IShellItemArray*, LPWSTR* ppszTip) override;
    STDMETHODIMP GetCanonicalName(GUID* pguid) override;
    STDMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* pState) override;
    STDMETHODIMP Invoke(IShellItemArray* psia, IBindCtx*) override;
    STDMETHODIMP GetFlags(EXPCMDFLAGS* pFlags) override;
    STDMETHODIMP EnumSubCommands(IEnumExplorerCommand**) override;

private:
    long m_cRef;
    bool m_dangerous;
};

// -- 서브 커맨드 열거자 --
class ClaudeEnumSubCommands : public IEnumExplorerCommand
{
public:
    ClaudeEnumSubCommands();
    ~ClaudeEnumSubCommands();

    STDMETHODIMP         QueryInterface(REFIID riid, void** ppv) override;
    STDMETHODIMP_(ULONG) AddRef()  override;
    STDMETHODIMP_(ULONG) Release() override;

    STDMETHODIMP Next(ULONG celt, IExplorerCommand** rgelt, ULONG* pceltFetched) override;
    STDMETHODIMP Skip(ULONG celt) override;
    STDMETHODIMP Reset() override;
    STDMETHODIMP Clone(IEnumExplorerCommand** ppenum) override;

private:
    long            m_cRef;
    ULONG           m_idx;
    ClaudeSubCommand* m_cmds[2];
};
