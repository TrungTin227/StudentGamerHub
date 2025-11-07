namespace Services.Common.Emailing.Interfaces
{
    public interface IMembershipEmailFactory
    {
        EmailMessage BuildMembershipPurchaseConfirmation(User user, MembershipPlan plan, UserMembership membership);
    }
}
