using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PortraitCamera;

internal record UpdateResult(string Message, Uri? ReleasePage = null);
internal static class Updates
{
    public const string Repository = "RazvanManolache/Portrait-PC-Camera";
    public static Version CurrentVersion => new(typeof(Updates).Assembly.GetName().Version!.ToString(3));
    public static string VersionLabel => "v" + CurrentVersion;
    static readonly HttpClient Client = CreateClient();
    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PortraitCamera/" + CurrentVersion);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }
    public static UpdateResult Parse(string json, Version installed)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
            return new("No stable release available yet.");
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"^v?\d+\.\d+\.\d+$") || !Version.TryParse(tag.TrimStart('v'), out var latest))
            return new("This release has an unrecognised version number.");
        if (latest <= installed) return new("You’re up to date.");
        return new($"Version {latest} is available.", new Uri($"https://github.com/{Repository}/releases/tag/{Uri.EscapeDataString(tag)}"));
    }
    public static async Task<UpdateResult> CheckAsync()
    {
        try
        {
            using var response = await Client.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest").ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound) return new("No published release available yet.");
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                return new("GitHub is busy. Try checking again later.");
            response.EnsureSuccessStatusCode();
            return Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false), CurrentVersion);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
        { return new("Couldn’t check for updates. Try again when you’re online."); }
    }
    public static void OpenRelease(Uri page) => Process.Start(new ProcessStartInfo(page.AbsoluteUri) { UseShellExecute = true });
}
