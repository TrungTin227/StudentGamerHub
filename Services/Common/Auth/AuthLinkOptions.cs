namespace Services.Common.Auth
{
    public sealed class AuthLinkOptions
    {
        public string PublicBaseUrl { get; init; } = "";
        public string ConfirmEmailPath { get; init; } = "/auth/confirm-email";
        public string ResetPasswordPath { get; init; } = "/auth/reset-password";
        public string ChangeEmailPath { get; init; } = "/auth/confirm-change-email";
    }
}
