using System.Collections.Generic;

namespace ZaapLauncher.App.Models;

public sealed class Manifest
{
    public string Version { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public List<ManifestFile> Files { get; set; } = new();
}

public sealed class ManifestFile
{
    public string Path { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
}