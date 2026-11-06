#include <windows.h>
#include <dbt.h>
#include <dshow.h>
#include <cstdio>
#include <cstring>

// Windows' documented legacy filter-data serializer (fil_data.h is no longer
// distributed with current SDKs). Used only to generate installer metadata.
struct __declspec(uuid("97F7C4D4-547B-4A5F-8332-536430AD2E4D")) FilterDataSerializer : IUnknown
{
    virtual HRESULT STDMETHODCALLTYPE ParseFilterData(BYTE*, ULONG, BYTE**) = 0;
    virtual HRESULT STDMETHODCALLTYPE CreateFilterData(REGFILTER2*, BYTE**, ULONG*) = 0;
};

int main(int argc, char** argv)
{
    if (argc == 2 && std::strcmp(argv[1], "--refresh") == 0)
    {
        DWORD_PTR result = 0;
        // Notify existing device pickers; do not stop applications or change selections.
        SendMessageTimeoutW(HWND_BROADCAST, WM_DEVICECHANGE, DBT_DEVNODES_CHANGED,
            0, SMTO_ABORTIFHUNG, 200, &result);
        return 0;
    }
    if (argc != 2 || std::strcmp(argv[1], "--metadata") != 0) return 2;
    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(hr)) return 3;
    FilterDataSerializer* serializer = nullptr;
    hr = CoCreateInstance(CLSID_FilterMapper2, nullptr, CLSCTX_INPROC_SERVER,
        __uuidof(FilterDataSerializer), reinterpret_cast<void**>(&serializer));
    BYTE* bytes = nullptr;
    ULONG count = 0;
    if (SUCCEEDED(hr))
    {
        const REGPINTYPES types[] = { { &MEDIATYPE_Video, &MEDIASUBTYPE_NULL } };
        REGFILTERPINS pin = {};
        pin.strName = const_cast<LPWSTR>(L"Output");
        pin.bOutput = TRUE;
        pin.clsConnectsToFilter = &CLSID_NULL;
        pin.nMediaTypes = 1;
        pin.lpMediaType = types;
        REGFILTER2 filter = {};
        filter.dwVersion = 1;
        filter.dwMerit = MERIT_DO_NOT_USE;
        filter.cPins = 1;
        filter.rgPins = &pin;
        hr = serializer->CreateFilterData(&filter, &bytes, &count);
        if (SUCCEEDED(hr))
        {
            for (ULONG i = 0; i < count; ++i) std::printf("%02x", bytes[i]);
            std::printf("\n");
        }
        CoTaskMemFree(bytes);
        serializer->Release();
    }
    CoUninitialize();
    if (FAILED(hr)) std::fprintf(stderr, "Filter metadata generation failed: 0x%08lx\n", hr);
    return FAILED(hr) ? 1 : 0;
}
