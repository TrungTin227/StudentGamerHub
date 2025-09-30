using static System.Net.WebUtility;
namespace Services.Common.Emailing.Implementations
{
    public sealed class AuthEmailFactory : IAuthEmailFactory
    {
        public EmailMessage BuildConfirmEmail(User user, string url)
        {
            var display = user.FullName ?? user.UserName ?? user.Email!;
            var urlEncoded = HtmlEncode(url);

            return new EmailMessage
            {
                To = { new EmailAddress(user.Email!, display) },
                Subject = "Xác nhận địa chỉ email",
                TextBody = $"Xin chào {display},\nHãy mở liên kết sau để xác nhận email: {url}\nNếu bạn không yêu cầu, hãy bỏ qua.",
                HtmlBody = $"""
<p>Xin chào <strong>{HtmlEncode(display)}</strong>,</p>
<p>Vui lòng nhấn vào nút bên dưới để xác nhận email.</p>
<p><a href="{urlEncoded}" target="_blank" style="display:inline-block;background:#2563eb;color:#fff;padding:10px 16px;border-radius:8px;font-weight:600;text-decoration:none">Xác nhận email</a></p>
<p>Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>
"""
            };
        }

        public EmailMessage BuildResetPassword(User user, string url)
        {
            var display = user.FullName ?? user.UserName ?? user.Email!;
            var urlEncoded = HtmlEncode(url);

            return new EmailMessage
            {
                To = { new EmailAddress(user.Email!, display) },
                Subject = "Đặt lại mật khẩu",
                TextBody = $"Xin chào {display},\nLiên kết đặt lại mật khẩu: {url}\nNếu bạn không yêu cầu, xin bỏ qua.",
                HtmlBody = $"""
<p>Xin chào <strong>{HtmlEncode(display)}</strong>,</p>
<p>Nhấn vào nút bên dưới để đặt lại mật khẩu:</p>
<p><a href="{urlEncoded}" target="_blank" style="display:inline-block;background:#2563eb;color:#fff;padding:10px 16px;border-radius:8px;font-weight:600;text-decoration:none">Đặt lại mật khẩu</a></p>
<p>Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>
"""
            };
        }

        public EmailMessage BuildChangeEmail(string newEmail, string url, string? displayName)
        {
            var display = displayName ?? newEmail;
            var urlEncoded = HtmlEncode(url);

            return new EmailMessage
            {
                To = { new EmailAddress(newEmail, display) },
                Subject = "Xác nhận thay đổi email",
                TextBody = $"Bạn đã yêu cầu đổi email đăng nhập. Mở liên kết sau để xác nhận: {url}",
                HtmlBody = $"""
<p>Bạn đã yêu cầu thay đổi email đăng nhập.</p>
<p><a href="{urlEncoded}" target="_blank" style="display:inline-block;background:#2563eb;color:#fff;padding:10px 16px;border-radius:8px;font-weight:600;text-decoration:none">Xác nhận thay đổi</a></p>
<p>Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>
"""
            };
        }
        // NEW: Thông báo khi người dùng tự đổi mật khẩu (đang đăng nhập)
        public EmailMessage BuildPasswordChanged(User user)
        {
            var display = user.FullName ?? user.UserName ?? user.Email!;

            return new EmailMessage
            {
                To = { new EmailAddress(user.Email!, display) },
                Subject = "Mật khẩu của bạn đã được thay đổi",
                TextBody =
$@"Xin chào {display},
Mật khẩu cho tài khoản của bạn vừa được thay đổi thành công.

Nếu không phải bạn thực hiện, vui lòng:
1) Đặt lại mật khẩu ngay lập tức, và
2) Liên hệ hỗ trợ.

Cảm ơn bạn.",
                HtmlBody =
$"""
<p>Xin chào <strong>{HtmlEncode(display)}</strong>,</p>
<p>Mật khẩu cho tài khoản của bạn vừa được <strong>thay đổi</strong> thành công.</p>
<p>Nếu không phải bạn thực hiện, vui lòng <em>đặt lại mật khẩu ngay</em> và liên hệ bộ phận hỗ trợ.</p>
<p>Cảm ơn bạn.</p>
"""
            };
        }

        // NEW: Thông báo khi reset mật khẩu (qua email) đã hoàn tất
        public EmailMessage BuildPasswordResetSucceeded(User user)
        {
            var display = user.FullName ?? user.UserName ?? user.Email!;

            return new EmailMessage
            {
                To = { new EmailAddress(user.Email!, display) },
                Subject = "Đặt lại mật khẩu thành công",
                TextBody =
$@"Xin chào {display},
Bạn vừa đặt lại mật khẩu thành công.

Nếu không phải bạn thực hiện, vui lòng:
1) Đặt lại mật khẩu một lần nữa, và
2) Liên hệ hỗ trợ ngay.

Cảm ơn bạn.",
                HtmlBody =
$"""
<p>Xin chào <strong>{HtmlEncode(display)}</strong>,</p>
<p>Bạn vừa <strong>đặt lại mật khẩu</strong> thành công.</p>
<p>Nếu không phải bạn thực hiện, vui lòng <em>đặt lại mật khẩu</em> một lần nữa và liên hệ hỗ trợ ngay.</p>
<p>Cảm ơn bạn.</p>
"""
            };
        }
    
}
}
