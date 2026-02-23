using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ZaapLauncher.App.Models;

namespace ZaapLauncher.App.Services;

public sealed class DownloadService
{
    private static readonly HttpClient Http = new();

    public async Task DownloadAsync(
        string installDir,
        string baseUrl,
        List<ManifestFile> files,
        IProgress<UpdateProgress> progress,
        CancellationToken ct,
        double basePercent,
        double spanPercent)
    {
        long totalBytes = 0;
        foreach (var file in files)
            totalBytes += Math.Max(0, file.Size);

        long downloadedBytes = 0;

        for (var i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var file = files[i];

            var url = BuildUrl(baseUrl, file.Path);
            var fullPath = Path.Combine(installDir, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            progress.Report(new UpdateProgress(
                UpdateStage.Downloading,
                basePercent + spanPercent * (downloadedBytes / (double)Math.Max(1, totalBytes)),
                "Cargando kamas al transportista…",
                $"Descargando {i + 1}/{files.Count}: {file.Path}"));

            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(fullPath);

            var buffer = new byte[64 * 1024];
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                downloadedBytes += read;

                var percent = basePercent + spanPercent * (downloadedBytes / (double)Math.Max(1, totalBytes));
                progress.Report(new UpdateProgress(
                    UpdateStage.Downloading,
                    percent,
                    "Cargando kamas al transportista…",
                    $"Recibiendo datos… ({i + 1}/{files.Count})"));
            }
        }
    }

    private static string BuildUrl(string manifestBaseUrl, string relativePath)
    {
        var baseUri = new Uri(EnsureTrailingSlash(manifestBaseUrl), UriKind.Absolute);
        return new Uri(baseUri, relativePath.Replace("\\", "/")).ToString();
    }

    private static string EnsureTrailingSlash(string value) => value.EndsWith('/') ? value : value + '/';
}
