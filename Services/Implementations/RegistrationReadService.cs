using System.Linq;

namespace Services.Implementations;

/// <summary>
/// Read operations for event registrations.
/// </summary>
public sealed class RegistrationReadService : IRegistrationReadService
{
    private readonly IEventQueryRepository _eventQueryRepository;
    private readonly IRegistrationQueryRepository _registrationQueryRepository;

    public RegistrationReadService(
        IEventQueryRepository eventQueryRepository,
        IRegistrationQueryRepository registrationQueryRepository)
    {
        _eventQueryRepository = eventQueryRepository ?? throw new ArgumentNullException(nameof(eventQueryRepository));
        _registrationQueryRepository = registrationQueryRepository ?? throw new ArgumentNullException(nameof(registrationQueryRepository));
    }

    public async Task<Result<PagedResponse<RegistrationListItemDto>>> ListForEventAsync(
        Guid organizerId,
        Guid eventId,
        IEnumerable<EventRegistrationStatus>? statuses,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var ev = await _eventQueryRepository.GetByIdAsync(eventId, ct).ConfigureAwait(false);
        if (ev is null)
        {
            return Result<PagedResponse<RegistrationListItemDto>>.Failure(new Error(Error.Codes.NotFound, "Event not found."));
        }

        if (ev.OrganizerId != organizerId)
        {
            return Result<PagedResponse<RegistrationListItemDto>>.Failure(new Error(Error.Codes.Forbidden, "Only the organizer can view registrations."));
        }

        var (items, total) = await _registrationQueryRepository.ListByEventAsync(eventId, statuses, page, pageSize, ct).ConfigureAwait(false);
        var dtos = items
            .Select(r => new RegistrationListItemDto(
                r.Id,
                r.EventId,
                r.UserId,
                r.Status,
                r.PaidTransactionId,
                r.CreatedAtUtc))
            .ToList();
        var response = BuildPagedResponse(dtos, total, page, pageSize);
        return Result<PagedResponse<RegistrationListItemDto>>.Success(response);
    }

    public async Task<Result<PagedResponse<MyRegistrationDto>>> ListMineAsync(
        Guid userId,
        IEnumerable<EventRegistrationStatus>? statuses,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (items, total) = await _registrationQueryRepository.ListByUserAsync(userId, statuses, page, pageSize, ct).ConfigureAwait(false);
        var dtos = items.Select(tuple => new MyRegistrationDto(
            tuple.Reg.Id,
            tuple.Reg.EventId,
            tuple.Ev.Title,
            tuple.Ev.StartsAt,
            tuple.Ev.Location,
            tuple.Reg.Status)).ToList();

        var response = BuildPagedResponse(dtos, total, page, pageSize);
        return Result<PagedResponse<MyRegistrationDto>>.Success(response);
    }

    private static PagedResponse<T> BuildPagedResponse<T>(IReadOnlyList<T> items, int total, int page, int pageSize)
    {
        var normalizedPageSize = pageSize <= 0 ? PaginationOptions.DefaultPageSize : Math.Clamp(pageSize, 1, PaginationOptions.MaxPageSize);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)normalizedPageSize);
        var normalizedPage = totalPages == 0 ? 1 : Math.Clamp(page <= 0 ? 1 : page, 1, totalPages);
        var hasPrevious = totalPages > 0 && normalizedPage > 1;
        var hasNext = totalPages > 0 && normalizedPage < totalPages;

        return new PagedResponse<T>(
            items,
            normalizedPage,
            normalizedPageSize,
            total,
            totalPages,
            hasPrevious,
            hasNext,
            "CreatedAtUtc",
            true);
    }
}
