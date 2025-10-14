using System.Collections.Specialized;
using System.Text;
using System.Web;

namespace Services.Helpers;

/// <summary>
/// VNPAY library for building and validating payment requests/responses.
/// Maintains sorted key-value pairs for signature generation.
/// </summary>
public sealed class VnPayLibrary
{
    private readonly SortedList<string, string> _requestData = new(StringComparer.Ordinal);
    private readonly SortedList<string, string> _responseData = new(StringComparer.Ordinal);

    public void AddRequestData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _requestData[key] = value;
        }
    }

    public void AddResponseData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _responseData[key] = value;
        }
    }

    public string? GetResponseData(string key)
    {
        return _responseData.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Create payment URL with all parameters and signature.
    /// </summary>
    public string CreateRequestUrl(string baseUrl, string hashSecret)
    {
        var data = new StringBuilder();

        foreach (var (key, value) in _requestData)
        {
            if (!string.IsNullOrEmpty(value))
            {
                data.Append(HttpUtility.UrlEncode(key));
                data.Append('=');
                data.Append(HttpUtility.UrlEncode(value));
                data.Append('&');
            }
        }

        var queryString = data.ToString();

        if (queryString.Length > 0)
        {
            queryString = queryString.Remove(queryString.Length - 1); // Remove trailing '&'
        }

        var signData = queryString;
        var vnpSecureHash = Utils.HmacSHA512(hashSecret, signData);

        return $"{baseUrl}?{queryString}&vnp_SecureHash={vnpSecureHash}";
    }

    /// <summary>
    /// Validate signature from callback/IPN.
    /// </summary>
    public bool ValidateSignature(string inputHash, string hashSecret)
    {
        var data = new StringBuilder();

        foreach (var (key, value) in _responseData)
        {
            if (!string.IsNullOrEmpty(value) && 
                !key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase) && 
                !key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            {
                data.Append(HttpUtility.UrlEncode(key));
                data.Append('=');
                data.Append(HttpUtility.UrlEncode(value));
                data.Append('&');
            }
        }

        var rawData = data.ToString();
        if (rawData.Length > 0)
        {
            rawData = rawData.Remove(rawData.Length - 1); // Remove trailing '&'
        }

        var computedHash = Utils.HmacSHA512(hashSecret, rawData);

        return computedHash.Equals(inputHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validate signature directly from query collection.
    /// </summary>
    public static bool ValidateSignatureFromQuery(NameValueCollection queryCollection, string hashSecret)
    {
        var vnpSecureHash = queryCollection["vnp_SecureHash"];
        if (string.IsNullOrEmpty(vnpSecureHash))
        {
            return false;
        }

        var lib = new VnPayLibrary();

        foreach (string key in queryCollection)
        {
            if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_", StringComparison.Ordinal))
            {
                var value = queryCollection[key];
                if (!string.IsNullOrEmpty(value))
                {
                    lib.AddResponseData(key, value);
                }
            }
        }

        return lib.ValidateSignature(vnpSecureHash, hashSecret);
    }
}
