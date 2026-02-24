using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZaapLauncher.App.Models;

namespace ZaapLauncher.App.Services;

public sealed class UpdateOrchestrator
{
    private readonly ManifestService _manifestService = new();
    private readonly FileVerifier _fileVerifier = new();
    private readonly DownloadService _downloadService = new();
    private readonly PatchApplier _patchApplier = new();

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

            progress.Report(new UpdateProgress(UpdateStage.FetchManifest, 2,
                "Abriendo runas de actualización…", "Descargando manifiesto"));

            var manifest = await _manifestService.FetchAsync(ct);

            progress.Report(new UpdateProgress(UpdateStage.VerifyFiles, 10,
                "Escaneando el grimorio…", forceRepair ? "Modo repair: re-verificación completa" : "Verificando archivos locales"));

            var verifyResult = await _fileVerifier.VerifyAsync(
                installDir,
                manifest,
                progress,
                ct,
                basePercent: 10,
                spanPercent: 25);

            var toDownload = verifyResult.MissingOrInvalid;
            var totalBytes = toDownload.Sum(x => Math.Max(0, x.Size));

            if (toDownload.Count == 0)
            {
                progress.Report(new UpdateProgress(UpdateStage.FinalCheck, 98,
                    "Portal estabilizándose…", "Validación final"));

                await _fileVerifier.QuickCheckAsync(installDir, manifest, ct);

                progress.Report(new UpdateProgress(UpdateStage.Ready, 100,
                    "Portal estabilizado.", "Listo para entrar"));
                return;
            }

            progress.Report(new UpdateProgress(UpdateStage.Downloading, 35,
                "Generando plan de actualización…",
                $"{toDownload.Count} archivos ({FormatBytes(totalBytes)}) por descargar"));

            CleanTempDirectory(Paths.TempDownloadDir);

            var readyFiles = await _downloadService.DownloadAsync(
                Paths.TempDownloadDir,
                manifest.BaseUrl,
                toDownload,
                progress,
                ct,
                basePercent: 35,
                spanPercent: 55);

            progress.Report(new UpdateProgress(UpdateStage.Applying, 90,
                "Aplicando actualización…", "Realizando reemplazo atómico de archivos"));

            await _patchApplier.ApplyAsync(
                installDir,
                Paths.BackupDir,
                readyFiles,
                progress,
                ct,
                basePercent: 90,
                spanPercent: 8);

            progress.Report(new UpdateProgress(UpdateStage.FinalCheck, 98,
                "Alineando coordenadas del Zaap…", "Comprobación final"));

            await _fileVerifier.QuickCheckAsync(installDir, manifest, ct);

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