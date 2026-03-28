#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <new>
#include "ContextMenu.h"

// ── 전역 ──────────────────────────────────────────────────────────────────────
HINSTANCE g_hInst   = nullptr;
long      g_cDllRef = 0;

// CLSID_Normal    = {261B2913-8ABA-420B-9280-0061626EDA5A}
// CLSID_Dangerous = {261B2913-8ABA-420B-9280-0061626EDA5B}
const CLSID CLSID_ClaudeContextMenu = {
    0x261B2913, 0x8ABA, 0x420B,
    { 0x92, 0x80, 0x00, 0x61, 0x62, 0x6E, 0xDA, 0x5A }
};
const CLSID CLSID_ClaudeContextMenuDangerous = {
    0x261B2913, 0x8ABA, 0x420B,
    { 0x92, 0x80, 0x00, 0x61, 0x62, 0x6E, 0xDA, 0x5B }
};

// ── DllMain ───────────────────────────────────────────────────────────────────
BOOL APIENTRY DllMain(HMODULE hModule, DWORD dwReason, LPVOID)
{
    if (dwReason == DLL_PROCESS_ATTACH) {
        g_hInst = hModule;
        DisableThreadLibraryCalls(hModule);
    }
    return TRUE;
}

// ── ClassFactory ──────────────────────────────────────────────────────────────
class ClassFactory : public IClassFactory
{
    long m_cRef      = 1;
    bool m_dangerous;
public:
    explicit ClassFactory(bool dangerous) : m_dangerous(dangerous) {}

    STDMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv) return E_POINTER;
        *ppv = nullptr;
        if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, IID_IClassFactory))
            *ppv = static_cast<IClassFactory*>(this);
        else
            return E_NOINTERFACE;
        AddRef();
        return S_OK;
    }
    STDMETHODIMP_(ULONG) AddRef()  override { return InterlockedIncrement(&m_cRef); }
    STDMETHODIMP_(ULONG) Release() override
    {
        ULONG ref = InterlockedDecrement(&m_cRef);
        if (ref == 0) delete this;
        return ref;
    }

    STDMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv) override
    {
        if (pUnkOuter) return CLASS_E_NOAGGREGATION;
        auto* p = new (std::nothrow) ClaudeContextMenu(m_dangerous);
        if (!p) return E_OUTOFMEMORY;
        HRESULT hr = p->QueryInterface(riid, ppv);
        p->Release();
        return hr;
    }
    STDMETHODIMP LockServer(BOOL fLock) override
    {
        fLock ? InterlockedIncrement(&g_cDllRef) : InterlockedDecrement(&g_cDllRef);
        return S_OK;
    }
};

// ── COM 내보내기 ───────────────────────────────────────────────────────────────
STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv)
{
    *ppv = nullptr;
    bool dangerous;
    if      (IsEqualCLSID(rclsid, CLSID_ClaudeContextMenu))          dangerous = false;
    else if (IsEqualCLSID(rclsid, CLSID_ClaudeContextMenuDangerous)) dangerous = true;
    else    return CLASS_E_CLASSNOTAVAILABLE;

    auto* pCF = new (std::nothrow) ClassFactory(dangerous);
    if (!pCF) return E_OUTOFMEMORY;
    HRESULT hr = pCF->QueryInterface(riid, ppv);
    pCF->Release();
    return hr;
}

STDAPI DllCanUnloadNow()
{
    return (g_cDllRef == 0) ? S_OK : S_FALSE;
}
