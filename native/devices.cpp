#include <windows.h>
#include <dshow.h>
#include <string>
#include <algorithm>

// Enumeration only: never open a camera just to populate the picker.
extern "C" int __cdecl pcListCameras(wchar_t* output, int capacity)
{
    const HRESULT init = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    ICreateDevEnum* devices = nullptr;
    IEnumMoniker* enumerator = nullptr;
    std::wstring result;
    if (SUCCEEDED(CoCreateInstance(CLSID_SystemDeviceEnum, nullptr, CLSCTX_INPROC_SERVER,
        IID_ICreateDevEnum, reinterpret_cast<void**>(&devices))))
    {
        if (devices->CreateClassEnumerator(CLSID_VideoInputDeviceCategory, &enumerator, 0) == S_OK)
        {
            IMoniker* moniker = nullptr;
            int index = 0;
            while (enumerator->Next(1, &moniker, nullptr) == S_OK)
            {
                IPropertyBag* bag = nullptr;
                std::wstring name = L"Camera", id;
                if (SUCCEEDED(moniker->BindToStorage(nullptr, nullptr, IID_IPropertyBag, reinterpret_cast<void**>(&bag))))
                {
                    VARIANT value; VariantInit(&value);
                    if (SUCCEEDED(bag->Read(L"FriendlyName", &value, nullptr)) && value.vt == VT_BSTR) name = value.bstrVal;
                    VariantClear(&value);
                    if (SUCCEEDED(bag->Read(L"DevicePath", &value, nullptr)) && value.vt == VT_BSTR) id = value.bstrVal;
                    VariantClear(&value);
                    bag->Release();
                }
                if (id.empty())
                {
                    LPOLESTR display = nullptr;
                    if (SUCCEEDED(moniker->GetDisplayName(nullptr, nullptr, &display))) { id = display; CoTaskMemFree(display); }
                }
                std::replace(name.begin(), name.end(), L'\t', L' ');
                std::replace(name.begin(), name.end(), L'\n', L' ');
                result += std::to_wstring(index++) + L"\t" + name + L"\t" + id + L"\n";
                moniker->Release();
            }
            enumerator->Release();
        }
        devices->Release();
    }
    if (SUCCEEDED(init)) CoUninitialize();
    if (output && capacity > static_cast<int>(result.size())) wcscpy_s(output, capacity, result.c_str());
    return static_cast<int>(result.size()) + 1;
}
