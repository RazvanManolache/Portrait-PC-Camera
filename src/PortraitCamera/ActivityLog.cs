using System.IO;
using System.Text.Json;

namespace PortraitCamera;
internal static class ActivityLog
{
    static readonly object Sync = new();
    public static void Write(string reason, object? details = null)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Settings.Folder);
                var path = Path.Combine(Settings.Folder, "activity.jsonl");
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024)
                    File.Move(path, path + ".previous", true);
                File.AppendAllText(path, JsonSerializer.Serialize(new { time = DateTimeOffset.Now, reason, details }) + Environment.NewLine);
            }
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
