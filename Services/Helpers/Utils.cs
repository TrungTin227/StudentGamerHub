using System.Security.Cryptography;
using System.Text;

namespace Services.Helpers;

/// <summary>
/// Utility methods for VNPAY integration.
/// </summary>
public static class Utils
{
    /// <summary>
    /// Compute HMAC-SHA512 signature.
    /// </summary>
    public static string HmacSHA512(string secret, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);

        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}
