using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZaapLauncher.App.Models;

namespace ZaapLauncher.App.Services;

public sealed class ManifestService
{
    private static readonly HttpClient Http = new();

    // Reemplazar por endpoint real.
    private const string ManifestUrl = "https://example.com/manifest.json";

    public async Task<Manifest> FetchAsync(CancellationToken ct)
    {
        using var resp = await Http.GetAsync(ManifestUrl, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        var manifest = JsonSerializer.Deserialize<Manifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (manifest is null)
            throw new InvalidOperationException("No se pudo deserializar el manifest.");

        return manifest;
    }
}
