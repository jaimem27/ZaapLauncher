using System;
using System.IO;

namespace ZaapLauncher.App.Services;

public static class Paths
{
    public static readonly string LauncherDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZaapLauncher");

    public static readonly string InstallDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZaapLauncher", "Game");

    public static readonly string TempDownloadDir = Path.Combine(LauncherDataDir, "temp");

    public static readonly string BackupDir = Path.Combine(LauncherDataDir, "backup");

    public static readonly string SettingsPath = Path.Combine(LauncherDataDir, "settings.json");

    public static readonly string ManifestCachePath = Path.Combine(LauncherDataDir, "cache", "manifest.json");
}