namespace Repositories.Implements;

public sealed class EventCommandRepository : IEventCommandRepository
{
    private readonly AppDbContext _context;

    public EventCommandRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task CreateAsync(Event e, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        await _context.Events.AddAsync(e, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(Event e, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(e);
        _context.Events.Update(e);
        return Task.CompletedTask;
    }
}
