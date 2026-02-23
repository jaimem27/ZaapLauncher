using System;
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
            progress.Report(new UpdateProgress(UpdateStage.FetchManifest, 1,
                "Abriendo runas de actualización…", "Descargando manifiesto"));

            var manifest = await _manifestService.FetchAsync(ct);

            progress.Report(new UpdateProgress(UpdateStage.FetchManifest, 10,
                "Preparando portal de actualización…", forceRepair ? "Modo repair: re-verificación completa" : "Manifest cargado"));

            progress.Report(new UpdateProgress(UpdateStage.VerifyFiles, 10,
                "Escaneando el grimorio…", "Verificando archivos locales"));

            var verifyResult = await _fileVerifier.VerifyAsync(
                installDir,
                manifest,
                progress,
                ct,
                basePercent: 10,
                spanPercent: 25);

            var toDownload = verifyResult.MissingOrInvalid;

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
                "Cargando kamas al transportista…", $"Descargando {toDownload.Count} archivos"));

            await _downloadService.DownloadAsync(
                installDir,
                manifest.BaseUrl,
                toDownload,
                progress,
                ct,
                basePercent: 35,
                spanPercent: 55);

            progress.Report(new UpdateProgress(UpdateStage.Applying, 90,
                "Encajando runas en el portal…", "Aplicando cambios"));

            await _patchApplier.ApplyAsync(
                installDir,
                toDownload,
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
}