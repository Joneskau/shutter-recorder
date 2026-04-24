using System;
using System.Security.Cryptography;
using System.Text;

namespace Shutter.App;

public static class StealthFilenameService
{
    public static string EnsureSalt(StealthSettings stealth)
    {
        if (string.IsNullOrWhiteSpace(stealth.FilenameRandomizationSalt))
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            stealth.FilenameRandomizationSalt = Convert.ToHexString(bytes).ToLowerInvariant();
        }

        if (stealth.InstallTimestampMs is null or <= 0)
        {
            stealth.InstallTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        return stealth.FilenameRandomizationSalt;
    }

    public static string BuildFileStem(StealthSettings stealth)
    {
        if (!string.Equals(stealth.FilenameStyle, "random", StringComparison.OrdinalIgnoreCase))
        {
            return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        }

        var salt = EnsureSalt(stealth);
        var installTs = stealth.InstallTimestampMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var input = $"{installTs}{salt}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant()[..8];
    }
}
