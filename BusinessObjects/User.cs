using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BusinessObjects;

// USER (Identity)
public sealed class User : IdentityUser<Guid>, IAuditable, ISoftDelete
{
    // Profile
    public string? FullName { get; set; }
    public Gender? Gender { get; set; }
    public string? University { get; set; }
    public int Level { get; set; } = 1;
    public int Points { get; set; }

    // Media
    public string? AvatarUrl { get; set; }
    public string? CoverUrl { get; set; }

    // Audit/Soft-delete
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }

    // Navs
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new HashSet<RefreshToken>();
    public ICollection<UserGame> UserGames { get; set; } = new HashSet<UserGame>();

    public Wallet? Wallet { get; set; }
}


// FRIENDS: Sender/Recipient + unique vô hướng min/max

public sealed class FriendLink : AuditableEntity
{
    // Hướng nghiệp vụ
    public Guid SenderId { get; set; }        // người gửi
    public Guid RecipientId { get; set; }     // người nhận

    // Trạng thái
    public FriendStatus Status { get; set; } = FriendStatus.Pending;

    // Thời điểm phản hồi (ai bấm thì bạn không lưu nữa)
    public DateTime? RespondedAt { get; set; }

    // Cột tính toán (unordered pair) để unique chống trùng A<->B
    public Guid PairMinUserId { get; private set; }  // computed (stored)
    public Guid PairMaxUserId { get; private set; }  // computed (stored)

    // Navs
    public User? Sender { get; set; }
    public User? Recipient { get; set; }
}


// CATALOGS
public sealed class Game : AuditableEntity
{
    public string Name { get; set; } = default!;
    public ICollection<UserGame> Users { get; set; } = new HashSet<UserGame>();

}
[PrimaryKey(nameof(UserId), nameof(GameId))]
public sealed class UserGame : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid GameId { get; set; }
    public Game? Game { get; set; }

    // Thông tin tuỳ chọn
    public string? InGameName { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public GameSkillLevel? Skill { get; set; }  // nếu muốn lưu “trình độ”
}
// COMMUNITIES (3 tables)
public sealed class Community : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? School { get; set; }
    public bool IsPublic { get; set; } = true;
    public int MembersCount { get; set; } = 0;

    public List<CommunityGame> Games { get; set; } = new();

    public List<Club> Clubs { get; set; } = new();
}
public sealed class Club : AuditableEntity
{
    public Guid CommunityId { get; set; }
    public Community? Community { get; set; }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsPublic { get; set; } = true;

    // Chỉ tổng hợp từ Rooms (tuỳ chọn cache)
    public int MembersCount { get; set; } = 0;

    public List<Room> Rooms { get; set; } = new();
}
public sealed class Room : AuditableEntity
{
    public Guid ClubId { get; set; }
    public Club? Club { get; set; }

    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    // Chính sách vào phòng
    public RoomJoinPolicy JoinPolicy { get; set; } = RoomJoinPolicy.Open;

    // Nếu RequiresPassword: lưu hash (KHÔNG lưu plaintext)
    public string? JoinPasswordHash { get; set; }

    // Tuỳ chọn: giới hạn số lượng
    public int? Capacity { get; set; }

    // Cache cho nhanh (có thể bỏ)
    public int MembersCount { get; set; } = 0;

    public List<RoomMember> Members { get; set; } = new();
}
[PrimaryKey(nameof(RoomId), nameof(UserId))]
public sealed class RoomMember : AuditableEntity
{
    public Guid RoomId { get; set; }
    public Room? Room { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public RoomRole Role { get; set; } = RoomRole.Member;
    public RoomMemberStatus Status { get; set; } = RoomMemberStatus.Approved; // mặc định; sẽ set Pending nếu phòng cần duyệt
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

[PrimaryKey(nameof(CommunityId), nameof(GameId))]
public sealed class CommunityGame 
{
    public Guid CommunityId { get; set; }
    public Community? Community { get; set; }
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
}

// EVENTS (3 tables)

public sealed class Event : AuditableEntity
{
    public Guid? CommunityId { get; set; }
    public Community? Community { get; set; }

    public Guid OrganizerId { get; set; }
    public User? Organizer { get; set; }

    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public EventMode Mode { get; set; } = EventMode.Online;
    public string? Location { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    public long PriceCents { get; set; } = 0;
    public int? Capacity { get; set; }

    public long EscrowMinCents { get; set; } = 0;
    public decimal PlatformFeeRate { get; set; } = 0.07m;
    public GatewayFeePolicy GatewayFeePolicy { get; set; } = GatewayFeePolicy.OrganizerPays;

    public EventStatus Status { get; set; } = EventStatus.Draft;

    public Escrow? Escrow { get; set; }
    public List<EventRegistration> Registrations { get; set; } = new();
}

public sealed class EventRegistration : AuditableEntity
{
    public Guid EventId { get; set; }
    public Event? Event { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public EventRegistrationStatus Status { get; set; } = EventRegistrationStatus.Pending;
    public Guid? PaidTransactionId { get; set; }
    public Transaction? PaidTransaction { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? CheckInAt { get; set; }
    public PaymentIntent? PaymentIntent { get; set; }

}

public sealed class Escrow : AuditableEntity
{
    public Guid EventId { get; set; }
    public Event? Event { get; set; }
    public long AmountHoldCents { get; set; }
    public EscrowStatus Status { get; set; } = EscrowStatus.Held;
}

// WALLET / TRANSACTIONS / PAYMENT INTENTS (3 tables)


public sealed class Wallet : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public long BalanceCents { get; set; } = 0;
    public List<Transaction> Transactions { get; set; } = new();
}

[Index(nameof(EventId))]
public sealed class Transaction : AuditableEntity
{
    public Guid? WalletId { get; set; }
    public Wallet? Wallet { get; set; }
    public Guid? EventId { get; set; }
    public Event? Event { get; set; }

    public long AmountCents { get; set; }
    public string Currency { get; set; } = "VND";

    public TransactionDirection Direction { get; set; } = TransactionDirection.Out;
    public TransactionMethod Method { get; set; } = TransactionMethod.Wallet;
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public string? Provider { get; set; }
    public string? ProviderRef { get; set; }
    public JsonDocument? Metadata { get; set; }
}



// PAYMENT INTENT
public sealed class PaymentIntent : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public long AmountCents { get; set; }
    public PaymentPurpose Purpose { get; set; } // vd: TopUp = 0, EventTicket = 1
    
    public Guid? EventRegistrationId { get; set; }
    public EventRegistration? EventRegistration { get; set; }

    public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.RequiresPayment;
    public string ClientSecret { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
}
// GIFTS (đổi quà bằng Points)
public sealed class Gift : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public int CostPoints { get; set; }
    public int? StockQty { get; set; }
    public bool IsActive { get; set; } = true;
}

// BUG REPORTS

public sealed class BugReport : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Category { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string? ImageUrl { get; set; }
    public BugStatus Status { get; set; } = BugStatus.Open;
}