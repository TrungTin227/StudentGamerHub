namespace DTOs.Auth.Responses
{
    public sealed record AccessTokenResponse(string AccessToken, DateTime AccessExpiresAtUtc);
}
