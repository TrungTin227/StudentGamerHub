namespace DTOs.Auth.Validation
{
    public static class DtoNormalization
    {
        public static RegisterRequest Normalize(this RegisterRequest req)
            => new(
                FullName: req.FullName?.Trim() ?? string.Empty,
                Gender: req.Gender,
                University: req.University?.Trim(),
                Email: req.Email?.Trim().ToLowerInvariant() ?? string.Empty,
                PhoneNumber: req.PhoneNumber?.Trim(),
                Password: req.Password 
            );

        public static LoginRequest Normalize(this LoginRequest req)
        {
            var u = req.UserNameOrEmail?.Trim();
            if (!string.IsNullOrWhiteSpace(u) && u.Contains('@')) u = u.ToLowerInvariant();
            return new LoginRequest
            {
                UserNameOrEmail = u!,
                Password = req.Password,
                TwoFactorCode = req.TwoFactorCode?.Trim(),
                TwoFactorRecoveryCode = req.TwoFactorRecoveryCode?.Trim()
            };
        }

        public static RefreshTokenRequest Normalize(this RefreshTokenRequest req)
            => new() { RefreshToken = req.RefreshToken?.Trim()! };

        public static RevokeTokenRequest Normalize(this RevokeTokenRequest req)
            => new() { RefreshToken = req.RefreshToken?.Trim()! };
    }
}
