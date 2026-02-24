using System.Security.Cryptography;
using System.Text.Json;

if (args.Length < 2)
{
    Console.WriteLine("Uso: ZaapLauncher.ManifestTool <sourceDir> <outputManifestPath> [baseUrl] [version]");
    return 1;
}

var sourceDir = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);
var baseUrl = args.Length >= 3 ? args[2] : "https://cdn.example.com/game/";
var version = args.Length >= 4 ? args[3] : $"build-{DateTime.UtcNow:yyyyMMddHHmmss}";

if (!Directory.Exists(sourceDir))
{
    Console.Error.WriteLine($"No existe el directorio origen: {sourceDir}");
    return 2;
}

var files = Directory
    .EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories)
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .Select(path =>
    {
        var relativePath = Path.GetRelativePath(sourceDir, path).Replace('\\', '/');
        var info = new FileInfo(path);
        return new
        {
            path = relativePath,
            sha256 = ComputeSha256(path),
            size = info.Length
        };
    })
    .ToList();

var manifest = new
{
    version,
    baseUrl,
    files
};

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

await File.WriteAllTextAsync(outputPath,
    JsonSerializer.Serialize(manifest, new JsonSerializerOptions
    {
        WriteIndented = true
    }) + Environment.NewLine);

Console.WriteLine($"Manifest generado: {outputPath}");
Console.WriteLine($"Archivos: {files.Count}");
return 0;

static string ComputeSha256(string path)
{
    using var stream = File.OpenRead(path);
    using var sha = SHA256.Create();
    var hash = sha.ComputeHash(stream);
    return Convert.ToHexString(hash).ToLowerInvariant();
}