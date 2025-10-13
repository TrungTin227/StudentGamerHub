using DTOs.Registrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/payments")]
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
}
