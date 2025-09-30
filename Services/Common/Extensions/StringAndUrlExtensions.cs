using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace Services.Common.Extensions
{
    public static class StringAndUrlExtensions
    {

        public static string? NormalizeKeyword(this string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();

        public static string Base64UrlEncodeUtf8(this string token)
            => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        public static string DecodeTokenIfNeeded(this string raw)
        {
            try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(raw)); }
            catch { return raw; }
        }

        public static string BuildPublicLink(string baseUrl, string path, Dictionary<string, string?> query)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("callbackBaseUrl is required.", nameof(baseUrl));

            baseUrl = baseUrl.TrimEnd('/');
            path = path.TrimStart('/');

            var url = $"{baseUrl}/{path}";
            return QueryHelpers.AddQueryString(url, query);
        }
    }
}
