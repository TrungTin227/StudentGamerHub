namespace Services.Common.Auth;
public interface ITokenService
{
    Task<TokenPairDto> IssueAsync(User user, string? ip = null, string? userAgent = null, CancellationToken ct = default);
    Task<TokenPairDto> RefreshAsync(string refreshTokenRaw, string? ip = null, string? userAgent = null, CancellationToken ct = default);
    Task RevokeAsync(string refreshTokenRaw, string? ip = null, string? reason = null, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userId, string? ip = null, string? reason = null, CancellationToken ct = default);

}
