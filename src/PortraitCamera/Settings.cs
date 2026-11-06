using System.IO;
using System.Text.Json;

namespace PortraitCamera;

public record Settings
{
    public int OutputWidth { get; init; } = 720;
    public int OutputHeight => OutputWidth * 16 / 9;
    public static int ValidWidth(int width) => width is 360 or 720 or 1080 ? width : 720;
    public bool CheckForUpdates { get; init; } = true;
    public bool Paused { get; init; }
    public string CameraId { get; init; } = "";
    public bool AutoFrame { get; init; } = true;
    public bool Mirror { get; init; }
    public int Rotation { get; init; }
    public double Position { get; init; } = .5;
    public double Zoom { get; init; } = 1;
    public bool ShowOnCall { get; init; } = true;
    public bool StartWithWindows { get; init; } = true;
    public bool KeepOnTop { get; init; } = true;
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PortraitCamera");
    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(Folder, "settings.json"))) ?? new(); }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, "settings.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
}
