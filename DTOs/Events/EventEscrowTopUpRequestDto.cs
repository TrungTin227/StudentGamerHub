using System.ComponentModel.DataAnnotations;

namespace DTOs.Events;

public sealed record EventEscrowTopUpRequestDto
{
    [Range(1, long.MaxValue, ErrorMessage = "Amount must be positive.")]
    public long AmountCents { get; init; }
}
