using DTOs.Registrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IPaymentReadService _paymentReadService;

    public PaymentsController(IPaymentService paymentService, IPaymentReadService paymentReadService)
    {
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _paymentReadService = paymentReadService ?? throw new ArgumentNullException(nameof(paymentReadService));
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

    [HttpPost("{intentId:guid}/vnpay/checkout")]
    [EnableRateLimiting("PaymentsWrite")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> CreateVnPayCheckout(Guid intentId, [FromBody] VnPayCheckoutRequest? request, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return this.ToActionResult(Result<string>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
        var returnUrl = request?.ReturnUrl;

        var result = await _paymentService.CreateHostedCheckoutUrlAsync(currentUserId.Value, intentId, returnUrl, clientIp, ct).ConfigureAwait(false);
        return this.ToActionResult(result, v => new { paymentUrl = v }, StatusCodes.Status200OK);
    }

    [HttpPost("webhooks/vnpay")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting("PaymentsWebhook")]
    [ProducesResponseType(typeof(VnPayWebhookResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult> VnPayWebhook(CancellationToken ct)
    {
        var result = await _paymentService.HandleVnPayCallbackAsync(Request.Query, null, ct).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            return Ok(new VnPayWebhookResponse { RspCode = "00", Message = "Confirm success" });
        }

        // Map error codes to VNPAY response codes
        var rspCode = result.Error.Code switch
        {
            Error.Codes.NotFound => "01",
            Error.Codes.Validation => "97",
            Error.Codes.Conflict => "94",
            _ => "99"
        };

        return Ok(new VnPayWebhookResponse { RspCode = rspCode, Message = result.Error.Message });
    }

    [HttpGet("vnpay/return")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult VnPayReturn()
    {
        var vnpResponseCode = Request.Query["vnp_ResponseCode"].ToString();
        var vnpTxnRef = Request.Query["vnp_TxnRef"].ToString();

        if (vnpResponseCode == "00" && Guid.TryParseExact(vnpTxnRef, "N", out var intentId))
        {
            // Redirect to SPA with success status
            return Redirect($"/payment/result?status=success&intentId={intentId}");
        }

        // Redirect to SPA with failure status
        return Redirect($"/payment/result?status=failed");
    }
}

public sealed record VnPayCheckoutRequest
{
    public string? ReturnUrl { get; set; }
}

public sealed record VnPayWebhookResponse
{
    public string RspCode { get; set; } = default!;
    public string Message { get; set; } = default!;
}
