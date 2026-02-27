using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ZaapLauncher.Core.Services;

public sealed class UpdateStateService
{
    public async Task BeginAsync(string version, CancellationToken ct)
    {
        var state = new UpdateState
        {
            Version = version,
            StartedUtc = DateTime.UtcNow,
            Status = "applying"
        };

        await SaveAsync(state, ct);
    }

    public async Task MarkAppliedAsync(string relativePath, string backupPath, CancellationToken ct)
    {
        var state = await LoadAsync(ct) ?? new UpdateState();
        state.AppliedFiles.Add(new AppliedFileState
        {
            RelativePath = relativePath,
            BackupPath = backupPath
        });
        await SaveAsync(state, ct);
    }

    public async Task CompleteAsync(CancellationToken ct)
    {
        var state = await LoadAsync(ct);
        if (state is null)
            return;

        state.Status = "completed";
        await SaveAsync(state, ct);
        SafeDelete(Paths.UpdateStatePath);
    }

    public async Task RollbackAsync(string installDir, CancellationToken ct)
    {
        var state = await LoadAsync(ct);
        if (state is null)
            return;

        foreach (var applied in Enumerable.Reverse(state.AppliedFiles))
        {
            ct.ThrowIfCancellationRequested();
            var targetPath = Path.Combine(installDir, applied.RelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!string.IsNullOrWhiteSpace(applied.BackupPath) && File.Exists(applied.BackupPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(applied.BackupPath, targetPath, overwrite: true);
            }
            else
            {
                SafeDelete(targetPath);
            }
        }

        SafeDelete(Paths.UpdateStatePath);
    }

    public async Task RecoverIfPendingAsync(string installDir, CancellationToken ct)
    {
        var state = await LoadAsync(ct);
        if (state is null)
            return;

        if (!string.Equals(state.Status, "completed", StringComparison.OrdinalIgnoreCase))
            await RollbackAsync(installDir, ct);
    }

    private static async Task<UpdateState?> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(Paths.UpdateStatePath))
            return null;

        var json = await File.ReadAllTextAsync(Paths.UpdateStatePath, ct);
        return JsonSerializer.Deserialize<UpdateState>(json, SerializerOptions());
    }

    private static async Task SaveAsync(UpdateState state, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Paths.UpdateStatePath)!);
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(Paths.UpdateStatePath, json, ct);
    }

    private static JsonSerializerOptions SerializerOptions() => new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static void SafeDelete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private sealed class UpdateState
    {
        public string Version { get; set; } = "";
        public DateTime StartedUtc { get; set; }
        public string Status { get; set; } = "applying";
        public List<AppliedFileState> AppliedFiles { get; set; } = new();
    }

    private sealed class AppliedFileState
    {
        public string RelativePath { get; set; } = "";
        public string BackupPath { get; set; } = "";
    }
}