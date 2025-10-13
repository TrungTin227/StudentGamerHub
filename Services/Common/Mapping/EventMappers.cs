namespace Services.Common.Mapping;

public static class EventMappers
{
    public static EventDetailDto ToDetailDto(
        this Event ev,
        Escrow? escrow,
        bool isOrganizer,
        EventRegistration? myRegistration)
    {
        ArgumentNullException.ThrowIfNull(ev);

        var escrowAmount = escrow?.AmountHoldCents ?? 0;
        var escrowStatus = escrow?.Status ?? EscrowStatus.Held;

        return new EventDetailDto(
            Id: ev.Id,
            OrganizerId: ev.OrganizerId,
            CommunityId: ev.CommunityId,
            Title: ev.Title,
            Description: ev.Description,
            Mode: ev.Mode,
            Location: ev.Location,
            StartsAt: ev.StartsAt,
            EndsAt: ev.EndsAt,
            PriceCents: ev.PriceCents,
            Capacity: ev.Capacity,
            EscrowMinCents: ev.EscrowMinCents,
            PlatformFeeRate: ev.PlatformFeeRate,
            GatewayFeePolicy: ev.GatewayFeePolicy,
            Status: ev.Status,
            EscrowAmountHoldCents: escrowAmount,
            EscrowStatus: escrowStatus,
            IsOrganizer: isOrganizer,
            MyRegistrationId: myRegistration?.Id,
            MyRegistrationStatus: myRegistration?.Status,
            CreatedAtUtc: ev.CreatedAtUtc,
            UpdatedAtUtc: ev.UpdatedAtUtc);
    }
}
