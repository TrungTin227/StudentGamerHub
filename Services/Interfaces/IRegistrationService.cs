namespace Services.Interfaces;

public interface IRegistrationService
{
    Task<Result<Guid>> RegisterAsync(Guid userId, Guid eventId, CancellationToken ct = default);
}
