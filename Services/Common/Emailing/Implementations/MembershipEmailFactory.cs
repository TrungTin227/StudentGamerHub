using Microsoft.Extensions.Options;
using static System.Net.WebUtility;

namespace Services.Common.Emailing.Implementations
{
    public sealed class MembershipEmailFactory : IMembershipEmailFactory
    {
        private readonly EmailOptions _opts;

        public MembershipEmailFactory(IOptions<EmailOptions> opts)
        {
            _opts = opts?.Value ?? throw new ArgumentNullException(nameof(opts));
        }

        public EmailMessage BuildMembershipPurchaseConfirmation(User user, MembershipPlan plan, UserMembership membership)
        {
            var display = user.FullName ?? user.UserName ?? user.Email!;
            var planName = HtmlEncode(plan.Name);
            var displayName = HtmlEncode(display);

            // Format dates in Vietnamese style
            var startDate = membership.StartDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
            var endDate = membership.EndDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

            // Format price in VND
            var priceFormatted = plan.Price.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

            // Event quota display
            var eventQuotaText = plan.MonthlyEventLimit == -1
                ? "Không giới hạn"
                : $"{plan.MonthlyEventLimit} sự kiện/tháng";

            var subject = $"🎉 Gói hội viên {plan.Name} của bạn đã được kích hoạt!";

            var textBody = $@"Xin chào {display},

Chúc mừng! Gói hội viên {plan.Name} của bạn đã được kích hoạt thành công.

Thông tin gói hội viên:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Tên gói: {plan.Name}
Thời hạn: {plan.DurationMonths} tháng
Ngày kích hoạt: {startDate}
Ngày hết hạn: {endDate}
Hạn mức sự kiện: {eventQuotaText}
Giá trị: {priceFormatted} VND
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Bạn có thể bắt đầu tận hưởng các đặc quyền của gói hội viên ngay bây giờ!

Cảm ơn bạn đã tin tưởng và đồng hành cùng Student Gamer Hub.

Trân trọng,
Đội ngũ Student Gamer Hub";

            var htmlBody = $@"
<html>
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f5f5f5;"">
    <div style=""background-color: #ffffff; border-radius: 12px; padding: 30px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);"">
        <!-- Header with emoji -->
        <div style=""text-align: center; margin-bottom: 30px;"">
            <h1 style=""color: #2563eb; margin: 0; font-size: 28px;"">🎉 Chúc mừng!</h1>
        </div>

        <!-- Greeting -->
        <p style=""font-size: 16px; margin-bottom: 20px;"">
            Xin chào <strong style=""color: #2563eb;"">{displayName}</strong>,
        </p>

        <!-- Main message -->
        <p style=""font-size: 16px; margin-bottom: 25px; line-height: 1.8;"">
            Gói hội viên <strong style=""color: #10b981;"">{planName}</strong> của bạn đã được <strong>kích hoạt thành công</strong>! 🚀
        </p>

        <!-- Membership details card -->
        <div style=""background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); border-radius: 10px; padding: 25px; margin: 25px 0; color: #ffffff;"">
            <h2 style=""margin: 0 0 20px 0; font-size: 20px; border-bottom: 2px solid rgba(255,255,255,0.3); padding-bottom: 12px;"">
                Thông tin gói hội viên
            </h2>
            <table style=""width: 100%; color: #ffffff; font-size: 15px;"">
                <tr>
                    <td style=""padding: 8px 0; font-weight: 500;"">📦 Tên gói:</td>
                    <td style=""padding: 8px 0; text-align: right; font-weight: 600;"">{planName}</td>
                </tr>
                <tr>
                    <td style=""padding: 8px 0; font-weight: 500;"">⏱️ Thời hạn:</td>
                    <td style=""padding: 8px 0; text-align: right; font-weight: 600;"">{plan.DurationMonths} tháng</td>
                </tr>
                <tr>
                    <td style=""padding: 8px 0; font-weight: 500;"">📅 Ngày kích hoạt:</td>
                    <td style=""padding: 8px 0; text-align: right; font-weight: 600;"">{startDate}</td>
                </tr>
                <tr>
                    <td style=""padding: 8px 0; font-weight: 500;"">🗓️ Ngày hết hạn:</td>
                    <td style=""padding: 8px 0; text-align: right; font-weight: 600;"">{endDate}</td>
                </tr>
                <tr>
                    <td style=""padding: 8px 0; font-weight: 500;"">🎯 Hạn mức sự kiện:</td>
                    <td style=""padding: 8px 0; text-align: right; font-weight: 600;"">{eventQuotaText}</td>
                </tr>
                <tr style=""border-top: 2px solid rgba(255,255,255,0.3);"">
                    <td style=""padding: 12px 0 0 0; font-weight: 600; font-size: 16px;"">💰 Giá trị:</td>
                    <td style=""padding: 12px 0 0 0; text-align: right; font-weight: 700; font-size: 18px; color: #fbbf24;"">{priceFormatted} VND</td>
                </tr>
            </table>
        </div>

        <!-- Benefits section -->
        <div style=""background-color: #f0fdf4; border-left: 4px solid #10b981; padding: 20px; margin: 25px 0; border-radius: 8px;"">
            <p style=""margin: 0; font-size: 15px; color: #065f46;"">
                ✨ <strong>Bạn có thể bắt đầu tận hưởng các đặc quyền của gói hội viên ngay bây giờ!</strong>
            </p>
        </div>

        <!-- Call to action (optional) -->
        <div style=""text-align: center; margin: 30px 0;"">
            <p style=""font-size: 14px; color: #6b7280; margin-bottom: 15px;"">
                Khám phá thêm các tính năng dành riêng cho hội viên
            </p>
        </div>

        <!-- Footer message -->
        <p style=""font-size: 15px; margin-top: 30px; color: #374151; border-top: 1px solid #e5e7eb; padding-top: 20px;"">
            Cảm ơn bạn đã tin tưởng và đồng hành cùng <strong>Student Gamer Hub</strong>! 🎮
        </p>

        <!-- Signature -->
        <div style=""margin-top: 25px; padding-top: 20px; border-top: 1px solid #e5e7eb;"">
            <p style=""margin: 0; font-size: 14px; color: #6b7280;"">
                Trân trọng,<br/>
                <strong style=""color: #2563eb;"">Đội ngũ Student Gamer Hub</strong>
            </p>
        </div>
    </div>

    <!-- Footer disclaimer -->
    <div style=""text-align: center; margin-top: 20px; padding: 15px; color: #9ca3af; font-size: 12px;"">
        <p style=""margin: 0;"">
            Email này được gửi tự động. Vui lòng không trả lời email này.
        </p>
    </div>
</body>
</html>";

            return new EmailMessage
            {
                To = { new EmailAddress(user.Email!, display) },
                From = new EmailAddress(_opts.DefaultFrom, _opts.DefaultFromName),
                Subject = subject,
                TextBody = textBody,
                HtmlBody = htmlBody
            };
        }
    }
}
