namespace Services.Common.Emailing.Interfaces
{
    public interface IAuthEmailFactory
    {
        EmailMessage BuildConfirmEmail(User user, string url);
        EmailMessage BuildResetPassword(User user, string url);
        EmailMessage BuildChangeEmail(string newEmail, string url, string? displayName);
        EmailMessage BuildPasswordChanged(User user);
        EmailMessage BuildPasswordResetSucceeded(User user);
    }
}
