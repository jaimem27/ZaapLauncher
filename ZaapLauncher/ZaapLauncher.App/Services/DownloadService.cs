using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ZaapLauncher.App.Models;

namespace ZaapLauncher.App.Services;

public sealed record DownloadedFile(ManifestFile ManifestFile, string ReadyPath);

public sealed class DownloadService
{
    private static readonly HttpClient Http = new();
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(700),
        TimeSpan.FromMilliseconds(1500)
    ];

    public async Task<List<DownloadedFile>> DownloadAsync(
        string tempRoot,
        string baseUrl,
        List<ManifestFile> files,
        IProgress<UpdateProgress> progress,
        CancellationToken ct,
        double basePercent,
        double spanPercent)
    {
        Directory.CreateDirectory(tempRoot);

        long totalBytes = 0;
        foreach (var file in files)
            totalBytes += Math.Max(0, file.Size);

        long downloadedBytes = 0;
        var downloadedFiles = new List<DownloadedFile>(files.Count);

        for (var i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var file = files[i];         

            progress.Report(new UpdateProgress(
                UpdateStage.Downloading,
                basePercent + spanPercent * Clamp01(downloadedBytes / (double)Math.Max(1, totalBytes)),
                "Descargando archivos del portal…",
                $"Descargando {i + 1}/{files.Count}: {file.Path}"));


            var beforeFileBytes = downloadedBytes;
            var readyPath = BuildReadyPath(tempRoot, file.Path);
            var bytesWritten = await DownloadWithRetriesAsync(baseUrl, file, readyPath, i, files.Count, read =>
            {
                downloadedBytes = beforeFileBytes + read;
                var percent = basePercent + spanPercent * Clamp01(downloadedBytes / (double)Math.Max(1, totalBytes));
                progress.Report(new UpdateProgress(
                    UpdateStage.Downloading,
                    percent,
                    "Cargando kamas al transportista…",
                    $"Recibiendo datos… ({i + 1}/{files.Count})"));
            }, ct);

            downloadedBytes = beforeFileBytes + bytesWritten;
            downloadedFiles.Add(new DownloadedFile(file, readyPath));
        }

        return downloadedFiles;
    }

    private static async Task<long> DownloadWithRetriesAsync(
        string baseUrl,
        ManifestFile file,
        string readyPath,
        int index,
        int total,
        Action<long> onBytesWritten,
        CancellationToken ct)
    {
        Exception? lastError = null;

        var partPath = readyPath + ".part";

        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(readyPath)!);
                SafeDelete(partPath);
                SafeDelete(readyPath);

                var fileSource = ResolveDownloadSource(baseUrl, file);
                var written = await DownloadRemoteFileAsync(fileSource, partPath, onBytesWritten, ct);

                await ValidateTempFileAsync(partPath, file, ct);
                File.Move(partPath, readyPath, overwrite: true);

                return written;
            }
            catch (OperationCanceledException)
            {
                SafeDelete(partPath);
                throw;
            }
            catch (Exception ex) when (attempt < RetryDelays.Length)
            {
                lastError = ex;
                SafeDelete(partPath);
                await Task.Delay(RetryDelays[attempt], ct);
            }
            catch (Exception ex)
            {
                lastError = ex;
                SafeDelete(partPath);
                break;
            }
        }

        var reason = lastError?.Message is { Length: > 0 }
            ? $" Motivo: {lastError.GetType().Name}: {lastError.Message}"
            : string.Empty;

        throw new UpdateFlowException(
            "No se pudo completar la descarga.",
            $"Falló {file.Path} tras varios reintentos ({index + 1}/{total}).{reason}",
            lastError);
    }

    private static string BuildReadyPath(string tempRoot, string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(tempRoot, normalized + ".ready");
    }

    private static string ResolveDownloadSource(string baseUrl, ManifestFile file)
    {
        if (Uri.TryCreate(file.Url, UriKind.Absolute, out var absoluteFromFile))
            return absoluteFromFile.ToString();

        if (!string.IsNullOrWhiteSpace(file.Url))
            return BuildUrl(baseUrl, file.Url);

        return BuildUrl(baseUrl, file.Path);
    }


    private static async Task<long> DownloadRemoteFileAsync(string url, string destinationPath, Action<long> onBytesWritten, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(RequestTimeout);

        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
        await using var output = File.Create(destinationPath);

        return await StreamCopyAsync(input, output, onBytesWritten, timeoutCts.Token);
    }

    private static async Task<long> StreamCopyAsync(Stream input, Stream output, Action<long> onBytesWritten, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        int read;
        long total = 0;

        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            total += read;
            onBytesWritten(total);
        }

        return total;
    }

    private static async Task ValidateTempFileAsync(string tempPath, ManifestFile file, CancellationToken ct)
    {
        if (file.Size > 0)
        {
            var size = new FileInfo(tempPath).Length;
            if (size != file.Size)
                throw new IOException($"Tamaño inválido en {file.Path}. Esperado {file.Size}, recibido {size}.");
        }

        if (!string.IsNullOrWhiteSpace(file.Sha256))
        {
            var hash = await FileVerifier.Sha256FileAsync(tempPath, ct);
            if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Hash SHA-256 inválido en {file.Path}. Esperado {file.Sha256}, recibido {hash}.");
        }
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore cleanup issues
        }
    }

    private static double Clamp01(double value) => Math.Min(1d, Math.Max(0d, value));

    private static string BuildUrl(string manifestBaseUrl, string relativePath)
    {
        var baseUri = new Uri(EnsureTrailingSlash(manifestBaseUrl), UriKind.Absolute);
        return new Uri(baseUri, relativePath.Replace("\\", "/")).ToString();
    }

    private static string EnsureTrailingSlash(string value) => value.EndsWith('/') ? value : value + '/';
}