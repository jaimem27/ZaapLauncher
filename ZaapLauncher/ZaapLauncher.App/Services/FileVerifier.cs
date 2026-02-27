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
    public Dictionary<string, InstallFileState> VerifiedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class FileVerifier
{
    private readonly InstallStateService _installStateService = new();

    public Task<VerifyResult> VerifyAsync(
        string installDir,
        Manifest manifest,
        IProgress<UpdateProgress> progress,
        CancellationToken ct,
        double basePercent,
        double spanPercent,
        bool forceStrongVerification)
    {
        return Task.Run(() =>
        {
            var result = new VerifyResult();
            var total = manifest.Files.Count;
            var installState = _installStateService.LoadAsync(ct).GetAwaiter().GetResult();
            var hasTrustedManifestState =
                !forceStrongVerification &&
                installState is not null &&
                string.Equals(installState.ManifestVersion, manifest.Version, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(installDir);

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
                FileInfo? info = null;

                if (ok && file.Size > 0)
                {
                    info = new FileInfo(fullPath);
                    ok = info.Length == file.Size;
                }

                if (ok && !string.IsNullOrWhiteSpace(file.Sha256))
                {
                    info ??= new FileInfo(fullPath);
                    var relativePath = Normalize(file.Path);

                    var hasReusableFingerprint =
                        installState?.Files.TryGetValue(relativePath, out var cached) == true &&
                        cached.Size == info.Length &&
                        cached.LastWriteTimeUtcTicks == info.LastWriteTimeUtc.Ticks &&
                        string.Equals(cached.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase);

                    var mustHash = forceStrongVerification || (!hasTrustedManifestState && !hasReusableFingerprint);
                    if (mustHash)
                    {
                        var localHash = Sha256File(fullPath, ct);
                        ok = string.Equals(localHash, file.Sha256, StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (!ok)
                {
                    result.MissingOrInvalid.Add(file);
                }
                else
                {
                    info ??= new FileInfo(fullPath);
                    result.VerifiedFiles[Normalize(file.Path)] = new InstallFileState
                    {
                        Size = info.Length,
                        LastWriteTimeUtcTicks = info.LastWriteTimeUtc.Ticks,
                        Sha256 = file.Sha256
                    };
                }

                done++;
                var percent = basePercent + (spanPercent * done / (double)total);
                progress.Report(new UpdateProgress(UpdateStage.VerifyFiles, percent,
                    "Escaneando el grimorio…", $"Verificando {done}/{total}"));
            }

            return result;
        }, ct);
    }

    public async Task QuickCheckAsync(string installDir, Manifest manifest, CancellationToken ct, bool includeHashChecks = true)
    {
        if (includeHashChecks)
        {
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = 3
            };

            await Parallel.ForEachAsync(manifest.Files, parallelOptions, async (file, token) =>
            {
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
                    var hash = await Sha256FileAsync(fullPath, token);
                    if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new UpdateFlowException("Comprobación final fallida.", $"Hash inválido en {file.Path}.");
                }
            });

            return;
        }

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
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 256 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Sha256File(string path, CancellationToken ct)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 256 * 1024, FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        ct.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

}