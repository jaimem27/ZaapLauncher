using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

if (args.Length < 2)
{
    Console.WriteLine("Uso: ZaapLauncher.ManifestTool <sourceDir> <outputManifestPath> [baseUrl] [version] [--private-key <pemPath>]");
    return 1;
}

var sourceDir = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);
var baseUrl = args.Length >= 3 && !args[2].StartsWith("--", StringComparison.Ordinal) ? args[2] : "https://cdn.example.com/game/";
var version = args.Length >= 4 && !args[3].StartsWith("--", StringComparison.Ordinal) ? args[3] : $"build-{DateTime.UtcNow:yyyyMMddHHmmss}";
var privateKeyPath = ReadOption(args, "--private-key");

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
        return new ManifestFileDto(
            relativePath,
            string.Empty,
            ComputeSha256(path),
            info.Length);
    })
    .ToList();

var unsignedManifest = new UnsignedManifestDto(version, baseUrl, files);

string? signature = null;
string? signatureAlgorithm = null;

if (!string.IsNullOrWhiteSpace(privateKeyPath))
{
    var canonical = JsonSerializer.Serialize(unsignedManifest, JsonOptions());
    var bytes = Encoding.UTF8.GetBytes(canonical);
    var pem = await File.ReadAllTextAsync(Path.GetFullPath(privateKeyPath));

    using var rsa = RSA.Create();
    rsa.ImportFromPem(pem);
    var signedBytes = rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    signature = Convert.ToBase64String(signedBytes);
    signatureAlgorithm = "RSA-SHA256";
}

var manifest = new ManifestDto(
    unsignedManifest.Version,
    unsignedManifest.BaseUrl,
    signatureAlgorithm,
    signature,
    unsignedManifest.Files);

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

await File.WriteAllTextAsync(outputPath,
    JsonSerializer.Serialize(manifest, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    }) + Environment.NewLine);

Console.WriteLine($"Manifest generado: {outputPath}");
Console.WriteLine($"Archivos: {files.Count}");
Console.WriteLine(string.IsNullOrWhiteSpace(signature) ? "Firma: no" : "Firma: sí (RSA-SHA256)");
return 0;

static string? ReadOption(string[] args, string key)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }

    return null;
}

static string ComputeSha256(string path)
{
    using var stream = File.OpenRead(path);
    using var sha = SHA256.Create();
    var hash = sha.ComputeHash(stream);
    return Convert.ToHexString(hash).ToLowerInvariant();
}

static JsonSerializerOptions JsonOptions() => new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = false
};

file sealed record UnsignedManifestDto(string Version, string BaseUrl, List<ManifestFileDto> Files);
file sealed record ManifestDto(string Version, string BaseUrl, string? SignatureAlgorithm, string? Signature, List<ManifestFileDto> Files);
file sealed record ManifestFileDto(string Path, string Url, string Sha256, long Size);