using System.Security.Cryptography;
using System.Text;

namespace CsvProcessing.Api.Infrastructure;

public static class ApiKeyComparer
{
    public static bool Matches(string presentedKey, IEnumerable<string> configuredKeys)
    {
        if (string.IsNullOrEmpty(presentedKey))
        {
            return false;
        }

        var presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey));

        var matched = false;
        foreach (var configuredKey in configuredKeys)
        {
            if (string.IsNullOrEmpty(configuredKey))
            {
                continue;
            }

            var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));

            if (CryptographicOperations.FixedTimeEquals(presentedHash, configuredHash))
            {
                matched = true;
            }
        }

        return matched;
    }
}
