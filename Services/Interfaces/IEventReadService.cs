namespace Services.Interfaces;

public interface IEventReadService
{
    Task<Result<EventDetailDto>> GetByIdAsync(Guid currentUserId, Guid eventId, CancellationToken ct = default);
    Task<Result<PagedResponse<EventDetailDto>>> SearchAsync(
        Guid currentUserId,
        IEnumerable<EventStatus>? statuses,
        Guid? communityId,
        Guid? organizerId,
        DateTime? from,
        DateTime? to,
        string? search,
        bool sortAscByStartsAt,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<Result<PagedResponse<EventDetailDto>>> SearchMyOrganizedAsync(
        Guid organizerId,
        IEnumerable<EventStatus>? statuses,
        Guid? communityId,
        DateTime? from,
        DateTime? to,
        string? search,
        bool sortAscByStartsAt,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
