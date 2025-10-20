using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Common.Emailing.Interfaces;

namespace Services.Common.Emailing.Implementations;

public sealed class ResendEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> emailOptions,
    IOptions<ResendOptions> resendOptions,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient = httpClient;
    private readonly EmailOptions _emailOptions = emailOptions.Value;
    private readonly ResendOptions _resendOptions = resendOptions.Value;
    private readonly ILogger<ResendEmailSender> _logger = logger;

    public async Task SendAsync(EmailMessage msg, CancellationToken ct = default)
    {
        var requestBody = BuildPayload(msg);
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, SerializerOptions), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _resendOptions.ApiKey);

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        _logger.LogError("Resend API returned {StatusCode}: {Body}", (int)response.StatusCode, body);
        response.EnsureSuccessStatusCode();
    }

    private object BuildPayload(EmailMessage msg)
    {
        var from = msg.From ?? new EmailAddress(_emailOptions.DefaultFrom, _emailOptions.DefaultFromName);

        string? FormatAddress(EmailAddress address)
            => string.IsNullOrWhiteSpace(address.DisplayName)
                ? address.Address
                : $"{address.DisplayName} <{address.Address}>";

        var to = msg.To.Select(FormatAddress).Where(static a => a is not null).ToArray();
        if (to.Length == 0)
        {
            throw new InvalidOperationException("Email message must contain at least one recipient.");
        }

        var cc = msg.Cc.Select(FormatAddress).Where(static a => a is not null).ToArray();
        var bcc = msg.Bcc.Select(FormatAddress).Where(static a => a is not null).ToArray();

        var attachments = msg.Attachments.Count == 0
            ? null
            : msg.Attachments.Select(a => new ResendAttachment(a.FileName, Convert.ToBase64String(a.Content), a.ContentType)).ToArray();

        return new ResendRequest(
            FormatAddress(from)!,
            to,
            cc.Length == 0 ? null : cc,
            bcc.Length == 0 ? null : bcc,
            msg.ReplyTo is null ? null : FormatAddress(msg.ReplyTo),
            msg.Subject,
            msg.TextBody,
            msg.HtmlBody,
            attachments);
    }

    private sealed record ResendRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("cc")] string[]? Cc,
        [property: JsonPropertyName("bcc")] string[]? Bcc,
        [property: JsonPropertyName("reply_to")] string? ReplyTo,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("html")] string? Html,
        [property: JsonPropertyName("attachments")] ResendAttachment[]? Attachments);

    private sealed record ResendAttachment(
        [property: JsonPropertyName("filename")] string Filename,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("content_type")] string? ContentType);
}
