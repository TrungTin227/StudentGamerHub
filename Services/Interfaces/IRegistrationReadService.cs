namespace Services.Interfaces;

public interface IRegistrationReadService
{
    Task<Result<PagedResponse<RegistrationListItemDto>>> ListForEventAsync(
        Guid organizerId,
        Guid eventId,
        IEnumerable<EventRegistrationStatus>? statuses,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<PagedResponse<MyRegistrationDto>>> ListMineAsync(
        Guid userId,
        IEnumerable<EventRegistrationStatus>? statuses,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
