namespace Services.Implementations;

/// <summary>
/// Read-only queries for events.
/// </summary>
public sealed class EventReadService : IEventReadService
{
    private readonly IEventQueryRepository _eventQueryRepository;
    private readonly IEscrowRepository _escrowRepository;
    private readonly IRegistrationQueryRepository _registrationQueryRepository;

    public EventReadService(
        IEventQueryRepository eventQueryRepository,
        IEscrowRepository escrowRepository,
        IRegistrationQueryRepository registrationQueryRepository)
    {
        _eventQueryRepository = eventQueryRepository ?? throw new ArgumentNullException(nameof(eventQueryRepository));
        _escrowRepository = escrowRepository ?? throw new ArgumentNullException(nameof(escrowRepository));
        _registrationQueryRepository = registrationQueryRepository ?? throw new ArgumentNullException(nameof(registrationQueryRepository));
    }

    public async Task<Result<EventDetailDto>> GetByIdAsync(Guid currentUserId, Guid eventId, CancellationToken ct = default)
    {
        var ev = await _eventQueryRepository.GetByIdAsync(eventId, ct).ConfigureAwait(false);
        if (ev is null)
        {
            return Result<EventDetailDto>.Failure(new Error(Error.Codes.NotFound, "Event not found."));
        }

        var escrow = await _escrowRepository.GetByEventIdAsync(eventId, ct).ConfigureAwait(false);
        EventRegistration? myReg = null;
        if (currentUserId != Guid.Empty)
        {
            myReg = await _registrationQueryRepository.GetByEventAndUserAsync(eventId, currentUserId, ct).ConfigureAwait(false);
        }

        var dto = ev.ToDetailDto(escrow, ev.OrganizerId == currentUserId, myReg);
        return Result<EventDetailDto>.Success(dto);
    }

    public async Task<Result<PagedResponse<EventDetailDto>>> SearchAsync(
        Guid currentUserId,
        IEnumerable<EventStatus>? statuses,
        Guid? communityId,
        Guid? organizerId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? search,
        bool sortAscByStartsAt,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (items, total) = await _eventQueryRepository.SearchAsync(
            statuses,
            communityId,
            organizerId,
            from,
            to,
            search,
            page,
            pageSize,
            sortAscByStartsAt,
            ct).ConfigureAwait(false);

        var dtos = await MapEventsAsync(items, currentUserId, ct).ConfigureAwait(false);
        var response = BuildPagedResponse(dtos, total, page, pageSize, sortAscByStartsAt);
        return Result<PagedResponse<EventDetailDto>>.Success(response);
    }

    public async Task<Result<PagedResponse<EventDetailDto>>> SearchMyOrganizedAsync(
        Guid organizerId,
        IEnumerable<EventStatus>? statuses,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? search,
        bool sortAscByStartsAt,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (items, total) = await _eventQueryRepository.SearchAsync(
            statuses,
            communityId: null,
            organizerId: organizerId,
            from,
            to,
            search,
            page,
            pageSize,
            sortAscByStartsAt,
            ct).ConfigureAwait(false);

        var dtos = await MapEventsAsync(items, organizerId, ct).ConfigureAwait(false);
        var response = BuildPagedResponse(dtos, total, page, pageSize, sortAscByStartsAt);
        return Result<PagedResponse<EventDetailDto>>.Success(response);
    }

    private async Task<IReadOnlyList<EventDetailDto>> MapEventsAsync(IReadOnlyList<Event> events, Guid currentUserId, CancellationToken ct)
    {
        var results = new List<EventDetailDto>(events.Count);
        foreach (var ev in events)
        {
            var escrow = await _escrowRepository.GetByEventIdAsync(ev.Id, ct).ConfigureAwait(false);
            EventRegistration? myReg = null;
            if (currentUserId != Guid.Empty)
            {
                myReg = await _registrationQueryRepository.GetByEventAndUserAsync(ev.Id, currentUserId, ct).ConfigureAwait(false);
            }

            results.Add(ev.ToDetailDto(escrow, ev.OrganizerId == currentUserId, myReg));
        }

        return results;
    }

    private static PagedResponse<EventDetailDto> BuildPagedResponse(
        IReadOnlyList<EventDetailDto> items,
        int total,
        int page,
        int pageSize,
        bool sortAscByStartsAt)
    {
        var normalizedPageSize = pageSize <= 0 ? PaginationOptions.DefaultPageSize : Math.Clamp(pageSize, 1, PaginationOptions.MaxPageSize);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)normalizedPageSize);
        var normalizedPage = totalPages == 0 ? 1 : Math.Clamp(page <= 0 ? 1 : page, 1, totalPages);
        var hasPrevious = totalPages > 0 && normalizedPage > 1;
        var hasNext = totalPages > 0 && normalizedPage < totalPages;

        return new PagedResponse<EventDetailDto>(
            items,
            normalizedPage,
            normalizedPageSize,
            total,
            totalPages,
            hasPrevious,
            hasNext,
            "StartsAt",
            !sortAscByStartsAt);
    }
}
