namespace DTOs.Auth
{
    // Refresh không trả trong body; set bằng cookie HttpOnly
    public sealed record TokenPairDto(
        string AccessToken, DateTime AccessExpiresAtUtc,
        string RefreshToken, DateTime RefreshExpiresAtUtc);
}
