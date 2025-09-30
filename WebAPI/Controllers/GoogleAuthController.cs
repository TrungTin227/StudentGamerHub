using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public sealed class GoogleAuthController : ControllerBase
    {
        private readonly IGoogleAuthService _google;

        public GoogleAuthController(IGoogleAuthService google) => _google = google;

        // POST /api/auth/google/login
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] GoogleLoginRequest req, CancellationToken ct)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = Request.Headers.UserAgent.ToString();

            var r = await _google.LoginAsync(req, ip, ua, ct);
            if (!r.IsSuccess) return this.ToActionResult(r);

            // set refresh cookie + CSRF giống login thường
            Response.SetAuthCookies(r.Value.RefreshToken, r.Value.RefreshExpiresAtUtc, CsrfService.NewToken());

            return Ok(new AccessTokenResponse(r.Value.AccessToken, r.Value.AccessExpiresAtUtc));
        }
    }
}
