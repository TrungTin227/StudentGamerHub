namespace DTOs.Auth
{
    public sealed record AccessTokenDto(string AccessToken, DateTime AccessExpiresAtUtc);

}
