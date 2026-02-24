using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZaapLauncher.App.Models;

namespace ZaapLauncher.App.Services;

public sealed class UpdateOrchestrator
{
    private static readonly string[] RepairWhitelist =
    [
        "settings.json",
        "logs",
        "screenshots",
        "config.ini"
    ];

    private readonly ManifestService _manifestService = new();
    private readonly FileVerifier _fileVerifier = new();
    private readonly DownloadService _downloadService = new();
    private readonly PatchApplier _patchApplier = new();
    private readonly UpdateStateService _stateService = new();


    public async Task RunAsync(
        string installDir,
        bool forceRepair,
        IProgress<UpdateProgress> progress,
        CancellationToken ct)
    {

        try
        {
            Directory.CreateDirectory(Paths.LauncherDataDir);
            Directory.CreateDirectory(Paths.TempDownloadDir);
            Directory.CreateDirectory(Paths.BackupDir);

            await _stateService.RecoverIfPendingAsync(installDir, ct);

            progress.Report(new UpdateProgress(UpdateStage.FetchManifest, 2,
                "Comprobando versión…", "Descargando manifiest"));

            var manifest = await _manifestService.FetchAsync(ct);

            progress.Report(new UpdateProgress(UpdateStage.VerifyFiles, 10,
                "Verificando archivos…", forceRepair ? "Modo repair: rehash + limpieza" : "Verificación incremental"));

            var verifyResult = await _fileVerifier.VerifyAsync(
                installDir,
                manifest,
                progress,
                ct,
                basePercent: 10,
                spanPercent: 25);

            if (forceRepair)
            {
                var removedOrphans = await _fileVerifier.CleanupOrphansAsync(installDir, manifest, RepairWhitelist, ct);
                if (removedOrphans > 0)
                {
                    progress.Report(new UpdateProgress(UpdateStage.VerifyFiles, 34,
                        "Limpiando archivos huérfanos…", $"Se eliminaron {removedOrphans} archivos no listados en manifest."));
                }
            }

            var toDownload = verifyResult.MissingOrInvalid;
            var totalBytes = toDownload.Sum(x => Math.Max(0, x.Size));

            if (toDownload.Count == 0)
            {
                progress.Report(new UpdateProgress(UpdateStage.FinalCheck, 98,
                    "Verificando estado final…", "Validación final"));

                await _fileVerifier.QuickCheckAsync(installDir, manifest, ct);

                progress.Report(new UpdateProgress(UpdateStage.Ready, 100,
                    "Portal estabilizado.", "Listo para entrar"));
                return;
            }

            progress.Report(new UpdateProgress(UpdateStage.Downloading, 35,
                "Descargando…",
                $"Descargando {toDownload.Count} archivos ({FormatBytes(totalBytes)})"));

            CleanTempDirectory(Paths.TempDownloadDir);

            var readyFiles = await _downloadService.DownloadAsync(
                Paths.TempDownloadDir,
                manifest.BaseUrl,
                toDownload,
                progress,
                ct,
                basePercent: 35,
                spanPercent: 55);

            await _stateService.BeginAsync(manifest.Version, ct);

            progress.Report(new UpdateProgress(UpdateStage.Applying, 90,
                "Aplicando…", "Realizando reemplazo transaccional"));

            await _patchApplier.ApplyAsync(
                installDir,
                Paths.BackupDir,
                readyFiles,
                _stateService,
                progress,
                ct,
                basePercent: 90,
                spanPercent: 8);

            progress.Report(new UpdateProgress(UpdateStage.FinalCheck, 98,
                 "Verificando…", "Comprobación final"));

            try
            {
                await _fileVerifier.QuickCheckAsync(installDir, manifest, ct);
            }
            catch
            {
                await _stateService.RollbackAsync(installDir, ct);
                throw;
            }

            await _stateService.CompleteAsync(ct);

            progress.Report(new UpdateProgress(UpdateStage.Ready, 100,
                "Portal estabilizado. Listo para entrar.", "Todo actualizado"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UpdateFlowException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new UpdateFlowException("La actualización falló.", "No se pudieron completar todas las etapas de actualización.", ex);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = Math.Max(0, bytes);
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private static void CleanTempDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.Delete(file);
    }
}