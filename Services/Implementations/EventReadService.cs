using Services.Common.Caching;
using Microsoft.Extensions.Logging;

namespace Services.Implementations;

/// <summary>
/// Read-only queries for events with Redis caching.
/// </summary>
public sealed class EventReadService : IEventReadService
{
    private static readonly TimeSpan EventCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EventListCacheTtl = TimeSpan.FromMinutes(3);

    private readonly IEventQueryRepository _eventQueryRepository;
    private readonly IEscrowRepository _escrowRepository;
    private readonly IRegistrationQueryRepository _registrationQueryRepository;
    private readonly ICacheService _cache;
    private readonly ILogger<EventReadService> _logger;

    public EventReadService(
        IEventQueryRepository eventQueryRepository,
        IEscrowRepository escrowRepository,
        IRegistrationQueryRepository registrationQueryRepository,
        ICacheService cache,
        ILogger<EventReadService> logger)
    {
        _eventQueryRepository = eventQueryRepository ?? throw new ArgumentNullException(nameof(eventQueryRepository));
        _escrowRepository = escrowRepository ?? throw new ArgumentNullException(nameof(escrowRepository));
        _registrationQueryRepository = registrationQueryRepository ?? throw new ArgumentNullException(nameof(registrationQueryRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<EventDetailDto>> GetByIdAsync(Guid currentUserId, Guid eventId, CancellationToken ct = default)
    {
        // ? Cache key includes currentUserId for personalized data
        var cacheKey = $"event:detail:{eventId}:user:{currentUserId}";

        var cached = await _cache.GetAsync<EventDetailDto>(cacheKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            _logger.LogDebug("Event detail cache HIT for eventId: {EventId}", eventId);
            return Result<EventDetailDto>.Success(cached);
        }

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

        var registeredCount = await _eventQueryRepository.CountPendingOrConfirmedAsync(eventId, ct).ConfigureAwait(false);
        var confirmedCount = await _eventQueryRepository.CountConfirmedAsync(eventId, ct).ConfigureAwait(false);

        var dto = ev.ToDetailDto(escrow, ev.OrganizerId == currentUserId, myReg, registeredCount, confirmedCount);

        // Cache for 5 minutes
        await _cache.SetAsync(cacheKey, dto, EventCacheTtl, ct).ConfigureAwait(false);
        _logger.LogDebug("Event detail cached for eventId: {EventId}", eventId);

        return Result<EventDetailDto>.Success(dto);
    }

    public async Task<Result<PagedResponse<EventDetailDto>>> SearchAsync(
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
        CancellationToken ct = default)
    {
        // ? Cache key for search results (without currentUserId - personalization happens in mapping)
        var statusesStr = statuses is not null ? string.Join(",", statuses.Select(s => s.ToString()).OrderBy(s => s)) : "all";
        var cacheKey = $"event:search:{statusesStr}:comm:{communityId}:org:{organizerId}:from:{from:yyyyMMdd}:to:{to:yyyyMMdd}:q:{search}:asc:{sortAscByStartsAt}:p:{page}:s:{pageSize}";

        try
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

            // Cache for 3 minutes (shorter than detail cache due to dynamic nature)
            await _cache.SetAsync(cacheKey, response, EventListCacheTtl, ct).ConfigureAwait(false);

            return Result<PagedResponse<EventDetailDto>>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search events");
            return Result<PagedResponse<EventDetailDto>>.Failure(
                new Error(Error.Codes.Unexpected, "Failed to search events"));
        }
    }

    public async Task<Result<PagedResponse<EventDetailDto>>> SearchMyOrganizedAsync(
        Guid organizerId,
        IEnumerable<EventStatus>? statuses,
        Guid? communityId,
        DateTime? from,
        DateTime? to,
        string? search,
        bool sortAscByStartsAt,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // Note: Not caching this since it's personalized and changes frequently
        var (items, total) = await _eventQueryRepository.SearchAsync(
            statuses,
            communityId,
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
        if (events.Count == 0)
        {
            return Array.Empty<EventDetailDto>();
        }

        var eventIds = events.Select(e => e.Id).ToList();

        // ?? FIXED: Execute queries sequentially to avoid DbContext concurrency issues
        // DbContext is not thread-safe, so we can't run parallel queries on the same instance
        var escrows = await _escrowRepository.GetByEventIdsAsync(eventIds, ct).ConfigureAwait(false);

        Dictionary<Guid, EventRegistration> registrations;
        if (currentUserId != Guid.Empty)
        {
            registrations = await _registrationQueryRepository.GetByEventIdsAndUserAsync(eventIds, currentUserId, ct).ConfigureAwait(false);
        }
        else
        {
            registrations = new Dictionary<Guid, EventRegistration>();
        }

        var registeredCounts = await _registrationQueryRepository.GetRegisteredCountsByEventIdsAsync(eventIds, ct).ConfigureAwait(false);
        var confirmedCounts = await _registrationQueryRepository.GetConfirmedCountsByEventIdsAsync(eventIds, ct).ConfigureAwait(false);

        var results = new List<EventDetailDto>(events.Count);
        foreach (var ev in events)
        {
            escrows.TryGetValue(ev.Id, out var escrow);
            registrations.TryGetValue(ev.Id, out var myReg);
            registeredCounts.TryGetValue(ev.Id, out var registeredCount);
            confirmedCounts.TryGetValue(ev.Id, out var confirmedCount);
            
            results.Add(ev.ToDetailDto(escrow, ev.OrganizerId == currentUserId, myReg, registeredCount, confirmedCount));
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
