using System.Runtime.InteropServices;
using System.Text;

namespace PortraitCamera;

public record CameraDevice(int Index, string Name, string Id)
{
    public override string ToString() => Name;
}

internal static class NativeCamera
{
    static NativeCamera()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeCamera).Assembly, (name, _, _) =>
            name == Dll ? NativeLibrary.Load(System.IO.Path.Combine(AppContext.BaseDirectory,
                $"PortraitCamera.Native-{typeof(NativeCamera).Assembly.GetName().Version!.ToString(3)}.dll")) : 0);
    }
    const string Dll = "PortraitCamera.Native.dll";
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint scCreateCamera(int width, int height, float fps);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void scDeleteCamera(nint camera);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void scSendFrame(nint camera, nint pixels);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool scIsConnected(nint camera);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    static extern int pcListCameras(StringBuilder output, int capacity);

    public static List<CameraDevice> List(bool includeOutput = false)
    {
        var buffer = new StringBuilder(65536);
        if (pcListCameras(buffer, buffer.Capacity) > buffer.Capacity) throw new InvalidOperationException("Camera list is too large.");
        return buffer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t', 3))
            .Where(p => p.Length == 3 && (includeOutput || !p[1].StartsWith("Portrait Camera", StringComparison.Ordinal)))
            .Select(p => new CameraDevice(int.Parse(p[0]), p[1], p[2])).ToList();
    }
}
