namespace BusinessObjects.Common
{
    public enum Gender { Female, Male, Custom }
    public enum FriendStatus { Pending, Accepted, Declined }
    public enum CommunityRole { Owner, Mod, Member }
    public enum EventMode { Online, Offline }
    public enum EventStatus { Draft, Open, Closed, Completed, Canceled }
    public enum EventRegistrationStatus { Pending, Confirmed, CheckedIn, Canceled, Refunded }
    public enum EscrowStatus { Held, Released, Applied }
    public enum GatewayFeePolicy { OrganizerPays, AttendeePays }
    public enum TransactionDirection { In, Out }     // In: tiền vào ví, Out: tiền ra ví
    public enum TransactionMethod { Wallet, Card, Gateway }
    public enum TransactionStatus { Pending, Succeeded, Failed, Refunded, Disputed }
    public enum PaymentPurpose { TopUp, EventTicket, WalletTopUp }
    public enum PaymentIntentStatus { RequiresPayment, Processing, Succeeded, Canceled }
    public enum BugStatus { Open, InProgress, Resolved, Rejected }
    public enum RoomJoinPolicy
    {
        Open = 0,            // ai cũng vào được
        RequiresApproval = 1,// cần duyệt
        RequiresPassword = 2 // cần mật khẩu
    }

    public enum RoomMemberStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2,
        Banned = 3
    }

    public enum RoomRole
    {
        Member = 0,
        Moderator = 1,
        Owner = 2
    }
    public enum GameSkillLevel
    {
        Casual, Intermediate, Competitive
    }
}
