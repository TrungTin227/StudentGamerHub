namespace Services.Interfaces
{
    public interface IAuthService
    {
        Task<Result<TokenPairDto>> LoginAsync(LoginRequest req, string? ip = null, string? userAgent = null, CancellationToken ct = default);
        Task<Result<TokenPairDto>> RefreshAsync(string refreshToken, string? ip = null, string? userAgent = null, CancellationToken ct = default);
        Task<Result> RevokeAsync(string refreshToken, string? ip = null, CancellationToken ct = default);
    }
}
