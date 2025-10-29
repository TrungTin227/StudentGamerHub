using DTOs.Payments.PayOs;
using DTOs.Registrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

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

    public PaymentsController(IPaymentService paymentService, IPaymentReadService paymentReadService, IPayOsService payOsService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _paymentReadService = paymentReadService ?? throw new ArgumentNullException(nameof(paymentReadService));
        _payOsService = payOsService ?? throw new ArgumentNullException(nameof(payOsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        PayOsWebhookPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<PayOsWebhookPayload>(rawBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return this.ToActionResult(Result<string>.Failure(new Error(Error.Codes.Validation, "Invalid PayOS webhook payload.")));
        }

        if (payload is null)
        {
            return this.ToActionResult(Result<string>.Failure(new Error(Error.Codes.Validation, "Payload is required.")));
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
            _logger.LogWarning("PayOS webhook missing signature header.");
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
                return Redirect($"/payment/result?status=success&intentId={intentResult.Value}");
            }
            _logger.LogWarning("PayOS return unable to resolve intent for orderCode={OrderCode}", orderCodeValue);
        }

        return Redirect("/payment/result?status=failed");
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
}

public sealed record PayOsCheckoutRequest
{
    public Guid IntentId { get; init; }
    public string? ReturnUrl { get; init; }
}
