namespace Services.Common.Mapping;

public static class EventMappers
{
    public static EventDetailDto ToDetailDto(
        this Event ev,
        Escrow? escrow,
        bool isOrganizer,
        EventRegistration? myRegistration,
        int registeredCount,
        int confirmedCount)
    {
        ArgumentNullException.ThrowIfNull(ev);

        var escrowAmount = escrow?.AmountHoldCents ?? 0;
        var escrowStatus = escrow?.Status ?? EscrowStatus.Held;
        var displayStatus = DetermineEventDisplayStatus(ev, DateTime.UtcNow);

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
            DisplayStatus: displayStatus,
            EscrowAmountHoldCents: escrowAmount,
            EscrowStatus: escrowStatus,
            IsOrganizer: isOrganizer,
            MyRegistrationId: myRegistration?.Id,
            MyRegistrationStatus: myRegistration?.Status,
            RegisteredCount: registeredCount,
            ConfirmedCount: confirmedCount,
            CreatedAtUtc: ev.CreatedAtUtc,
            UpdatedAtUtc: ev.UpdatedAtUtc);
    }

    /// <summary>
    /// Determine event display status based on current time and event status.
    /// Returns: "Upcoming", "Opened", or "Closed"
    /// </summary>
    private static string DetermineEventDisplayStatus(Event ev, DateTime nowUtc)
    {
        if (ev.Status == EventStatus.Canceled || ev.Status == EventStatus.Completed)
            return "Closed";

        if (ev.Status == EventStatus.Draft)
            return "Upcoming";

        if (nowUtc < ev.StartsAt)
            return "Upcoming";

        if (ev.Status == EventStatus.Open)
        {
            if (ev.EndsAt.HasValue && nowUtc >= ev.EndsAt.Value)
                return "Closed";
            return "Opened";
        }

        return "Closed";
    }
}
