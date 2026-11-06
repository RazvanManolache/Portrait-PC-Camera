using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace PortraitCamera;

internal static class WindowsCameraOutput
{
    public const string ClassId = "{ADE8BACE-D898-461E-845D-F7E255527B4F}";
    [DllImport("PortraitCamera.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int pwStartSized(out nint handle, int width, int height);
    [DllImport("PortraitCamera.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void pwSend(nint handle, nint pixels);
    [DllImport("PortraitCamera.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool pwConnected(nint handle);
    [DllImport("PortraitCamera.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong pwRequestedFrames(nint handle);
    [DllImport("PortraitCamera.Windows.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void pwStop(nint handle);
    public static bool Installed()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\CLSID\" + ClassId + @"\InprocServer32");
        return key?.GetValue("") is string;
    }
}
