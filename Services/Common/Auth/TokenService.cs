using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Services.Common.Auth
{
    public sealed class TokenService : ITokenService
    {
        private readonly JwtSettings _opt;
        private readonly UserManager<User> _users;
        private readonly AppDbContext _db;

        // Base64Url(64 bytes) ≈ 86 ký tự, dùng ngưỡng 80 để phát hiện token sai định dạng
        private const int MinRefreshTokenLength = 80;

        public TokenService(IOptions<JwtSettings> opt, UserManager<User> users, AppDbContext db)
        {
            _opt = opt.Value;
            _users = users;
            _db = db;

            if (string.IsNullOrWhiteSpace(_opt.Key))
                throw new InvalidOperationException("JwtSettings:Key is required.");
        }

        public async Task<TokenPairDto> IssueAsync(User user, string? ip = null, string? ua = null, CancellationToken ct = default)
        {
            var (access, accessExp) = await CreateAccessAsync(user, ct).ConfigureAwait(false);

            var refreshDays = _opt.RefreshTokenDays ?? 7; // mặc định 7 ngày nếu không cấu hình
            var (raw, hash, refreshExp) = NewRefreshToken(refreshDays);

            _db.Set<RefreshToken>().Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = hash,                    // chỉ lưu HASH
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = refreshExp,
                CreatedByIp = ip,
                UserAgent = ua
            });
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            return new(access, accessExp, raw, refreshExp);
        }

        public async Task<TokenPairDto> RefreshAsync(string refreshTokenRaw, string? ip = null, string? ua = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(refreshTokenRaw) || refreshTokenRaw.Length < MinRefreshTokenLength)
                throw new UnauthorizedAccessException("Malformed refresh token");

            // băm raw -> hex
            var presentedHashHex = Hash(refreshTokenRaw);

            // Tìm token theo hash (so sánh DB). Bước này đã đủ dùng.
            var rt = await _db.Set<RefreshToken>()
                              .FirstOrDefaultAsync(x => x.TokenHash == presentedHashHex, ct)
                              .ConfigureAwait(false)
                     ?? throw new UnauthorizedAccessException("Invalid refresh token");

            // (Tuỳ chọn) xác nhận lại constant-time ở tầng ứng dụng
            if (!FixedTimeEqualsHex(rt.TokenHash, presentedHashHex))
                throw new UnauthorizedAccessException("Invalid refresh token");

            // Reuse-detection: nếu token đã bị revoke mà vẫn được sử dụng lại → compromise
            if (rt.RevokedAtUtc is not null)
            {
                await RevokeDescendantsChain(rt, ip, "Detected reuse", ct).ConfigureAwait(false);
                throw new UnauthorizedAccessException("Refresh token reused");
            }

            // Hết hạn thì từ chối (không cần chain-revoke)
            if (DateTime.UtcNow >= rt.ExpiresAtUtc)
                throw new UnauthorizedAccessException("Refresh token expired");

            var user = await _users.FindByIdAsync(rt.UserId.ToString()).ConfigureAwait(false)
                       ?? throw new UnauthorizedAccessException("User not found");

            // Rotation: revoke token cũ, tạo token mới, xâu chuỗi
            rt.RevokedAtUtc = DateTime.UtcNow;
            rt.RevokedByIp = ip;
            rt.ReasonRevoked = "rotated";

            var refreshDays = _opt.RefreshTokenDays ?? 7;
            var (rawNew, newHash, refreshExpNew) = NewRefreshToken(refreshDays);

            var newRt = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = newHash,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = refreshExpNew,
                CreatedByIp = ip,
                UserAgent = ua
            };
            rt.ReplacedByTokenId = newRt.Id;
            _db.Add(newRt);

            var (access, accessExp) = await CreateAccessAsync(user, ct).ConfigureAwait(false);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            return new(access, accessExp, rawNew, refreshExpNew);
        }

        public async Task RevokeAsync(string refreshTokenRaw, string? ip = null, string? reason = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(refreshTokenRaw) || refreshTokenRaw.Length < MinRefreshTokenLength)
                return;

            var hash = Hash(refreshTokenRaw);
            var rt = await _db.Set<RefreshToken>().FirstOrDefaultAsync(x => x.TokenHash == hash, ct).ConfigureAwait(false);
            if (rt is null) return;

            if (rt.RevokedAtUtc is null)
            {
                rt.RevokedAtUtc = DateTime.UtcNow;
                rt.RevokedByIp = ip;
                rt.ReasonRevoked = reason ?? "revoked";

                // 🔐 Rotate SecurityStamp → vô hiệu hóa ngay mọi access token còn sống
                var user = await _users.FindByIdAsync(rt.UserId.ToString()).ConfigureAwait(false);
                if (user is not null)
                    await _users.UpdateSecurityStampAsync(user).ConfigureAwait(false);

                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task RevokeAllForUserAsync(Guid userId, string? ip = null, string? reason = null, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var tokens = await _db.Set<RefreshToken>()
                .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
                .ToListAsync(ct).ConfigureAwait(false);

            foreach (var t in tokens)
            {
                t.RevokedAtUtc = now;
                t.RevokedByIp = ip;
                t.ReasonRevoked = reason ?? "bulk revoke";
            }

            // (Khuyến nghị) đổi SecurityStamp để chặn access token còn sống ngay lập tức
            var user = await _users.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
            if (user is not null)
                await _users.UpdateSecurityStampAsync(user).ConfigureAwait(false);

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        // ===== helpers =====

        private async Task<(string token, DateTime expUtc)> CreateAccessAsync(User user, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var roles = await _users.GetRolesAsync(user).ConfigureAwait(false);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Iat, ((long)(now - DateTime.UnixEpoch).TotalSeconds).ToString(), ClaimValueTypes.Integer64),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
            };

            var sst = await _users.GetSecurityStampAsync(user).ConfigureAwait(false);
            claims.Add(new Claim("sst", sst));

            if (!string.IsNullOrWhiteSpace(user.Email))
                claims.Add(new Claim(ClaimTypes.Email, user.Email));

            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
            var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var exp = now.AddMinutes(_opt.Expires);

            var jwt = new JwtSecurityToken(
                issuer: _opt.ValidIssuer,
                audience: _opt.ValidAudience,
                claims: claims,
                notBefore: now,
                expires: exp,
                signingCredentials: cred);

            return (new JwtSecurityTokenHandler().WriteToken(jwt), exp);
        }

        private static (string raw, string hash, DateTime expUtc) NewRefreshToken(int days)
        {
            var rawBytes = RandomNumberGenerator.GetBytes(64);
            var raw = WebEncoders.Base64UrlEncode(rawBytes);
            var hash = Hash(raw); // lưu HEX (chuẩn hoá)
            return (raw, hash, DateTime.UtcNow.AddDays(days));
        }

        private static string Hash(string raw)
        {
            // (Tuỳ chọn) thay bằng HMACSHA256 với "pepper" bí mật đọc từ cấu hình.
            // using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_opt.RefreshPepper));
            // var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
            // return Convert.ToHexString(bytes);

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes);
        }

        private static bool FixedTimeEqualsHex(string hexA, string hexB)
        {
            try
            {
                var a = Convert.FromHexString(hexA);
                var b = Convert.FromHexString(hexB);
                if (a.Length != b.Length) return false;
                return CryptographicOperations.FixedTimeEquals(a, b);
            }
            catch
            {
                return false;
            }
        }

        private async Task RevokeDescendantsChain(RefreshToken start, string? ip, string? reason, CancellationToken ct)
        {
            var cursor = start;
            while (cursor.ReplacedByTokenId is Guid nextId)
            {
                var next = await _db.Set<RefreshToken>().FirstOrDefaultAsync(x => x.Id == nextId, ct).ConfigureAwait(false);
                if (next is null) break;

                if (next.RevokedAtUtc is null)
                {
                    next.RevokedAtUtc = DateTime.UtcNow;
                    next.RevokedByIp = ip;
                    next.ReasonRevoked = reason ?? "revoked due to ancestor reuse";
                }

                cursor = next;
            }
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
