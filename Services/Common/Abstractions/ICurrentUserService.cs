using System.Security.Claims;

namespace Services.Common.Abstractions
{
    public interface ICurrentUserService
    {
        bool IsAuthenticated { get; }
        Guid? UserId { get; }
        string? UserName { get; }
        string? Email { get; }
        IReadOnlyList<string> Roles { get; }
        IEnumerable<Claim> Claims { get; }

        Guid GetUserIdOrThrow();                 // ném InvalidOperationException nếu chưa đăng nhập
        bool IsInRole(string role);              // tiện check role
        string? GetClaim(string type);           // tiện tra cứu claim tuỳ ý
    }
}
