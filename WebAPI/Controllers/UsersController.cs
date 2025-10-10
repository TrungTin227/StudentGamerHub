using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public sealed class UsersController : ControllerBase
    {
        private readonly IUserService _users;
        public UsersController(IUserService users) => _users = users;

        // ---------- READ ----------
        // GET /api/users — search + paging
        [HttpGet]
        public async Task<ActionResult> Search([FromQuery] UserFilter filter, [FromQuery] PageRequest page, CancellationToken ct)
        {
            var r = await _users.SearchAsync(filter, page, ct);
            return this.ToActionResult(r);
        }

        // GET /api/users/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult> GetById(Guid id, CancellationToken ct)
        {
            var r = await _users.GetByIdAsync(id, ct);
            return this.ToActionResult(r);
        }

        // ---------- WRITE ----------
        // POST /api/users — create (201 Created + Location header)
        [HttpPost]
        public async Task<ActionResult> Create([FromBody] CreateUserAdminRequest req, CancellationToken ct)
        {
            var r = await _users.CreateAsync(req, ct);
            object? routeValues = r.IsSuccess ? new { id = r.Value!.Id } : null;
            return this.ToCreatedAtAction(r, nameof(GetById), routeValues);
        }

        // PUT /api/users/{id} — update profile fields
        [HttpPut("{id:guid}")]
        public async Task<ActionResult> Update(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
        {
            var r = await _users.UpdateAsync(id, req, ct);
            return this.ToActionResult(r);
        }

        // ---------- LOCKOUT ----------
        // PATCH /api/users/{id}/lockout — enable/disable lockout
        [HttpPatch("{id:guid}/lockout")]
        public async Task<ActionResult> SetLockout(Guid id, [FromBody] SetLockoutRequest req, CancellationToken ct)
        {
            var r = await _users.SetLockoutAsync(id, req, ct);
            return this.ToActionResult(r);
        }

        // ---------- ROLES ----------
        // POST /api/users/{id}/roles:replace — replace an entire role set
        [HttpPost("{id:guid}/roles:replace")]
        public async Task<ActionResult> ReplaceRoles(Guid id, [FromBody] ReplaceRolesRequest req, CancellationToken ct)
        {
            var r = await _users.ReplaceRolesAsync(id, req, ct);
            return this.ToActionResult(r);
        }

        // POST /api/users/{id}/roles:modify — add/remove roles incrementally
        [HttpPost("{id:guid}/roles:modify")]
        public async Task<ActionResult> ModifyRoles(Guid id, [FromBody] ModifyRolesRequest req, CancellationToken ct)
        {
            var r = await _users.ModifyRolesAsync(id, req, ct);
            return this.ToActionResult(r);
        }

        // ---------- PASSWORD FLOW ----------
        // POST /api/users/{id}/password:change — admin change password for user (needs current & new in body if you require)
        [HttpPost("{id:guid}/password:change")]
        public async Task<ActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest req, CancellationToken ct)
        {
            var r = await _users.ChangePasswordAsync(id, req, ct);
            return this.ToActionResult(r);
        }

        // POST /api/users/password:send-reset?callbackBaseUrl=... — send reset email (public)
        [AllowAnonymous]
        [HttpPost("password:send-reset")]
        public async Task<ActionResult> SendPasswordResetEmail([FromBody] ForgotPasswordRequest req, [FromQuery] string callbackBaseUrl, CancellationToken ct)
        {
            var r = await _users.SendPasswordResetEmailAsync(req, callbackBaseUrl, ct);
            return this.ToActionResult(r);
        }

        // POST /api/users/password:reset — reset password with token (public)
        [AllowAnonymous]
        [HttpPost("password:reset")]
        public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
        {
            var r = await _users.ResetPasswordAsync(req, ct);
            return this.ToActionResult(r);
        }

        // ---------- EMAIL CONFIRM FLOW ----------
        // POST /api/users/{id}/email:send-confirm?callbackBaseUrl=... — send confirm email (admin-triggered)
        [HttpPost("{id:guid}/email:send-confirm")]
        public async Task<ActionResult> SendEmailConfirm(Guid id, [FromQuery] string callbackBaseUrl, CancellationToken ct)
        {
            var r = await _users.SendEmailConfirmAsync(id, callbackBaseUrl, ct);
            return this.ToActionResult(r);
        }

        // POST /api/users/email:confirm — confirm email with token (public)
        [AllowAnonymous]
        [HttpPost("email:confirm")]
        public async Task<ActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest req, CancellationToken ct)
        {
            var r = await _users.ConfirmEmailAsync(req, ct);
            return this.ToActionResult(r);
        }

        // ---------- CHANGE EMAIL FLOW ----------
        // POST /api/users/{id}/email:send-change?callbackBaseUrl=... — send change-email confirmation to new email
        [HttpPost("{id:guid}/email:send-change")]
        public async Task<ActionResult> SendChangeEmailConfirm(Guid id, [FromBody] ChangeEmailRequest req, [FromQuery] string callbackBaseUrl, CancellationToken ct)
        {
            var r = await _users.SendChangeEmailConfirmAsync(id, req, callbackBaseUrl, ct);
            return this.ToActionResult(r);
        }

        // POST /api/users/email:confirm-change — confirm change email with token (public)
        [AllowAnonymous]
        [HttpPost("email:confirm-change")]
        public async Task<ActionResult> ConfirmChangeEmail([FromBody] ConfirmChangeEmailRequest req, CancellationToken ct)
        {
            var r = await _users.ConfirmChangeEmailAsync(req, ct);
            return this.ToActionResult(r);
        }

#if DEBUG
        // ---------- DEV-ONLY token helpers (do NOT expose in prod) ----------
        // POST /api/users/dev/password:token
        //[AllowAnonymous]
        //[HttpPost("dev/password:token")]
        //public async Task<ActionResult> GenerateForgotPasswordToken([FromBody] ForgotPasswordRequest req, CancellationToken ct)
        //{
        //    var r = await _users.GenerateForgotPasswordTokenAsync(req, ct);
        //    return this.ToActionResult(r);
        //}

        //// POST /api/users/{id}/dev/email:confirm-token
        //[AllowAnonymous]
        //[HttpPost("{id:guid}/dev/email:confirm-token")]
        //public async Task<ActionResult> GenerateEmailConfirmToken(Guid id, CancellationToken ct)
        //{
        //    var r = await _users.GenerateEmailConfirmTokenAsync(id, ct);
        //    return this.ToActionResult(r);
        //}

        //// POST /api/users/{id}/dev/email:change-token
        //[AllowAnonymous]
        //[HttpPost("{id:guid}/dev/email:change-token")]
        //public async Task<ActionResult> GenerateChangeEmailToken(Guid id, [FromBody] ChangeEmailRequest req, CancellationToken ct)
        //{
        //    var r = await _users.GenerateChangeEmailTokenAsync(id, req, ct);
        //    return this.ToActionResult(r);
        //}
#endif
    }
}
