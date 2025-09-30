using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Services.Common.Services
{
    public sealed class CurrentUserService : ICurrentUserService
    {
        private static readonly string[] IdClaimTypes = { ClaimTypes.NameIdentifier, "sub", "uid" };
        private static readonly string[] RoleClaimTypes = { ClaimTypes.Role, "role", "roles" };

        private readonly IHttpContextAccessor _http;
        private ClaimsPrincipal? Principal => _http.HttpContext?.User;

        public CurrentUserService(IHttpContextAccessor http) => _http = http;

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

        public Guid? UserId
        {
            get
            {
                var s = GetFirstClaimValue(IdClaimTypes);
                return Guid.TryParse(s, out var id) ? id : null;
            }
        }

        public string? UserName =>
            GetClaim(ClaimTypes.Name) ?? GetClaim("name") ?? GetClaim("preferred_username");

        public string? Email =>
            GetClaim(ClaimTypes.Email) ?? GetClaim("email");

        public IReadOnlyList<string> Roles =>
            Principal is null
                ? Array.Empty<string>()
                : RoleClaimTypes
                    .SelectMany(t => Principal.FindAll(t).Select(c => c.Value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

        public IEnumerable<Claim> Claims => Principal?.Claims ?? Enumerable.Empty<Claim>();

        public Guid GetUserIdOrThrow() =>
            UserId ?? throw new InvalidOperationException("Current user is not authenticated or has no valid ID.");

        public bool IsInRole(string role) =>
            Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

        public string? GetClaim(string type) =>
            Principal?.FindFirst(type)?.Value;

        private string? GetFirstClaimValue(IEnumerable<string> types)
        {
            if (Principal is null) return null;
            foreach (var t in types)
            {
                var v = Principal.FindFirst(t)?.Value;
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
            return null;
        }
    }
}
