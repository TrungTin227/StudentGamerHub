using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;


namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IUserService _users;

    public AuthController(IAuthService auth, IUserService users)
    {
        _auth = auth;
        _users = users;
    }

    // -------- TOKEN FLOWS --------

    [AllowAnonymous]
    [HttpPost("user-register")]
    public async Task<ActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var r = await _users.RegisterAsync(req, ct);
        if (!r.IsSuccess) return this.ToActionResult(r);

        // 201 Created; Location có thể trỏ tới /api/auth/me (cần login mới dùng)
        return this.ToCreatedAtAction(
            r,
            nameof(GetMe),
            routeValues: null,
            shape: u => new {
                u.Id,
                u.UserName,
                u.Email,
                u.FullName,
                u.EmailConfirmed
            }
        );
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers["User-Agent"].ToString();

        var r = await _auth.LoginAsync(req, ip, ua, ct);
        if (!r.IsSuccess) return this.ToActionResult(r);

        // Set HttpOnly refresh cookie + CSRF cookie
        Response.SetAuthCookies(
            r.Value.RefreshToken,
            r.Value.RefreshExpiresAtUtc, // consider passing DateTimeOffset if you already have it
            CsrfService.NewToken()
        );

        return Ok(new AccessTokenResponse(r.Value.AccessToken, r.Value.AccessExpiresAtUtc));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult> Refresh([FromBody] RefreshTokenRequest? req, CancellationToken ct)
    {
        var rt = Request.Cookies[AuthCookie.RefreshName] ?? req?.RefreshToken;
        if (string.IsNullOrWhiteSpace(rt))
            return Unauthorized(new { message = "Missing refresh token." });

        var csrfHeader = Request.Headers[AuthCookie.CsrfHeader].ToString();
        var csrfCookie = Request.Cookies[AuthCookie.CsrfName];
        if (!CsrfService.Validate(csrfHeader, csrfCookie))
            return Forbid(); // 403 (CSRF)

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers["User-Agent"].ToString();

        var r = await _auth.RefreshAsync(rt, ip, ua, ct);
        if (!r.IsSuccess) return this.ToActionResult(r);

        // keep same CSRF token on rotation
        Response.SetAuthCookies(r.Value.RefreshToken, r.Value.RefreshExpiresAtUtc, csrfCookie);

        return Ok(new AccessTokenResponse(r.Value.AccessToken, r.Value.AccessExpiresAtUtc));
    }

    [Authorize]
    [HttpPost("revoke")]
    public async Task<ActionResult> Revoke([FromBody] RevokeTokenRequest? req, CancellationToken ct)
    {
        var rt = Request.Cookies[AuthCookie.RefreshName] ?? req?.RefreshToken;
        if (!string.IsNullOrEmpty(rt))
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auth.RevokeAsync(rt, ip, ct);
        }

        Response.ClearAuthCookies(); // use your extension for consistency
        return Ok();
    }

    // -------- EMAIL CONFIRM (PUBLIC) --------
    // POST /api/auth/email:confirm — xác nhận email bằng token (public)
    [AllowAnonymous]
    [HttpPost("email:confirm")]
    public async Task<ActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest req, CancellationToken ct)
        => this.ToActionResult(await _users.ConfirmEmailAsync(req, ct));

    //// POST /api/auth/email:confirm-change — xác nhận đổi email bằng token (public)
    //[AllowAnonymous]
    //[HttpPost("email:confirm-change")]
    //public async Task<ActionResult> ConfirmChangeEmail([FromBody] ConfirmChangeEmailRequest req, CancellationToken ct)
    //    => this.ToActionResult(await _users.ConfirmChangeEmailAsync(req, ct));

    // -------- PASSWORD RESET (PUBLIC) --------
    // POST /api/auth/password:send-reset?callbackBaseUrl=... — gửi mail reset (public)
    [AllowAnonymous]
    [HttpPost("password:send-reset")]
    public async Task<ActionResult> SendPasswordResetEmail(
    [FromBody] ForgotPasswordRequest req,
    [FromQuery] string? callbackBaseUrl,  
    CancellationToken ct)
    {
        var r = await _users.SendPasswordResetEmailAsync(req, callbackBaseUrl, ct);
        return this.ToActionResult(r);
    }


    // POST /api/auth/password:reset — reset bằng token (public)
    [AllowAnonymous]
    [HttpPost("password:reset")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
        => this.ToActionResult(await _users.ResetPasswordAsync(req, ct));

    // -------- SELF-SERVICE ("me") --------
    // GET /api/auth/me — lấy hồ sơ chính mình
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult> GetMe(CancellationToken ct)
    {
        var id = GetUserIdOrThrow();
        return this.ToActionResult(await _users.GetByIdAsync(id, ct));
    }

    // PUT /api/auth/me — cập nhật hồ sơ chính mình
    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult> UpdateMe([FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        var id = GetUserIdOrThrow();
        return this.ToActionResult(await _users.UpdateAsync(id, req, ct));
    }

    // POST /api/auth/me/password:change — đổi mật khẩu chính mình
    [Authorize]
    [HttpPost("me/password:change")]
    public async Task<ActionResult> ChangeMyPassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        var id = GetUserIdOrThrow();
        return this.ToActionResult(await _users.ChangePasswordAsync(id, req, ct));
    }

    // POST /api/auth/me/email:send-confirm?callbackBaseUrl=... — gửi lại mail confirm
    [Authorize]
    [HttpPost("me/email:send-confirm")]
    public async Task<ActionResult> SendMyEmailConfirm([FromQuery] string callbackBaseUrl, CancellationToken ct)
    {
        var id = GetUserIdOrThrow();
        return this.ToActionResult(await _users.SendEmailConfirmAsync(id, callbackBaseUrl, ct));
    }

    // POST /api/auth/me/email:send-change?callbackBaseUrl=... — gửi mail xác nhận đổi email
    [Authorize]
    [HttpPost("me/email:send-change")]
    public async Task<ActionResult> SendMyChangeEmailConfirm([FromBody] ChangeEmailRequest req, [FromQuery] string callbackBaseUrl, CancellationToken ct)
    {
        var id = GetUserIdOrThrow();
        return this.ToActionResult(await _users.SendChangeEmailConfirmAsync(id, req, callbackBaseUrl, ct));
    }

    // -------- Helpers --------
    private Guid GetUserIdOrThrow()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idStr, out var id)) throw new UnauthorizedAccessException("Missing or invalid user id claim.");
        return id;
    }
}
