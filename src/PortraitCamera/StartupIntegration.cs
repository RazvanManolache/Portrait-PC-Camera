using Microsoft.Win32;
using System.IO;

namespace PortraitCamera;
internal static class StartupIntegration
{
    public static void Apply(Settings settings)
    {
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"))) return;
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (settings.StartWithWindows) key.SetValue("PortraitCamera", "\"" + Environment.ProcessPath + "\" --background");
        else key.DeleteValue("PortraitCamera", false);
    }
}
