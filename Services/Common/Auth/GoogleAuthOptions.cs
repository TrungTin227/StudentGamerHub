namespace Services.Common.Auth
{
    public sealed class GoogleAuthOptions
    {
        public const string SectionName = "GoogleAuth";
        public string ClientId { get; init; } = string.Empty;
        public string? AllowedHostedDomain { get; init; } // optional
    }
}
