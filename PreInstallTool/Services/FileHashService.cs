using System.IO;
using System.Security.Cryptography;
using PreInstallTool.Localization;

namespace PreInstallTool.Services;

public static class FileHashService
{
    public static string ComputeSha256Hex(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static void VerifySha256OrThrow(string filePath, string? expectedSha256, string fileLabel)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            return;
        }

        var expected = expectedSha256.Trim().ToLowerInvariant();
        var actual = ComputeSha256Hex(filePath);
        if (actual.Equals(expected, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            LocalizationService.Format("DownloadHashMismatch", fileLabel, expected, actual));
    }
}
