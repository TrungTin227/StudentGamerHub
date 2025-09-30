namespace Services.Interfaces
{
    public interface IGoogleAuthService
    {
        Task<Result<TokenPairDto>> LoginAsync(GoogleLoginRequest req, string? ip = null, string? userAgent = null, CancellationToken ct = default);

    }
}
