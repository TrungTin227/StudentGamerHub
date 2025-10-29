using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DTOs.Payments.PayOs;

namespace WebApi.Payments.Tests.Helpers;

internal static class PayOsTestHelper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public static PayOsWebhookPayload CreatePayload(
        long orderCode,
        long amount,
        string reference,
        string description,
        string code,
        bool success,
        DateTimeOffset timestamp,
        string secret)
    {
        var data = new PayOsWebhookData
        {
            OrderCode = orderCode,
            Amount = amount,
            Reference = reference,
            Description = description,
            TransactionDateTime = timestamp.ToString("O"),
            Currency = "VND",
            PaymentLinkId = $"plink_{orderCode}"
        };

        var signature = ComputePayloadSignature(data, secret);

        return new PayOsWebhookPayload
        {
            Code = code,
            Desc = description,
            Success = success,
            Data = data,
            Signature = signature
        };
    }

    public static string SerializeBody<T>(T payload)
        => JsonSerializer.Serialize(payload, SerializerOptions);

    public static string ComputeBodySignature(string rawBody, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputePayloadSignature(PayOsWebhookData data, string secret)
    {
        var json = JsonSerializer.Serialize(data, SerializerOptions);
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;

        string Normalize(object? value)
        {
            if (value is null)
            {
                return string.Empty;
            }

            if (value is JsonElement element)
            {
                return element.ValueKind switch
                {
                    JsonValueKind.Null => string.Empty,
                    JsonValueKind.Array => NormalizeJsonArray(element),
                    JsonValueKind.Object => NormalizeJsonObject(element),
                    _ => element.ToString() ?? string.Empty
                };
            }

            return value.ToString() ?? string.Empty;
        }

        var ordered = dict.OrderBy(kv => kv.Key, StringComparer.Ordinal);
        var query = string.Join("&", ordered.Select(kv => $"{kv.Key}={Normalize(kv.Value)}"));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = Encoding.UTF8.GetBytes(query);
        var hash = hmac.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();

        static string NormalizeJsonObject(JsonElement element)
        {
            var obj = JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText()) ?? new Dictionary<string, object?>();
            var normalized = obj
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            return JsonSerializer.Serialize(normalized, SerializerOptions);
        }

        static string NormalizeJsonArray(JsonElement element)
        {
            var array = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(element.GetRawText()) ?? new List<Dictionary<string, object?>>();
            var normalized = array
                .Select(item => item
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .ToDictionary(kv => kv.Key, kv => kv.Value))
                .ToList();
            return JsonSerializer.Serialize(normalized, SerializerOptions);
        }
    }
}
