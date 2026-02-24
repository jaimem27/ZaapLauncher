using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZaapLauncher.App.Models;

namespace ZaapLauncher.App.Services;

public sealed class ManifestService
{
    private static readonly HttpClient Http = new();

    private const string DefaultManifestUrl = "http://127.0.0.1:8080/manifest.json";

    public async Task<Manifest> FetchAsync(CancellationToken ct)
    {
        var settings = LoadSettings();
        var manifestUrl = ResolveManifestUrl(settings);
        var allowUnsigned = settings.AllowUnsignedManifest || IsLocalManifestUrl(manifestUrl);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

            using var resp = await Http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.NotModified)
                return await LoadFromCacheOrThrowAsync("cache local", allowUnsigned, ct);

            resp.EnsureSuccessStatusCode();

            var remoteJson = await resp.Content.ReadAsStringAsync(ct);
            var manifest = DeserializeManifest(remoteJson, manifestUrl);
            ManifestSignatureVerifier.VerifyOrThrow(remoteJson, manifest, allowUnsigned);

            Directory.CreateDirectory(Path.GetDirectoryName(Paths.ManifestCachePath)!);
            await File.WriteAllTextAsync(Paths.ManifestCachePath, remoteJson, ct);

            return manifest;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (File.Exists(Paths.ManifestCachePath))
                return await LoadFromCacheOrThrowAsync("cache local (fallback)", allowUnsigned, ct);

            throw new UpdateFlowException(
                "No se pudo descargar el manifest.",
                $"Revisa manifestUrl ({manifestUrl}) en ZAAP_MANIFEST_URL o settings.json.",
                ex);
        }
    }

    private static bool IsLocalManifestUrl(string manifestUrl)
    {
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri))
            return false;

        return uri.IsLoopback
               || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
               || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Manifest> LoadFromCacheOrThrowAsync(string source, bool allowUnsigned, CancellationToken ct)
    {
        if (!File.Exists(Paths.ManifestCachePath))
            throw new UpdateFlowException("Manifest no disponible.", "No existe cache local de manifest para continuar.");

        var cachedJson = await File.ReadAllTextAsync(Paths.ManifestCachePath, ct);
        var cachedManifest = DeserializeManifest(cachedJson, source);
        ManifestSignatureVerifier.VerifyOrThrow(cachedJson, cachedManifest, allowUnsigned);
        return cachedManifest;
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

    private static Manifest DeserializeManifest(string json, string source)
    {
        var manifest = JsonSerializer.Deserialize<Manifest>(json, SerializerOptions());
        if (manifest is null)
            throw new UpdateFlowException("Manifest inválido.", $"No se pudo deserializar el manifest de {source}.");

        if (string.IsNullOrWhiteSpace(manifest.BaseUrl))
            throw new UpdateFlowException("Manifest inválido.", $"El manifest de {source} no contiene baseUrl.");

        if (manifest.Files is null)
            throw new UpdateFlowException("Manifest inválido.", $"El manifest de {source} no contiene archivos.");

        return manifest;
    }

    private static JsonSerializerOptions SerializerOptions() => new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class LauncherSettings
    {
        public string ManifestUrl { get; set; } = "";
        public bool AllowUnsignedManifest { get; set; }
    }
}