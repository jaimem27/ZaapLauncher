using System.Diagnostics;

namespace ZaapLauncher.App.Services;

public sealed class LauncherService
{
    public void Launch(string exePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true
        };

        Process.Start(psi);
    }
}