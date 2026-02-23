using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ZaapLauncher.App.Models;

namespace ZaapLauncher.App.Services;

public sealed class VerifyResult
{
    public List<ManifestFile> MissingOrInvalid { get; } = new();
}

public sealed class FileVerifier
{
    public Task<VerifyResult> VerifyAsync(
        string installDir,
        Manifest manifest,
        IProgress<UpdateProgress> progress,
        CancellationToken ct,
        double basePercent,
        double spanPercent)
    {
        return Task.Run(() =>
        {
            var result = new VerifyResult();
            var total = manifest.Files.Count;

            if (total == 0)
            {
                progress.Report(new UpdateProgress(UpdateStage.VerifyFiles, basePercent + spanPercent,
                    "Escaneando el grimorio…", "Sin archivos por verificar."));
                return result;
            }

            var done = 0;
            foreach (var file in manifest.Files)
            {
                ct.ThrowIfCancellationRequested();

                var fullPath = Path.Combine(installDir, file.Path);
                var ok = File.Exists(fullPath);

                if (ok && file.Size > 0)
                {
                    var info = new FileInfo(fullPath);
                    ok = info.Length == file.Size;
                }

                if (ok && !string.IsNullOrWhiteSpace(file.Sha256))
                {
                    var localHash = Sha256File(fullPath, ct);
                    ok = string.Equals(localHash, file.Sha256, StringComparison.OrdinalIgnoreCase);
                }

                if (!ok)
                    result.MissingOrInvalid.Add(file);

                done++;
                var percent = basePercent + (spanPercent * done / (double)total);
                progress.Report(new UpdateProgress(UpdateStage.VerifyFiles, percent,
                    "Escaneando el grimorio…", $"Verificando {done}/{total}"));
            }

            return result;
        }, ct);
    }

    public async Task QuickCheckAsync(string installDir, Manifest manifest, CancellationToken ct)
    {
        foreach (var file in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();

            var fullPath = Path.Combine(installDir, file.Path);
            if (!File.Exists(fullPath))
                throw new UpdateFlowException("Comprobación final fallida.", $"No se encontró {file.Path} tras actualizar.");

            if (file.Size > 0)
            {
                var info = new FileInfo(fullPath);
                if (info.Length != file.Size)
                    throw new UpdateFlowException("Comprobación final fallida.", $"Tamaño inesperado en {file.Path}.");
            }

            if (!string.IsNullOrWhiteSpace(file.Sha256))
            {
                var hash = await Sha256FileAsync(fullPath, ct);
                if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new UpdateFlowException("Comprobación final fallida.", $"Hash inválido en {file.Path}.");
            }
        }
    }

    public static async Task<string> Sha256FileAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Sha256File(string path, CancellationToken ct)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        ct.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

}