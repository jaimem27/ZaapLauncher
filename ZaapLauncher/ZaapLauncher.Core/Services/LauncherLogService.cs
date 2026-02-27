using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ZaapLauncher.Core.Services;

public sealed class LauncherLogService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public Task LogInfoAsync(string category, string message, CancellationToken ct = default) =>
        WriteAsync("INFO", category, message, null, ct);

    public Task LogWarningAsync(string category, string message, Exception? ex = null, CancellationToken ct = default) =>
        WriteAsync("WARN", category, message, ex, ct);

    public Task LogErrorAsync(string category, string message, Exception? ex = null, CancellationToken ct = default) =>
        WriteAsync("ERROR", category, message, ex, ct);

    private static async Task WriteAsync(string level, string category, string message, Exception? ex, CancellationToken ct)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{category}] {message}";
        if (ex is not null)
            line = $"{line}{Environment.NewLine}{ex}";

        var text = line + Environment.NewLine;

        await Gate.WaitAsync(ct);
        try
        {
            var targetPath = ResolveLogFilePath();
            await File.AppendAllTextAsync(targetPath, text, ct);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string ResolveLogFilePath()
    {
        var candidateDirs = new[]
        {
            Path.Combine(Paths.InstallDir, "logs"),
            Path.Combine(Paths.LauncherDataDir, "logs")
        };

        foreach (var dir in candidateDirs)
        {
            try
            {
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "logs.txt");
            }
            catch
            {
                // try next directory
            }
        }

        var fallbackDir = Path.GetTempPath();
        return Path.Combine(fallbackDir, "zaaplauncher-logs.txt");
    }
}