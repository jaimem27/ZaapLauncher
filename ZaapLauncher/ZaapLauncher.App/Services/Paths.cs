using System;
using System.IO;

namespace ZaapLauncher.App.Services;

public static class Paths
{
    public static readonly string InstallDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZaapLauncher", "Game");
}
