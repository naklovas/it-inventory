using System.Security.Cryptography;

namespace Kesinti.Ingest.Docx;

public static class FileHasher
{
    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hashBytes = SHA256.HashData(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
