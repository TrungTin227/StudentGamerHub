namespace Repositories.Implements;

public sealed class RegistrationCommandRepository : IRegistrationCommandRepository
{
    private readonly AppDbContext _context;

    public RegistrationCommandRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task CreateAsync(EventRegistration r, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(r);
        await _context.EventRegistrations.AddAsync(r, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(EventRegistration r, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(r);
        _context.EventRegistrations.Update(r);
        return Task.CompletedTask;
    }
}
