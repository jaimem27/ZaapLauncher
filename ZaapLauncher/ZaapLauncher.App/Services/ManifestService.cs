using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZaapLauncher.App.Models;

namespace ZaapLauncher.App.Services;

public sealed class ManifestService
{
    private static readonly HttpClient Http = new();

    private const string DefaultManifestUrl = "C:\\Users\\unmis\\Desktop\\SHINSHEKAI\\Client Admin Ultimo";

    public async Task<Manifest> FetchAsync(CancellationToken ct)
    {
        var manifestUrl = ResolveManifestUrl();

        if (string.Equals(manifestUrl, DefaultManifestUrl, StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(Paths.ManifestCachePath))
            {
                throw new UpdateFlowException(
                    "Configura el endpoint de actualizaciones.",
                    "Define ZAAP_MANIFEST_URL o LocalAppData/ZaapLauncher/settings.json con manifestUrl antes de actualizar.");
            }

            var cachedJson = await File.ReadAllTextAsync(Paths.ManifestCachePath, ct);
            return DeserializeManifest(cachedJson, "cache local");
        }

        using var resp = await Http.GetAsync(manifestUrl, ct);
        resp.EnsureSuccessStatusCode();

        var remoteJson = await resp.Content.ReadAsStringAsync(ct);
        var manifest = DeserializeManifest(remoteJson, manifestUrl);

        Directory.CreateDirectory(Path.GetDirectoryName(Paths.ManifestCachePath)!);
        await File.WriteAllTextAsync(Paths.ManifestCachePath, remoteJson, ct);

        return manifest;
    }

    private static string ResolveManifestUrl()
    {
        var env = Environment.GetEnvironmentVariable("ZAAP_MANIFEST_URL");
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim();

        if (File.Exists(Paths.SettingsPath))
        {
            var json = File.ReadAllText(Paths.SettingsPath);
            var settings = JsonSerializer.Deserialize<LauncherSettings>(json, SerializerOptions());
            if (!string.IsNullOrWhiteSpace(settings?.ManifestUrl))
                return settings.ManifestUrl.Trim();
        }

        return DefaultManifestUrl;
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
    }
}