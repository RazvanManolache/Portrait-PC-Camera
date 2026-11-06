#include <windows.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <mferror.h>
#include <wrl/client.h>
#include <sddl.h>
#include <cstdio>
#include <vector>
#include <string>
#include "mf/PortraitProtocol.h"
using Microsoft::WRL::ComPtr;
extern "C" __declspec(dllimport) HRESULT __cdecl pwStartTransport(void**);
extern "C" __declspec(dllimport) HRESULT __cdecl pwStart(void**);
extern "C" __declspec(dllimport) HRESULT __cdecl pwStartSized(void**, int, int);
extern "C" __declspec(dllimport) HRESULT __cdecl pwStartTransportSized(void**, int, int);
extern "C" __declspec(dllimport) void __cdecl pwSend(void*, const BYTE*);
extern "C" __declspec(dllimport) BOOL __cdecl pwConnected(void*);
extern "C" __declspec(dllimport) unsigned long long __cdecl pwRequestedFrames(void*);
extern "C" __declspec(dllimport) void __cdecl pwStop(void*);
#define CHECK(expr) do { HRESULT checkHr = (expr); if (FAILED(checkHr)) { std::fprintf(stderr, "%s failed: 0x%08lx at line %d\n", #expr, checkHr, __LINE__); return checkHr; } } while(0)
std::wstring Owner() {
    HANDLE token = nullptr; OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token);
    DWORD size = 0; GetTokenInformation(token, TokenUser, nullptr, 0, &size);
    std::vector<BYTE> data(size); GetTokenInformation(token, TokenUser, data.data(), size, &size); CloseHandle(token);
    LPWSTR sid = nullptr; ConvertSidToStringSidW(reinterpret_cast<TOKEN_USER*>(data.data())->User.Sid, &sid);
    std::wstring result = sid ? sid : L""; LocalFree(sid); return result;
}
HRESULT FindCamera(ComPtr<IMFActivate>& found, bool print) {
    ComPtr<IMFAttributes> attributes; CHECK(MFCreateAttributes(&attributes, 1));
    CHECK(attributes->SetGUID(MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE, MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID));
    IMFActivate** devices = nullptr; UINT32 count = 0;
    CHECK(MFEnumDeviceSources(attributes.Get(), &devices, &count));
    for (UINT32 i = 0; i < count; ++i) {
        LPWSTR name = nullptr; UINT32 length = 0;
        if (SUCCEEDED(devices[i]->GetAllocatedString(MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME, &name, &length))) {
            if (print) std::wprintf(L"%s\n", name);
            if (std::wstring(name).find(L"Portrait Camera") == 0) found = devices[i];
        }
        CoTaskMemFree(name); devices[i]->Release();
    }
    CoTaskMemFree(devices); return found ? S_OK : MF_E_NOT_FOUND;
}
HRESULT Run(bool installed, int testWidth, int testHeight) {
    void* output = nullptr;
    CHECK(installed ? pwStartSized(&output, testWidth, testHeight) : pwStartTransportSized(&output, testWidth, testHeight));
    struct Cleanup { void* handle; ~Cleanup() { pwStop(handle); } } cleanup{output};
    std::vector<BYTE> pixels(testWidth * testHeight * 3, 0);
    for (int y = 0; y < testHeight; ++y) for (int x = 0; x < testWidth; ++x) {
        auto p = pixels.data() + (y * testWidth + x) * 3;
        p[y < testHeight / 2 ? 2 : 0] = 255;
    }
    pwSend(output, pixels.data());
    Sleep(100);
    ComPtr<IMFActivate> activate;
    HMODULE module = nullptr;
    if (installed) CHECK(FindCamera(activate, true));
    else {
        module = LoadLibraryW(L"PortraitCamera.MediaSource.dll");
        if (!module) return HRESULT_FROM_WIN32(GetLastError());
        using GetClass = HRESULT(WINAPI*)(REFCLSID, REFIID, void**);
        auto getClass = reinterpret_cast<GetClass>(GetProcAddress(module, "DllGetClassObject"));
        ComPtr<IClassFactory> factory;
        CHECK(getClass(PortraitClassForWidth(testWidth), IID_PPV_ARGS(&factory)));
        CHECK(factory->CreateInstance(nullptr, IID_PPV_ARGS(&activate)));
        CHECK(activate->SetString(PortraitOwnerAttribute, Owner().c_str()));
        CHECK(MFSetAttributeSize(activate.Get(), PortraitSizeAttribute, testWidth, testHeight));
    }
    ComPtr<IMFMediaSource> source;
    CHECK(activate->ActivateObject(IID_PPV_ARGS(&source)));
    ComPtr<IMFSourceReader> reader;
    CHECK(MFCreateSourceReaderFromMediaSource(source.Get(), nullptr, &reader));
    ComPtr<IMFMediaType> type;
    CHECK(reader->GetNativeMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, &type));
    UINT32 width = 0, height = 0;
    CHECK(MFGetAttributeSize(type.Get(), MF_MT_FRAME_SIZE, &width, &height));
    if (width != testWidth || height != testHeight) {
        std::fprintf(stderr, "Expected %d x %d, got %u x %u\n", testWidth, testHeight, width, height);
        return E_FAIL;
    }
    CHECK(reader->SetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, nullptr, type.Get()));
    CHECK(reader->SetStreamSelection(MF_SOURCE_READER_FIRST_VIDEO_STREAM, TRUE));
    Sleep(1800);
    if (pwConnected(output) || pwRequestedFrames(output) != 0) {
        std::fprintf(stderr, "Discovery unexpectedly counted as streaming\n"); return E_FAIL;
    }
    ULONGLONG started = GetTickCount64();
    bool colorValid = false;
    for (int i = 0; i < 32; ++i) {
        ComPtr<IMFSample> sample; DWORD flags = 0; LONGLONG timestamp = 0;
        CHECK(reader->ReadSample(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, nullptr, &flags, &timestamp, &sample));
        if (!sample) { if (flags & MF_SOURCE_READERF_ERROR) return E_FAIL; --i; continue; }
        ComPtr<IMFMediaBuffer> buffer; CHECK(sample->ConvertToContiguousBuffer(&buffer));
        BYTE* data = nullptr; DWORD length = 0; CHECK(buffer->Lock(&data, nullptr, &length));
        if (length >= testWidth * testHeight * 3 / 2)
            colorValid = data[(testHeight / 4) * testWidth + 100] > 70 && data[(testHeight / 4) * testWidth + 100] < 95
                && data[(testHeight * 3 / 4) * testWidth + 100] > 30 && data[(testHeight * 3 / 4) * testWidth + 100] < 55;
        buffer->Unlock();
    }
    auto elapsed = GetTickCount64() - started;
    bool connected = pwConnected(output) != FALSE;
    if (pwRequestedFrames(output) < 32) return E_FAIL;
    std::fill(pixels.begin(), pixels.end(), 0); pwSend(output, pixels.data());
    bool black = false;
    for (int i = 0; i < 16; ++i) {
        ComPtr<IMFSample> sample; DWORD flags = 0;
        CHECK(reader->ReadSample(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, nullptr, &flags, nullptr, &sample));
        if (!sample) continue;
        ComPtr<IMFMediaBuffer> buffer; CHECK(sample->ConvertToContiguousBuffer(&buffer));
        BYTE* data = nullptr; DWORD length = 0; CHECK(buffer->Lock(&data, nullptr, &length));
        black = length >= testWidth * testHeight * 3 / 2 && data[testWidth * testHeight / 4] == 16;
        buffer->Unlock();
    }
    if (!black) return E_FAIL;
    reader.Reset(); source->Shutdown(); source.Reset(); activate->ShutdownObject(); activate.Reset();
    if (!colorValid || !connected) { std::fprintf(stderr, "colorValid=%d connected=%d\n", colorValid, connected); return E_FAIL; }
    std::printf("{\"passed\":true,\"installedDevice\":%s,\"width\":%d,\"height\":%d,\"frames\":32,\"elapsedMs\":%llu,\"nv12Colors\":true,\"demand\":true,\"frameReplacement\":true}\n", installed ? "true" : "false", testWidth, testHeight, elapsed);
    return S_OK;
}
int main(int argc, char** argv) {
    CoInitializeEx(nullptr, COINIT_MULTITHREADED); MFStartup(MF_VERSION);
    HRESULT hr = S_OK;
    if (argc > 1 && std::string(argv[1]) == "--list") { ComPtr<IMFActivate> camera; hr = FindCamera(camera, true); }
    else {
        int width = argc > 2 ? std::atoi(argv[2]) : PortraitWidth;
        hr = Run(argc > 1 && std::string(argv[1]) == "--installed", width, width * 16 / 9);
    }
    MFShutdown(); CoUninitialize(); return FAILED(hr) ? 1 : 0;
}
