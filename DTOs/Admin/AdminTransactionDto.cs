using BusinessObjects;

namespace DTOs.Admin;

/// <summary>
/// Chi tiết giao dịch cho Admin
/// </summary>
public record AdminTransactionDto
{
    public Guid Id { get; init; }
    public Guid WalletId { get; init; }
    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
    public string? UserEmail { get; init; }

    public long AmountCents { get; init; }
    public string Currency { get; init; } = "VND";

    public TransactionDirection Direction { get; init; }
    public TransactionMethod Method { get; init; }
    public TransactionStatus Status { get; init; }

    /// <summary>
    /// Event liên quan (nếu có)
    /// </summary>
    public Guid? EventId { get; init; }
    public string? EventTitle { get; init; }

    /// <summary>
    /// Payment provider (PayOS, Manual, etc.)
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// Provider reference ID
    /// </summary>
    public string? ProviderRef { get; init; }

    /// <summary>
    /// Metadata bổ sung
    /// </summary>
    public string? Metadata { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
}
