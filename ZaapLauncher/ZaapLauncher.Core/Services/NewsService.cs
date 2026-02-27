using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZaapLauncher.Core.Models;

namespace ZaapLauncher.Core.Services;

public sealed class NewsService
{
    private static readonly HttpClient Http = new();

    private const string DefaultManifestUrl = "http://127.0.0.1:8080/manifest.json";

    public async Task<NewsRoot?> FetchAsync(CancellationToken ct)
    {
        var settings = LoadSettings();
        var manifestUrl = ResolveManifestUrl(settings);
        var newsUrl = ResolveNewsUrl(manifestUrl);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, newsUrl);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

            using var resp = await Http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.NotFound)
                return await LoadLocalNewsAsync(ct);

            resp.EnsureSuccessStatusCode();
            var remoteJson = await resp.Content.ReadAsStringAsync(ct);
            return DeserializeNews(remoteJson);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return await LoadLocalNewsAsync(ct);
        }
    }

    private static string ResolveNewsUrl(string manifestUrl)
    {
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var manifestUri))
            return "news.json";

        var newsUri = new Uri(manifestUri, "news.json");
        return newsUri.ToString();
    }

    private static async Task<NewsRoot?> LoadLocalNewsAsync(CancellationToken ct)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var path = Path.Combine(baseDir, "Assets", "news", "news.json");

        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, ct);
        return DeserializeNews(json);
    }

    private static NewsRoot? DeserializeNews(string json)
    {
        return JsonSerializer.Deserialize<NewsRoot>(json, SerializerOptions());
    }

    private static string ResolveManifestUrl(LauncherSettings settings)
    {
        var env = Environment.GetEnvironmentVariable("ZAAP_MANIFEST_URL");
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim();

        if (!string.IsNullOrWhiteSpace(settings.ManifestUrl))
            return settings.ManifestUrl.Trim();

        return DefaultManifestUrl;
    }

    private static LauncherSettings LoadSettings()
    {
        if (!File.Exists(Paths.SettingsPath))
            return new LauncherSettings();

        try
        {
            var json = File.ReadAllText(Paths.SettingsPath);
            return JsonSerializer.Deserialize<LauncherSettings>(json, SerializerOptions()) ?? new LauncherSettings();
        }
        catch
        {
            return new LauncherSettings();
        }
    }

    private static JsonSerializerOptions SerializerOptions() => new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class LauncherSettings
    {
        public string ManifestUrl { get; set; } = "";
    }
}