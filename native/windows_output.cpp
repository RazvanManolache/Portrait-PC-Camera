#include <windows.h>
#include <sddl.h>
#include <mfapi.h>
#include <mfvirtualcamera.h>
#include <wrl/client.h>
#include <atomic>
#include <mutex>
#include <thread>
#include <vector>
#include <memory>
#include "mf/PortraitProtocol.h"
using Microsoft::WRL::ComPtr;

namespace {
struct Output {
    ComPtr<IMFVirtualCamera> camera;
    std::wstring sid;
    int width = PortraitWidth, height = PortraitHeight;
    std::vector<BYTE> frame = std::vector<BYTE>(PortraitFrameBytes);
    std::mutex lock;
    std::atomic<bool> stop{false};
    std::atomic<ULONGLONG> lastRequest{0};
    std::atomic<ULONGLONG> requestedFrames{0};
    std::vector<std::thread> workers;
    HANDLE stopped = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    bool comInitialized = false;
    bool mfInitialized = false;
    ~Output() {
        stop = true;
        if (stopped) SetEvent(stopped);
        for (auto& worker : workers) if (worker.joinable()) worker.join();
        if (stopped) CloseHandle(stopped);
        if (camera) { camera->Stop(); camera->Remove(); camera->Shutdown(); camera.Reset(); }
        if (mfInitialized) MFShutdown();
        if (comInitialized) CoUninitialize();
    }
};
bool Complete(Output* output, HANDLE pipe, OVERLAPPED& operation, DWORD& done) {
    HANDLE events[] = {output->stopped, operation.hEvent};
    if (WaitForMultipleObjects(2, events, FALSE, INFINITE) == WAIT_OBJECT_0 + 1)
        return GetOverlappedResult(pipe, &operation, &done, FALSE) != FALSE;
    CancelIoEx(pipe, &operation);
    GetOverlappedResult(pipe, &operation, &done, TRUE);
    return false;
}
bool Transfer(Output* output, HANDLE pipe, void* bytes, DWORD count, bool write) {
    auto ptr = static_cast<BYTE*>(bytes);
    while (count) {
        DWORD done = 0;
        OVERLAPPED operation{};
        operation.hEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!operation.hEvent) return false;
        BOOL ok = write ? WriteFile(pipe, ptr, count, &done, &operation) : ReadFile(pipe, ptr, count, &done, &operation);
        if (!ok && GetLastError() == ERROR_IO_PENDING) ok = Complete(output, pipe, operation, done);
        CloseHandle(operation.hEvent);
        if (!ok || !done) return false;
        ptr += done; count -= done;
    }
    return true;
}
void Serve(Output* output) {
    // Only the owner, SYSTEM, and the Windows camera service may receive frames.
    std::wstring acl = L"D:P(A;;GA;;;SY)(A;;GA;;;LS)(A;;GA;;;" + output->sid + L")";
    PSECURITY_DESCRIPTOR descriptor = nullptr;
    if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(acl.c_str(), SDDL_REVISION_1, &descriptor, nullptr)) return;
    SECURITY_ATTRIBUTES security{sizeof(security), descriptor, FALSE};
    std::vector<BYTE> frame(output->frame.size());
    while (!output->stop) {
        HANDLE pipe = CreateNamedPipeW(PortraitPipeName(output->sid).c_str(), PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
            4, 65536, 65536, 0, &security);
        if (pipe == INVALID_HANDLE_VALUE) break;
        if (output->stop) { CloseHandle(pipe); break; }
        OVERLAPPED operation{};
        operation.hEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!operation.hEvent) { CloseHandle(pipe); break; }
        BOOL connected = ConnectNamedPipe(pipe, &operation);
        if (!connected) {
            DWORD error = GetLastError(), done = 0;
            if (error == ERROR_PIPE_CONNECTED) connected = TRUE;
            else if (error == ERROR_IO_PENDING) connected = Complete(output, pipe, operation, done);
        }
        CloseHandle(operation.hEvent);
        if (connected) while (!output->stop) {
            BYTE request = 0;
            if (!Transfer(output, pipe, &request, 1, false) || request != 1 || output->stop) break;
            { std::lock_guard<std::mutex> guard(output->lock); std::memcpy(frame.data(), output->frame.data(), frame.size()); }
            PortraitFrameHeader header{PortraitMagic, static_cast<DWORD>(frame.size()), static_cast<DWORD>(output->width), static_cast<DWORD>(output->height)};
            if (!Transfer(output, pipe, &header, sizeof(header), true) || !Transfer(output, pipe, frame.data(), static_cast<DWORD>(frame.size()), true)) break;
            output->lastRequest = GetTickCount64();
            ++output->requestedFrames;
        }
        DisconnectNamedPipe(pipe); CloseHandle(pipe);
    }
    LocalFree(descriptor);
}
HRESULT GetOwner(std::wstring& sid) {
    HANDLE token = nullptr;
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token)) return HRESULT_FROM_WIN32(GetLastError());
    DWORD size = 0; GetTokenInformation(token, TokenUser, nullptr, 0, &size);
    std::vector<BYTE> data(size);
    BOOL ok = GetTokenInformation(token, TokenUser, data.data(), size, &size);
    CloseHandle(token);
    if (!ok) return HRESULT_FROM_WIN32(GetLastError());
    LPWSTR text = nullptr;
    if (!ConvertSidToStringSidW(reinterpret_cast<TOKEN_USER*>(data.data())->User.Sid, &text)) return HRESULT_FROM_WIN32(GetLastError());
    sid = text; LocalFree(text); return S_OK;
}
}
static HRESULT StartOutput(void** handle, bool createCamera, int width = PortraitWidth, int height = PortraitHeight) {
    if (!handle) return E_POINTER;
    *handle = nullptr;
    if (!PortraitValidSize(width, height)) return E_INVALIDARG;
    auto output = std::make_unique<Output>();
    if (!output->stopped) return HRESULT_FROM_WIN32(GetLastError());
    output->width = width; output->height = height; output->frame.resize(width * height * 3);
    HRESULT hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(hr) && hr != RPC_E_CHANGED_MODE) return hr;
    output->comInitialized = SUCCEEDED(hr);
    hr = MFStartup(MF_VERSION);
    if (FAILED(hr)) return hr;
    output->mfInitialized = true;
    hr = GetOwner(output->sid);
    if (FAILED(hr)) return hr;
    for (int i = 0; i < 4; ++i) output->workers.emplace_back(Serve, output.get());
    if (!createCamera) { *handle = output.release(); return S_OK; }
    hr = MFCreateVirtualCamera(MFVirtualCameraType_SoftwareCameraSource, MFVirtualCameraLifetime_Session,
        MFVirtualCameraAccess_CurrentUser, L"Portrait Camera", PortraitIdForWidth(width), nullptr, 0, &output->camera);
    if (FAILED(hr)) return hr;
    hr = output->camera->SetString(PortraitOwnerAttribute, output->sid.c_str());
    if (FAILED(hr)) return hr;
    hr = MFSetAttributeSize(output->camera.Get(), PortraitSizeAttribute, width, height);
    if (FAILED(hr)) return hr;
    hr = output->camera->Start(nullptr);
    if (FAILED(hr)) return hr;
    *handle = output.release(); return S_OK;
}
extern "C" __declspec(dllexport) HRESULT __cdecl pwStart(void** handle) { return StartOutput(handle, true); }
extern "C" __declspec(dllexport) HRESULT __cdecl pwStartTransport(void** handle) { return StartOutput(handle, false); }
extern "C" __declspec(dllexport) HRESULT __cdecl pwStartSized(void** handle, int width, int height) { return StartOutput(handle, true, width, height); }
extern "C" __declspec(dllexport) HRESULT __cdecl pwStartTransportSized(void** handle, int width, int height) { return StartOutput(handle, false, width, height); }
extern "C" __declspec(dllexport) void __cdecl pwSend(void* handle, const BYTE* bgr) {
    if (!handle || !bgr) return;
    auto output = static_cast<Output*>(handle);
    std::lock_guard<std::mutex> guard(output->lock);
    std::memcpy(output->frame.data(), bgr, output->frame.size());
}
extern "C" __declspec(dllexport) BOOL __cdecl pwConnected(void* handle) {
    if (!handle) return FALSE;
    auto tick = static_cast<Output*>(handle)->lastRequest.load();
    return tick && GetTickCount64() - tick < 1200;
}
extern "C" __declspec(dllexport) void __cdecl pwStop(void* handle) { delete static_cast<Output*>(handle); }
extern "C" __declspec(dllexport) unsigned long long __cdecl pwRequestedFrames(void* handle) {
    return handle ? static_cast<Output*>(handle)->requestedFrames.load() : 0;
}
