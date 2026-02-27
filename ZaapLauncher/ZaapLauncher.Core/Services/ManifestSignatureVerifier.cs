using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ZaapLauncher.Core.Models;

namespace ZaapLauncher.Core.Services;

public static class ManifestSignatureVerifier
{
    // Reemplazar por tu clave pública de release.
    private const string EmbeddedRsaPublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MFwwDQYJKoZIhvcNAQEBBQADSwAwSAJBAK2hHhM3V5sQ7gNQ4hJv9gXhW7yQjJxY
xz0I9v7+L2jWD2Rj2o9F7hXyH3x6dWq7z1e4fL2YlQ6O8t1kJ4H8u6MCAwEAAQ==
-----END PUBLIC KEY-----
""";

    public static void VerifyOrThrow(string rawManifestJson, Manifest manifest, bool allowUnsigned)
    {
        if (string.IsNullOrWhiteSpace(manifest.Signature) || string.IsNullOrWhiteSpace(manifest.SignatureAlgorithm))
        {
            if (allowUnsigned)
                return;

            throw new UpdateFlowException(
                "Manifest sin firma.",
                "El manifest no está firmado. Configura una firma RSA/Ed25519 o habilita allowUnsignedManifest temporalmente.");
        }

        var canonicalPayload = BuildCanonicalPayload(manifest);
        var payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        var signatureBytes = Convert.FromBase64String(manifest.Signature);

        var ok = manifest.SignatureAlgorithm.Trim().ToUpperInvariant() switch
        {
            "RSA-SHA256" => VerifyRsa(payloadBytes, signatureBytes),
            _ => false
        };

        if (!ok)
            throw new UpdateFlowException("Firma de manifest inválida.", "No se pudo validar la firma criptográfica del manifest.");
    }

    public static string BuildCanonicalPayload(Manifest manifest)
    {
        var payload = new
        {
            version = manifest.Version,
            baseUrl = manifest.BaseUrl,
            files = manifest.Files.Select(f => new
            {
                path = f.Path,
                url = f.Url,
                sha256 = f.Sha256,
                size = f.Size
            })
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    private static bool VerifyRsa(byte[] payload, byte[] signature)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(EmbeddedRsaPublicKeyPem);
        return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}