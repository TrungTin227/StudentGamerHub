using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;    

namespace WebAPI.Security
{
    internal static class CsrfService
    {
        // Không còn + / =  => không cần URL-encode/ decode
        public static string NewToken() =>
            WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        public static bool Validate(string? headerValue, string? cookieValue)
        {
            if (string.IsNullOrWhiteSpace(headerValue) || string.IsNullOrWhiteSpace(cookieValue))
                return false;

            // Bước 1: gỡ URL-encode nếu có
            var hRaw = Uri.UnescapeDataString(headerValue.Trim());
            var cRaw = Uri.UnescapeDataString(cookieValue.Trim());

            // Bước 2: cố gắng decode cả hai về bytes (ưu tiên base64url, fallback base64 thường)
            if (TryDecodeToBytes(hRaw, out var hb) && TryDecodeToBytes(cRaw, out var cb))
            {
                if (hb.Length != cb.Length) return false;
                return CryptographicOperations.FixedTimeEquals(hb, cb);
            }

            // Fallback (rất hiếm khi cần): so sánh chuỗi sau normalize
            var hs = Encoding.UTF8.GetBytes(hRaw);
            var cs = Encoding.UTF8.GetBytes(cRaw);
            if (hs.Length != cs.Length) return false;
            return CryptographicOperations.FixedTimeEquals(hs, cs);
        }

        private static bool TryDecodeToBytes(string s, out byte[] bytes)
        {
            try { bytes = WebEncoders.Base64UrlDecode(s); return true; } catch { }
            try { bytes = Convert.FromBase64String(s); return true; } catch { }
            bytes = Array.Empty<byte>(); return false;
        }
    }
}
