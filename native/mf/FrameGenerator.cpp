#include "pch.h"
#include "FrameGenerator.h"
#include <algorithm>
#include <sddl.h>

bool FrameGenerator::Transfer(void* bytes, DWORD count, bool write) {
    auto ptr = static_cast<BYTE*>(bytes);
    while (count) {
        DWORD done = 0;
        OVERLAPPED operation{};
        operation.hEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!operation.hEvent) return false;
        BOOL ok = write ? WriteFile(_pipe, ptr, count, &done, &operation) : ReadFile(_pipe, ptr, count, &done, &operation);
        if (!ok && GetLastError() == ERROR_IO_PENDING) {
            if (WaitForSingleObject(operation.hEvent, 250) == WAIT_OBJECT_0)
                ok = GetOverlappedResult(_pipe, &operation, &done, FALSE);
            else { CancelIoEx(_pipe, &operation); GetOverlappedResult(_pipe, &operation, &done, TRUE); ok = FALSE; }
        }
        CloseHandle(operation.hEvent);
        if (!ok || !done) return false;
        ptr += done; count -= done;
    }
    return true;
}
HRESULT FrameGenerator::Generate(IMFSample* sample, REFGUID format, IMFSample** result) {
    RETURN_HR_IF_NULL(E_POINTER, result); *result = nullptr;
    RETURN_HR_IF(MF_E_INVALIDMEDIATYPE, format != MFVideoFormat_NV12);
    auto now = GetTickCount64();
    if (_next > now && _next - now < 100) Sleep(static_cast<DWORD>(_next - now));
    _next = GetTickCount64() + 33;
    bool valid = false;
    if (_pipe == INVALID_HANDLE_VALUE && !_sid.empty()) {
        PSID parsed = nullptr;
        if (ConvertStringSidToSidW(_sid.c_str(), &parsed)) {
            LocalFree(parsed);
            _pipe = CreateFileW(PortraitPipeName(_sid).c_str(), GENERIC_READ | GENERIC_WRITE,
                0, nullptr, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, nullptr);
        }
    }
    if (_pipe != INVALID_HANDLE_VALUE) {
        BYTE request = 1; PortraitFrameHeader header{};
        valid = Transfer(&request, 1, true) && Transfer(&header, sizeof(header), false)
            && header.magic == PortraitMagic && header.length == static_cast<DWORD>(_pixels.size())
            && header.width == _width && header.height == _height
            && Transfer(_pixels.data(), static_cast<DWORD>(_pixels.size()), false);
        if (!valid) Disconnect();
    }
    wil::com_ptr_nothrow<IMFMediaBuffer> buffer;
    RETURN_IF_FAILED(sample->GetBufferByIndex(0, &buffer));
    wil::com_ptr_nothrow<IMF2DBuffer2> buffer2d;
    BYTE* data = nullptr; BYTE* start = nullptr; DWORD length = 0; LONG pitch = _width;
    bool twoD = SUCCEEDED(buffer->QueryInterface(IID_PPV_ARGS(&buffer2d)));
    if (twoD) RETURN_IF_FAILED(buffer2d->Lock2DSize(MF2DBuffer_LockFlags_Write, &data, &pitch, &start, &length));
    else { RETURN_IF_FAILED(buffer->Lock(&data, &length, nullptr)); start = data; }
    HRESULT hr = S_OK;
    if (pitch < _width || length < static_cast<DWORD>(pitch * _height * 3 / 2)) hr = MF_E_BUFFERTOOSMALL;
    else if (!valid) {
        for (int y = 0; y < _height; ++y) std::memset(data + y * pitch, 16, _width);
        for (int y = 0; y < _height / 2; ++y) std::memset(data + (_height + y) * pitch, 128, _width);
    }
    else {
        auto clip = [](int value) { return static_cast<BYTE>((std::clamp)(value, 0, 255)); };
        for (int y = 0; y < _height; ++y) {
            BYTE* row = data + y * pitch;
            const BYTE* input = _pixels.data() + y * _width * 3;
            for (int x = 0; x < _width; ++x) {
                const BYTE* rgb = input + x * 3;
                row[x] = clip(((66 * rgb[2] + 129 * rgb[1] + 25 * rgb[0] + 128) >> 8) + 16);
            }
        }
        for (int y = 0; y < _height; y += 2) {
            BYTE* uv = data + (_height + y / 2) * pitch;
            for (int x = 0; x < _width; x += 2) {
                int r = 0, g = 0, b = 0;
                for (int dy = 0; dy < 2; ++dy) for (int dx = 0; dx < 2; ++dx) {
                    const BYTE* rgb = _pixels.data() + ((y + dy) * _width + x + dx) * 3;
                    b += rgb[0]; g += rgb[1]; r += rgb[2];
                }
                r /= 4; g /= 4; b /= 4;
                uv[x] = clip(((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128);
                uv[x + 1] = clip(((112 * r - 94 * g - 18 * b + 128) >> 8) + 128);
            }
        }
    }
    if (twoD) buffer2d->Unlock2D(); else buffer->Unlock();
    RETURN_IF_FAILED(hr);
    RETURN_IF_FAILED(buffer->SetCurrentLength(_width * _height * 3 / 2));
    RETURN_IF_FAILED(sample->SetSampleTime(MFGetSystemTime()));
    RETURN_IF_FAILED(sample->SetSampleDuration(333333));
    RETURN_IF_FAILED(sample->SetUINT32(MFSampleExtension_CleanPoint, TRUE));
    sample->AddRef(); *result = sample; return S_OK;
}
