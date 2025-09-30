// Services/Auth/JwtSettings.cs
namespace Services.Common.Auth
{
    public sealed class JwtSettings
    {
        public const string SectionName = "JwtSettings";
        public string ValidIssuer { get; init; } = default!;
        public string ValidAudience { get; init; } = default!;
        public string Key { get; init; } = default!;    
        public int Expires { get; init; } = 120;          
        public int? RefreshTokenDays { get; init; }      // nếu dùng refresh token sau này
    }
}
