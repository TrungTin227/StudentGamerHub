namespace DTOs.Auth.Responses;

public sealed record TokenResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string TokenType = "Bearer"
);


