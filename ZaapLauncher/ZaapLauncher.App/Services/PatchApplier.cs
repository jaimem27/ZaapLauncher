using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ZaapLauncher.App.Models;

namespace ZaapLauncher.App.Services;

public sealed class PatchApplier
{
    public Task ApplyAsync(
        string installDir,
        string backupRoot,
        List<DownloadedFile> files,
        UpdateStateService stateService,
        IProgress<UpdateProgress> progress,
        CancellationToken ct,
        double basePercent,
        double spanPercent)
    {
        return Task.Run(async () =>
        {
            Directory.CreateDirectory(installDir);

            var backupDir = Path.Combine(backupRoot, DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(backupDir);

            for (var i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var item = files[i];
                var targetPath = Path.Combine(installDir, item.ManifestFile.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                var percent = basePercent + spanPercent * ((i + 1) / (double)Math.Max(1, files.Count));
                progress.Report(new UpdateProgress(
                    UpdateStage.Applying,
                    percent,
                    "Aplicando actualización…",
                    $"Aplicando {i + 1}/{files.Count}: {item.ManifestFile.Path}"));

                try
                {
                    var backupPath = ApplyFile(item.ReadyPath, targetPath, backupDir, item.ManifestFile.Path);
                    await stateService.MarkAppliedAsync(item.ManifestFile.Path, backupPath, ct);
                }
                catch (IOException ex)
                {
                    throw new UpdateFlowException(
                        "No se pudo reemplazar un archivo en uso.",
                         $"Archivo bloqueado: {item.ManifestFile.Path}.\n1) Cerrar juego y reintentar.\n2) Reintentar (x3) desde el launcher.\n3) Abrir carpeta: {Path.GetDirectoryName(targetPath)}",
                        ex);
                }
            }
        }, ct);
    }

    private static string ApplyFile(string readyPath, string targetPath, string backupDir, string relativePath)
    {
        var backupPath = Path.Combine(backupDir, relativePath.Replace('/', Path.DirectorySeparatorChar) + ".bak");
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

        if (!File.Exists(targetPath))
        {
            File.Move(readyPath, targetPath, overwrite: true);
            return string.Empty;
        }

        File.Copy(targetPath, backupPath, overwrite: true);

        try
        {
            File.Replace(readyPath, targetPath, backupPath, ignoreMetadataErrors: true);
        }
        catch
        {
            File.Copy(readyPath, targetPath, overwrite: true);
            File.Delete(readyPath);
        }
        return backupPath;
    }
}