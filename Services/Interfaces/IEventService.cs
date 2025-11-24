namespace Services.Interfaces;

public interface IEventService
{
    Task<Result<Guid>> CreateAsync(Guid organizerId, EventCreateRequestDto req, CancellationToken ct = default);
    Task<Result> UpdateAsync(Guid organizerId, Guid eventId, EventUpdateRequestDto req, CancellationToken ct = default);
    Task<Result> OpenAsync(Guid currentUserId, Guid eventId, bool isAdmin, CancellationToken ct = default);
    Task<Result> CancelAsync(Guid organizerId, Guid eventId, CancellationToken ct = default);
}
