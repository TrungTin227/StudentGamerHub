using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class WalletController : ControllerBase
{
    private readonly IWalletReadService _walletReadService;
    private readonly IPaymentService _paymentService;

    public WalletController(IWalletReadService walletReadService, IPaymentService paymentService)
    {
        _walletReadService = walletReadService ?? throw new ArgumentNullException(nameof(walletReadService));
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
    }

    [HttpGet]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(WalletSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Get(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return this.ToActionResult(Result<WalletSummaryDto>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _walletReadService.GetAsync(userId.Value, ct).ConfigureAwait(false);
        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    [HttpGet("platform")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(WalletSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetPlatformWallet(CancellationToken ct)
    {
        var result = await _walletReadService.GetPlatformWalletAsync(ct).ConfigureAwait(false);
        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    [HttpPost("topups")]
    [EnableRateLimiting("PaymentsWrite")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> CreateTopUp([FromBody] WalletTopUpRequestDto request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return this.ToActionResult(Result<Guid>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _paymentService.CreateWalletTopUpIntentAsync(userId.Value, request.AmountCents, ct).ConfigureAwait(false);
        return this.ToActionResult(result, v => new { paymentIntentId = v }, StatusCodes.Status201Created);
    }
}
