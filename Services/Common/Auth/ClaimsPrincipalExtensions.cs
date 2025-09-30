using System.Security.Claims;

namespace Services.Common.Auth
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid? GetUserId(this ClaimsPrincipal? user)
        {
            if (user is null) return null;
            foreach (var t in new[] { ClaimTypes.NameIdentifier, "sub", "uid" })
            {
                var v = user.FindFirst(t)?.Value;
                if (!string.IsNullOrWhiteSpace(v) && Guid.TryParse(v, out var id))
                    return id;
            }
            return null;
        }

        public static string? GetEmail(this ClaimsPrincipal? user) =>
            user?.FindFirst(ClaimTypes.Email)?.Value ?? user?.FindFirst("email")?.Value;
    }
}
