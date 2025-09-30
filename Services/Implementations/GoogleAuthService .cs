using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Services.Implementations
{
    public sealed class GoogleAuthService : IGoogleAuthService
    {
        private readonly UserManager<User> _users;
        private readonly RoleManager<Role> _roles;
        private readonly ITokenService _tokens;
        private readonly GoogleAuthOptions _opt;

        public GoogleAuthService(
            UserManager<User> users,
            RoleManager<Role> roles,
            ITokenService tokens,
            IOptions<GoogleAuthOptions> opt)
        {
            _users = users;
            _roles = roles;
            _tokens = tokens;
            _opt = opt.Value;
        }

        public async Task<Result<TokenPairDto>> LoginAsync(GoogleLoginRequest req, string? ip = null, string? userAgent = null, CancellationToken ct = default)
        {
            // 1) Verify id_token
            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(
                    req.IdToken,
                    new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { _opt.ClientId } });
            }
            catch (Exception ex)
            {
                return Result<TokenPairDto>.Failure(new Error(Error.Codes.Unauthorized, $"Invalid Google token: {ex.Message}"));
            }

            if (!string.IsNullOrWhiteSpace(_opt.AllowedHostedDomain) &&
                !string.Equals(payload.HostedDomain, _opt.AllowedHostedDomain, StringComparison.OrdinalIgnoreCase))
            {
                return Result<TokenPairDto>.Failure(new Error(Error.Codes.Forbidden, "Google account is not in allowed domain."));
            }

            var googleSub = payload.Subject;
            var email = payload.Email;
            var emailVerified = payload.EmailVerified;
            if (string.IsNullOrWhiteSpace(email) || !emailVerified)
                return Result<TokenPairDto>.Failure(new Error(Error.Codes.Forbidden, "Google email is not verified."));

            // 2) Tìm theo external login → theo email → provision
            var user = await _users.FindByLoginAsync("Google", googleSub) ?? await _users.FindByEmailAsync(email);

            if (user is null)
            {
                var userName = await GenerateUniqueUserNameAsync(email);
                user = new User
                {
                    UserName = userName,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = BuildFullName(payload),
                    AvatarUrl = payload.Picture
                };

                var create = await _users.CreateAsync(user);
                if (!create.Succeeded) return create.ToResult<TokenPairDto>(default!, "Create user from Google failed");

                var rolesToAssign = await EnsureDefaultRoleAsync();
                var addRoles = await _users.AddToRolesAsync(user, rolesToAssign);
                if (!addRoles.Succeeded) return addRoles.ToResult<TokenPairDto>(default!, "Assign roles failed");

                var addLogin = await _users.AddLoginAsync(user, new UserLoginInfo("Google", googleSub, "Google"));
                if (!addLogin.Succeeded) return addLogin.ToResult<TokenPairDto>(default!, "Link Google login failed");
            }
            else
            {
                var logins = await _users.GetLoginsAsync(user);
                if (!logins.Any(l => l.LoginProvider == "Google" && l.ProviderKey == googleSub))
                {
                    var link = await _users.AddLoginAsync(user, new UserLoginInfo("Google", googleSub, "Google"));
                    if (!link.Succeeded) return link.ToResult<TokenPairDto>(default!, "Link Google login failed");
                }

                if (!user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                    await _users.UpdateAsync(user);
                }
            }

            // 3) Issue tokens
            var pair = await _tokens.IssueAsync(user, ip, userAgent, ct);
            return Result<TokenPairDto>.Success(new TokenPairDto(
                pair.AccessToken, pair.AccessExpiresAtUtc,
                pair.RefreshToken, pair.RefreshExpiresAtUtc));
        }

        // ---- helpers ----
        private static string BuildFullName(GoogleJsonWebSignature.Payload p)
            => !string.IsNullOrWhiteSpace(p.Name) ? p.Name! :
               string.Join(' ', new[] { p.GivenName, p.FamilyName }.Where(s => !string.IsNullOrWhiteSpace(s)));

        private async Task<string[]> EnsureDefaultRoleAsync()
        {
            const string fallback = "User";
            if (!await _roles.RoleExistsAsync(fallback))
            {
                var createRole = await _roles.CreateAsync(new Role { Name = fallback, NormalizedName = fallback.ToUpperInvariant() });
                if (!createRole.Succeeded && !await _roles.RoleExistsAsync(fallback))
                    throw new InvalidOperationException("Create fallback role 'User' failed.");
            }
            return new[] { fallback };
        }

        private async Task<string> GenerateUniqueUserNameAsync(string email)
        {
            var at = email.IndexOf('@');
            var local = (at > 0 ? email[..at] : email).Trim();
            var baseName = Regex.Replace(local.ToLowerInvariant(), @"[^a-z0-9._-]", string.Empty);
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "user";
            if (baseName.Length < 3) baseName = (baseName + "user")[..Math.Min(16, (baseName + "user").Length)];

            var candidate = baseName;
            var i = 0;
            while (await _users.FindByNameAsync(candidate) is not null)
            {
                i++;
                candidate = $"{baseName}{i}";
                if (i > 9999)
                {
                    candidate = $"{baseName}{Guid.NewGuid():N}".Substring(0, Math.Min(24, baseName.Length + 8));
                    break;
                }
            }
            return candidate;
        }
    }
}
