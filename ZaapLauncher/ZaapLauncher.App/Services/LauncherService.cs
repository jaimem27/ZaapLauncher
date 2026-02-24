using System.Diagnostics;
using System.IO;

namespace ZaapLauncher.App.Services;

public sealed class LauncherService
{
    public void Launch(string exePath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Normal
        };

        Process.Start(psi);
    }
}