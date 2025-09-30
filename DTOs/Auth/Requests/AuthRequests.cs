using System.ComponentModel.DataAnnotations;

namespace DTOs.Auth.Requests
{
    public sealed record LoginRequest
    {
        [Required, MaxLength(256)] public string UserNameOrEmail { get; init; } = default!;
        [Required, MinLength(6)] public string Password { get; init; } = default!;
        public string? TwoFactorCode { get; init; }
        public string? TwoFactorRecoveryCode { get; init; }
    }

    public sealed record RefreshTokenRequest
    {
        [Required] public string RefreshToken { get; init; } = default!;
    }

    public sealed record RevokeTokenRequest
    {
        [Required] public string RefreshToken { get; init; } = default!;
    }
}