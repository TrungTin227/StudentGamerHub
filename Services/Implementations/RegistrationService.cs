using System.Data;

namespace Services.Implementations;

/// <summary>
/// Handles attendee registrations for events.
/// </summary>
public sealed class RegistrationService : IRegistrationService
{
    private readonly IGenericUnitOfWork _uow;
    private readonly IEventQueryRepository _eventQueryRepository;
    private readonly IRegistrationQueryRepository _registrationQueryRepository;
    private readonly IRegistrationCommandRepository _registrationCommandRepository;
    private readonly IPaymentIntentRepository _paymentIntentRepository;

    public RegistrationService(
        IGenericUnitOfWork uow,
        IEventQueryRepository eventQueryRepository,
        IRegistrationQueryRepository registrationQueryRepository,
        IRegistrationCommandRepository registrationCommandRepository,
        IPaymentIntentRepository paymentIntentRepository)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _eventQueryRepository = eventQueryRepository ?? throw new ArgumentNullException(nameof(eventQueryRepository));
        _registrationQueryRepository = registrationQueryRepository ?? throw new ArgumentNullException(nameof(registrationQueryRepository));
        _registrationCommandRepository = registrationCommandRepository ?? throw new ArgumentNullException(nameof(registrationCommandRepository));
        _paymentIntentRepository = paymentIntentRepository ?? throw new ArgumentNullException(nameof(paymentIntentRepository));
    }

    public async Task<Result<Guid>> RegisterAsync(Guid userId, Guid eventId, CancellationToken ct = default)
    {
        return await _uow.ExecuteTransactionAsync<Guid>(async innerCt =>
        {
            var ev = await _eventQueryRepository
                .GetForUpdateAsync(eventId, innerCt)
                .ConfigureAwait(false)
                ?? await _eventQueryRepository
                    .GetByIdAsync(eventId, innerCt)
                    .ConfigureAwait(false);
            if (ev is null)
            {
                return Result<Guid>.Failure(new Error(Error.Codes.NotFound, "Event not found."));
            }

            if (ev.Status != EventStatus.Open)
            {
                return Result<Guid>.Failure(new Error(Error.Codes.Forbidden, "Event is not open for registration."));
            }

            var existing = await _registrationQueryRepository.GetByEventAndUserAsync(eventId, userId, innerCt).ConfigureAwait(false);
            if (existing is not null)
            {
                return Result<Guid>.Failure(new Error(Error.Codes.Conflict, "You have already registered for this event."));
            }

            if (ev.Capacity.HasValue)
            {
                var nowUtc = DateTime.UtcNow;
                var confirmedCount = await _eventQueryRepository
                    .CountConfirmedAsync(eventId, innerCt)
                    .ConfigureAwait(false);
                var activePendingCount = await _paymentIntentRepository
                    .CountActivePendingByEventAsync(eventId, nowUtc, innerCt)
                    .ConfigureAwait(false);

                if (confirmedCount + activePendingCount >= ev.Capacity.Value)
                {
                    return Result<Guid>.Failure(new Error(Error.Codes.Forbidden, "Event has reached capacity."));
                }
            }

            var registration = new EventRegistration
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                UserId = userId,
                Status = EventRegistrationStatus.Pending,
                RegisteredAt = DateTime.UtcNow,
                CreatedBy = userId,
            };

            await _registrationCommandRepository.CreateAsync(registration, innerCt).ConfigureAwait(false);

            var eventPrice = ev.PriceCents ?? 0;
            var paymentIntent = new PaymentIntent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AmountCents = eventPrice,
                Purpose = PaymentPurpose.EventTicket,
                EventRegistrationId = registration.Id,
                EventId = eventId,
                Status = PaymentIntentStatus.RequiresPayment,
                ClientSecret = Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedBy = userId,
            };

            await _paymentIntentRepository.CreateAsync(paymentIntent, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            return Result<Guid>.Success(paymentIntent.Id);
        }, isolationLevel: IsolationLevel.Serializable, ct: ct).ConfigureAwait(false);
    }
}
