using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZaapLauncher.App.Models;

namespace ZaapLauncher.App.Services;

public sealed class PatchApplier
{
    public Task ApplyAsync(
        string installDir,
        List<ManifestFile> files,
        IProgress<UpdateProgress> progress,
        CancellationToken ct,
        double basePercent,
        double spanPercent)
    {
        ct.ThrowIfCancellationRequested();
        progress.Report(new UpdateProgress(UpdateStage.Applying, basePercent + spanPercent,
            "Encajando runas en el portal…", "Finalizando aplicación de archivos."));
        return Task.CompletedTask;
    }
}