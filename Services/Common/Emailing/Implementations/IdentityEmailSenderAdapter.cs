using Microsoft.AspNetCore.Identity;

namespace Services.Common.Emailing.Implementations
{
    public sealed class IdentityEmailSenderAdapter<TUser>(IEmailQueue queue, EmailOptions options) : IEmailSender<TUser>
        where TUser : class
    {
        public Task SendConfirmationLinkAsync(TUser user, string email, string confirmationLink) =>
            queue.EnqueueAsync(new EmailMessage
            {
                To = { new EmailAddress(email) },
                Subject = "Confirm your email",
                HtmlBody = $"Click <a href=\"{confirmationLink}\">here</a> to confirm your email.",
                TextBody = $"Open this link to confirm your email: {confirmationLink}"
            }).AsTask();

        public Task SendPasswordResetLinkAsync(TUser user, string email, string resetLink) =>
            queue.EnqueueAsync(new EmailMessage
            {
                To = { new EmailAddress(email) },
                Subject = "Reset your password",
                HtmlBody = $"Click <a href=\"{resetLink}\">here</a> to reset your password.",
                TextBody = $"Open this link to reset your password: {resetLink}"
            }).AsTask();

        public Task SendPasswordResetCodeAsync(TUser user, string email, string resetCode) =>
            queue.EnqueueAsync(new EmailMessage
            {
                To = { new EmailAddress(email) },
                Subject = "Password reset code",
                HtmlBody = $"Your reset code is: <strong>{resetCode}</strong>",
                TextBody = $"Your reset code is: {resetCode}"
            }).AsTask();
    }
}
