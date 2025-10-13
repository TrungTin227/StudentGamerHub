namespace Repositories.Interfaces;

public interface IRegistrationCommandRepository
{
    Task CreateAsync(EventRegistration r, CancellationToken ct = default);
    Task UpdateAsync(EventRegistration r, CancellationToken ct = default);
}
