#pragma once
#include "PortraitProtocol.h"
#include <vector>
class FrameGenerator {
    std::wstring _sid;
    HANDLE _pipe = INVALID_HANDLE_VALUE;
    std::vector<BYTE> _pixels = std::vector<BYTE>(PortraitFrameBytes);
    ULONGLONG _next = 0;
    int _width = PortraitWidth, _height = PortraitHeight;
    bool Transfer(void* bytes, DWORD count, bool write);
public:
    ~FrameGenerator() { Disconnect(); }
    void SetSize(int width, int height) { _width = width; _height = height; _pixels.resize(width * height * 3); }
    void SetOwner(const std::wstring& sid) { _sid = sid; }
    void Disconnect() { if (_pipe != INVALID_HANDLE_VALUE) CloseHandle(_pipe); _pipe = INVALID_HANDLE_VALUE; }
    HRESULT EnsureRenderTarget(UINT, UINT) { return S_OK; }
    HRESULT SetD3DManager(IUnknown*, UINT, UINT) { return S_OK; }
    HRESULT Generate(IMFSample* sample, REFGUID format, IMFSample** result);
};
