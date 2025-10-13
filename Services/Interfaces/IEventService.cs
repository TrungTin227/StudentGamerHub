namespace Services.Interfaces;

public interface IEventService
{
    Task<Result<Guid>> CreateAsync(Guid organizerId, EventCreateRequestDto req, CancellationToken ct = default);
    Task<Result> OpenAsync(Guid organizerId, Guid eventId, CancellationToken ct = default);
    Task<Result> CancelAsync(Guid organizerId, Guid eventId, CancellationToken ct = default);
}
