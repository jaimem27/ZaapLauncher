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

    // Reemplazar por endpoint real cuando se quiera habilitar backend.
    private const string ManifestUrl = "https://example.com/manifest.json";
    private static readonly string LocalManifestPath = Path.Combine(AppContext.BaseDirectory, "Assets", "update-sim", "manifest.local.json");

    public async Task<Manifest> FetchAsync(CancellationToken ct)
    {
        if (File.Exists(LocalManifestPath))
        {
            var localJson = await File.ReadAllTextAsync(LocalManifestPath, ct);
            return DeserializeManifest(localJson, "local manifest");
        }

        using var resp = await Http.GetAsync(ManifestUrl, ct);
        resp.EnsureSuccessStatusCode();

        var remoteJson = await resp.Content.ReadAsStringAsync(ct);
        return DeserializeManifest(remoteJson, ManifestUrl);
    }

    private static Manifest DeserializeManifest(string json, string source)
    {
        var manifest = JsonSerializer.Deserialize<Manifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (manifest is null)
            throw new UpdateFlowException("Manifest inválido.", $"No se pudo deserializar el manifest de {source}.");

        if (string.IsNullOrWhiteSpace(manifest.BaseUrl))
            throw new UpdateFlowException("Manifest inválido.", $"El manifest de {source} no contiene baseUrl.");

        if (manifest.Files is null)
            throw new UpdateFlowException("Manifest inválido.", $"El manifest de {source} no contiene archivos.");

        return manifest;
    }
}