#pragma once
#include <windows.h>
#include <string>

inline constexpr GUID PortraitSource360 = {0xf85fd47c,0xa5d3,0x467c,{0xb2,0xd4,0xfa,0xe4,0x2d,0x3d,0xd6,0xb1}};
inline constexpr wchar_t PortraitSourceId360[] = L"{F85FD47C-A5D3-467C-B2D4-FAE42D3DD6B1}";
inline constexpr GUID PortraitSource720 = {0xade8bace,0xd898,0x461e,{0x84,0x5d,0xf7,0xe2,0x55,0x52,0x7b,0x4f}};
inline constexpr wchar_t PortraitSourceId720[] = L"{ADE8BACE-D898-461E-845D-F7E255527B4F}";
inline constexpr GUID PortraitSource1080 = {0xc0d3c4fe,0x26d3,0x480b,{0x9a,0x96,0xf8,0x8e,0x26,0xe5,0xd4,0xf0}};
inline constexpr wchar_t PortraitSourceId1080[] = L"{C0D3C4FE-26D3-480B-9A96-F88E26E5D4F0}";
inline constexpr GUID PortraitSourceClsid = PortraitSource720;
inline const GUID& PortraitClassForWidth(int width) { return width == 360 ? PortraitSource360 : width == 1080 ? PortraitSource1080 : PortraitSource720; }
inline const wchar_t* PortraitIdForWidth(int width) { return width == 360 ? PortraitSourceId360 : width == 1080 ? PortraitSourceId1080 : PortraitSourceId720; }
inline constexpr GUID PortraitOwnerAttribute = {0x743346b4,0xb05c,0x4e82,{0xac,0x04,0xdc,0x2b,0x9c,0xcd,0xd0,0x53}};
inline constexpr int PortraitWidth = 720, PortraitHeight = 1280;
inline constexpr DWORD PortraitFrameBytes = PortraitWidth * PortraitHeight * 3;
inline constexpr GUID PortraitSizeAttribute = {0xb64adbe1,0xc232,0x4ebf,{0xa5,0xc4,0x6a,0xf1,0x49,0x79,0xbd,0x21}};
inline bool PortraitValidSize(int width, int height) {
    return (width == 360 && height == 640) || (width == 720 && height == 1280) || (width == 1080 && height == 1920);
}
inline constexpr DWORD PortraitMagic = 0x5043414d;
struct PortraitFrameHeader { DWORD magic, length, width, height; };
inline std::wstring PortraitPipeName(const std::wstring& sid) { return L"\\\\.\\pipe\\PortraitCamera.MF." + sid; }
