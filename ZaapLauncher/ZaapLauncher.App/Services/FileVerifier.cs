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

    public Task<int> CleanupOrphansAsync(string installDir, Manifest manifest, IReadOnlyCollection<string> whitelist, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(installDir))
                return 0;

            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in manifest.Files)
                expected.Add(Normalize(item.Path));

            var removed = 0;
            foreach (var file in Directory.EnumerateFiles(installDir, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var rel = Normalize(Path.GetRelativePath(installDir, file));
                if (expected.Contains(rel) || IsWhitelisted(rel, whitelist))
                    continue;

                File.Delete(file);
                removed++;
            }

            return removed;
        }, ct);
    }

    private static bool IsWhitelisted(string rel, IReadOnlyCollection<string> whitelist)
    {
        foreach (var item in whitelist)
        {
            var normalized = Normalize(item);
            if (rel.Equals(normalized, StringComparison.OrdinalIgnoreCase) || rel.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

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