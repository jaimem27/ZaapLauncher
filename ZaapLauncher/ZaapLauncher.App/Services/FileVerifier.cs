using System.Collections.Generic;
using System.IO;
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

    public Task QuickCheckAsync(string installDir, Manifest manifest, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
