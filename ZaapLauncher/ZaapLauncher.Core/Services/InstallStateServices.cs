using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ZaapLauncher.Core.Services;

public sealed class InstallStateService
{
    public async Task<InstallState?> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(Paths.InstallStatePath))
            return null;

        var json = await File.ReadAllTextAsync(Paths.InstallStatePath, ct);
        return JsonSerializer.Deserialize<InstallState>(json, SerializerOptions());
    }

    public async Task SaveAsync(InstallState state, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Paths.InstallStatePath)!);
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Paths.InstallStatePath, json, ct);
    }

    public Task MarkVerifiedAsync(string manifestVersion, Dictionary<string, InstallFileState> files, CancellationToken ct)
    {
        var state = new InstallState
        {
            ManifestVersion = manifestVersion,
            VerifiedAtUtc = DateTime.UtcNow,
            Files = files
        };

        return SaveAsync(state, ct);
    }

    private static JsonSerializerOptions SerializerOptions() => new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed class InstallState
{
    public string ManifestVersion { get; set; } = "";
    public DateTime VerifiedAtUtc { get; set; }
    public Dictionary<string, InstallFileState> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class InstallFileState
{
    public long Size { get; set; }
    public long LastWriteTimeUtcTicks { get; set; }
    public string Sha256 { get; set; } = "";
}