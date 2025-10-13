namespace Repositories.Interfaces;

public interface IRegistrationQueryRepository
{
    Task<EventRegistration?> GetByEventAndUserAsync(Guid eventId, Guid userId, CancellationToken ct = default);
    Task<EventRegistration?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<EventRegistration> Items, int Total)> ListByEventAsync(
        Guid eventId,
        IEnumerable<EventRegistrationStatus>? statuses,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<(IReadOnlyList<(EventRegistration Reg, Event Ev)> Items, int Total)> ListByUserAsync(
        Guid userId,
        IEnumerable<EventRegistrationStatus>? statuses,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
