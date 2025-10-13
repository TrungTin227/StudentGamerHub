namespace Repositories.Interfaces;

public interface IEventCommandRepository
{
    Task CreateAsync(Event e, CancellationToken ct = default);
    Task UpdateAsync(Event e, CancellationToken ct = default);
}
