using DTOs.Payments.PayOs;
using DTOs.Memberships;
using DTOs.Registrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Interfaces;
using Services.Configuration;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentReadService _paymentReadService;
    private readonly IPayOsService _payOsService;
    private readonly ILogger<PaymentsController> _logger;
    private readonly PayOsOptions _payOsOptions;

    public PaymentsController(
        IPaymentService paymentService,
        IPaymentReadService paymentReadService,
        IPayOsService payOsService,
        ILogger<PaymentsController> logger,
        IOptionsSnapshot<PayOsOptions> payOsOptions)
    {
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _paymentReadService = paymentReadService ?? throw new ArgumentNullException(nameof(paymentReadService));
        _payOsService = payOsService ?? throw new ArgumentNullException(nameof(payOsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _payOsOptions = payOsOptions?.Value ?? throw new ArgumentNullException(nameof(payOsOptions));
    }

    [HttpPost("{intentId:guid}/confirm")]
    [EnableRateLimiting("PaymentsWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Confirm(Guid intentId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return this.ToActionResult(Result.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _paymentService.ConfirmAsync(currentUserId.Value, intentId, ct).ConfigureAwait(false);
        return this.ToActionResult(result);
    }

    [HttpGet("{intentId:guid}")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(PaymentIntentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Get(Guid intentId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return this.ToActionResult(Result<PaymentIntentDto>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _paymentReadService.GetAsync(currentUserId.Value, intentId, ct).ConfigureAwait(false);
        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    [HttpPost("buy-membership/{planId:guid}")]
    [EnableRateLimiting("PaymentsWrite")]
    [ProducesResponseType(typeof(MembershipPurchaseResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> BuyMembership(Guid planId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return this.ToActionResult(Result<MembershipPurchaseResultDto>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _paymentService
            .BuyMembershipAsync(currentUserId.Value, planId, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, dto => dto, StatusCodes.Status200OK);
    }


    [HttpPost("payos/create")]
    [EnableRateLimiting("PaymentsWrite")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> CreatePayOsCheckout([FromBody] PayOsCheckoutRequest? request, CancellationToken ct)
    {
        if (request is null || request.IntentId == Guid.Empty)
        {
            return this.ToActionResult(Result<string>.Failure(new Error(Error.Codes.Validation, "A valid payment intent is required.")));
        }

        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return this.ToActionResult(Result<string>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
        var result = await _paymentService.CreateHostedCheckoutUrlAsync(currentUserId.Value, request.IntentId, request.ReturnUrl, clientIp, ct).ConfigureAwait(false);
        return this.ToActionResult(result, url => new { checkoutUrl = url }, StatusCodes.Status200OK);
    }

    [HttpPost("payos/webhook")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting("PaymentsWebhook")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> PayOsWebhook(CancellationToken ct)
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        }
        if (Request.Body.CanSeek)
        {
            Request.Body.Position = 0;
        }

        // Log raw webhook payload for debugging
        _logger.LogInformation("PayOS webhook received. Body={Body}, Headers={Headers}",
            rawBody,
            string.Join(", ", Request.Headers.Select(h => $"{h.Key}={h.Value}")));

        // Check if this is a test webhook from PayOS (empty body or special format)
        if (string.IsNullOrWhiteSpace(rawBody) || rawBody.Trim() == "{}" || rawBody.Trim() == "[]")
        {
            _logger.LogInformation("PayOS test webhook detected. Returning OK.");
            return Ok(new { status = "ok", message = "Test webhook received successfully" });
        }

        PayOsWebhookPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<PayOsWebhookPayload>(rawBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize PayOS webhook. Body={Body}", rawBody);
            // Return 200 OK even on JSON error to prevent PayOS from retrying test webhooks
            return Ok(new { status = "error", message = "Invalid JSON format" });
        }

        if (payload is null)
        {
            _logger.LogWarning("PayOS webhook payload is null after deserialization. Body={Body}", rawBody);
            return Ok(new { status = "error", message = "Payload is null" });
        }

        // If payload.Data is null, this is likely a test webhook
        if (payload.Data is null)
        {
            _logger.LogInformation("PayOS test webhook (no data field). Code={Code}, Desc={Desc}",
                payload.Code, payload.Desc);
            return Ok(new { status = "ok", message = "Test webhook received" });
        }

        string? signatureHeader = null;
        if (Request.Headers.TryGetValue("x-signature", out var signature))
        {
            signatureHeader = signature.ToString();
        }
        else if (Request.Headers.TryGetValue("X-Signature", out var signatureAlt))
        {
            signatureHeader = signatureAlt.ToString();
        }

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            _logger.LogWarning("PayOS webhook missing signature header. OrderCode={OrderCode}",
                payload.Data?.OrderCode);
        }

        var result = await _payOsService.HandleWebhookAsync(payload, rawBody, signatureHeader, ct).ConfigureAwait(false);
        Result<string> mapped;
        if (result.IsSuccess)
        {
            var statusLabel = result.Value == PayOsWebhookOutcome.Ignored ? "ignored" : "ok";
            mapped = Result<string>.Success(statusLabel);
        }
        else
        {
            mapped = Result<string>.Failure(result.Error);
        }

        return this.ToActionResult(mapped, v => new { status = v }, StatusCodes.Status200OK);
    }

    [HttpGet("payos/return")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> PayOsReturn(CancellationToken ct)
    {
        var status = Request.Query["status"].ToString();
        var orderCode = Request.Query["orderCode"].ToString();

        if (IsSuccessStatus(status) && long.TryParse(orderCode, out var orderCodeValue))
        {
            var intentResult = await _paymentReadService.ResolveIntentIdByOrderCodeAsync(orderCodeValue, ct).ConfigureAwait(false);
            if (intentResult.IsSuccess)
            {
                return Redirect(BuildResultUrl("success", intentResult.Value));
            }
            _logger.LogWarning("PayOS return unable to resolve intent for orderCode={OrderCode}", orderCodeValue);
        }

        return Redirect(BuildResultUrl("failed", null));
    }

    [HttpPost("debug/check-payment/{intentId:guid}")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CheckPaymentStatus(Guid intentId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return this.ToActionResult(Result<object>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var intentResult = await _paymentReadService.GetAsync(currentUserId.Value, intentId, ct).ConfigureAwait(false);
        if (intentResult.IsFailure)
        {
            return this.ToActionResult(Result<object>.Failure(intentResult.Error));
        }

        var intent = intentResult.Value;
        return Ok(new
        {
            intentId = intent.Id,
            status = intent.Status,
            orderCode = intent.OrderCode,
            amount = intent.AmountCents,
            purpose = intent.Purpose,
            createdAt = intent.CreatedAt,
            message = "If status is 'Pending' but you paid successfully, the webhook may not be working. Check logs or contact support."
        });
    }

    [HttpPost("debug/sync-payment/{intentId:guid}")]
    [EnableRateLimiting("PaymentsWrite")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> SyncPaymentFromPayOs(Guid intentId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return this.ToActionResult(Result<object>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        // Get payment intent
        var intentResult = await _paymentReadService.GetAsync(currentUserId.Value, intentId, ct).ConfigureAwait(false);
        if (intentResult.IsFailure)
        {
            return this.ToActionResult(Result<object>.Failure(intentResult.Error));
        }

        var intent = intentResult.Value;

        // If already succeeded, no need to sync
        if (intent.Status == PaymentIntentStatus.Succeeded)
        {
            return Ok(new { status = "already_completed", message = "Payment already confirmed." });
        }

        // Get payment info from PayOS
        var payosInfoResult = await _payOsService.GetPaymentInfoAsync(intent.OrderCode, ct).ConfigureAwait(false);
        if (payosInfoResult.IsFailure)
        {
            return this.ToActionResult(Result<object>.Failure(payosInfoResult.Error));
        }

        var payosInfo = payosInfoResult.Value;

        // Check if payment is successful on PayOS side
        if (!IsSuccessStatus(payosInfo.Status))
        {
            return Ok(new
            {
                status = "not_paid",
                payosStatus = payosInfo.Status,
                message = $"Payment not completed on PayOS. Current status: {payosInfo.Status}"
            });
        }

        // Create webhook payload to trigger payment confirmation
        var webhookPayload = new PayOsWebhookPayload
        {
            Code = "00",
            Desc = "success",
            Success = true,
            Data = new PayOsWebhookData
            {
                OrderCode = payosInfo.OrderCode,
                Amount = payosInfo.Amount,
                Description = $"Manual sync for order {payosInfo.OrderCode}",
                AccountNumber = "",
                Reference = payosInfo.Reference ?? payosInfo.OrderCode.ToString(),
                TransactionDateTime = payosInfo.TransactionDateTime ?? DateTimeOffset.UtcNow.ToString("o"),
                PaymentLinkId = payosInfo.Reference
            },
            Signature = "manual-sync"
        };

        // Process the webhook (skip signature validation by passing null)
        var webhookResult = await _payOsService.HandleWebhookAsync(
            webhookPayload,
            System.Text.Json.JsonSerializer.Serialize(webhookPayload),
            null, // Skip signature validation for manual sync
            ct
        ).ConfigureAwait(false);

        if (webhookResult.IsFailure)
        {
            _logger.LogWarning("Manual sync failed for intent {IntentId}. Error: {Error}",
                intentId, webhookResult.Error.Message);
            return this.ToActionResult(Result<object>.Failure(webhookResult.Error));
        }

        return Ok(new
        {
            status = "synced",
            webhookOutcome = webhookResult.Value.ToString(),
            message = "Payment synchronized successfully from PayOS."
        });
    }

    private static bool IsSuccessStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.Equals("PAID", StringComparison.OrdinalIgnoreCase)
            || status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)
            || status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildResultUrl(string status, Guid? intentId)
    {
        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = status
        };

        if (intentId.HasValue)
        {
            query["intentId"] = intentId.Value.ToString();
        }

        var basePath = "/payment/result";
        var frontendBase = _payOsOptions.FrontendBaseUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(frontendBase) && Uri.TryCreate(frontendBase, UriKind.Absolute, out var baseUri))
        {
            var targetUri = new Uri(baseUri, basePath);
            return QueryHelpers.AddQueryString(targetUri.ToString(), query);
        }

        return QueryHelpers.AddQueryString(basePath, query);
    }
}

public sealed record PayOsCheckoutRequest
{
    public Guid IntentId { get; init; }
    public string? ReturnUrl { get; init; }
}


