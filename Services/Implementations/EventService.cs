using FluentValidation;
using Microsoft.Extensions.Options;
using Services.Common.Results;
using Services.Configuration;
using System.Text.Json;

namespace Services.Implementations;

/// <summary>
/// Handles event lifecycle operations (create, open, cancel).
/// </summary>
public sealed class EventService : IEventService
{
    private readonly IGenericUnitOfWork _uow;
    private readonly IEventCommandRepository _eventCommandRepository;
    private readonly IEventQueryRepository _eventQueryRepository;
    private readonly IEscrowRepository _escrowRepository;
    private readonly IRegistrationQueryRepository _registrationQueryRepository;
    private readonly IRegistrationCommandRepository _registrationCommandRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IValidator<EventCreateRequestDto> _createValidator;
    private readonly BillingOptions _billingOptions;
    private const string SystemProvider = "SYSTEM";
    private const string EventCreateFeeNote = "EVENT_CREATE_FEE";

    public EventService(
        IGenericUnitOfWork uow,
        IEventCommandRepository eventCommandRepository,
        IEventQueryRepository eventQueryRepository,
        IEscrowRepository escrowRepository,
        IRegistrationQueryRepository registrationQueryRepository,
        IRegistrationCommandRepository registrationCommandRepository,
        IWalletRepository walletRepository,
        ITransactionRepository transactionRepository,
        IValidator<EventCreateRequestDto> createValidator,
        IOptionsSnapshot<BillingOptions> billingOptions)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _eventCommandRepository = eventCommandRepository ?? throw new ArgumentNullException(nameof(eventCommandRepository));
        _eventQueryRepository = eventQueryRepository ?? throw new ArgumentNullException(nameof(eventQueryRepository));
        _escrowRepository = escrowRepository ?? throw new ArgumentNullException(nameof(escrowRepository));
        _registrationQueryRepository = registrationQueryRepository ?? throw new ArgumentNullException(nameof(registrationQueryRepository));
        _registrationCommandRepository = registrationCommandRepository ?? throw new ArgumentNullException(nameof(registrationCommandRepository));
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _billingOptions = billingOptions?.Value ?? throw new ArgumentNullException(nameof(billingOptions));
    }

    public async Task<Result<Guid>> CreateAsync(Guid organizerId, EventCreateRequestDto req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var validation = await _createValidator.ValidateToResultAsync(req, ct).ConfigureAwait(false);
        if (validation.IsFailure)
        {
            return Result<Guid>.Failure(validation.Error);
        }

        var entity = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            CommunityId = req.CommunityId,
            Title = req.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            Mode = req.Mode,
            Location = string.IsNullOrWhiteSpace(req.Location) ? null : req.Location.Trim(),
            StartsAt = req.StartsAt,
            EndsAt = req.EndsAt,
            PriceCents = req.PriceCents,
            Capacity = req.Capacity,
            EscrowMinCents = req.EscrowMinCents,
            PlatformFeeRate = req.PlatformFeeRate,
            GatewayFeePolicy = req.GatewayFeePolicy,
            Status = EventStatus.Draft,
            CreatedBy = organizerId,
        };

        return await _uow.ExecuteTransactionAsync<Guid>(async innerCt =>
        {
            var feeCents = Math.Max(0, _billingOptions.EventCreationFeeCents);
            Guid? platformUserId = _billingOptions.PlatformUserId;

            Wallet? organizerWallet = null;
            Wallet? platformWallet = null;

            if (feeCents > 0)
            {
                if (!platformUserId.HasValue || platformUserId.Value == Guid.Empty)
                {
                    return Result<Guid>.Failure(new Error(Error.Codes.Unexpected, "Platform wallet is not configured."));
                }

                await _walletRepository.CreateIfMissingAsync(organizerId, innerCt).ConfigureAwait(false);
                await _walletRepository.CreateIfMissingAsync(platformUserId.Value, innerCt).ConfigureAwait(false);
                await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

                organizerWallet = await _walletRepository.GetByUserIdAsync(organizerId, innerCt).ConfigureAwait(false);
                if (organizerWallet is null)
                {
                    return Result<Guid>.Failure(new Error(Error.Codes.Unexpected, "Organizer wallet could not be loaded."));
                }

                platformWallet = await _walletRepository.GetByUserIdAsync(platformUserId.Value, innerCt).ConfigureAwait(false);
                if (platformWallet is null)
                {
                    return Result<Guid>.Failure(new Error(Error.Codes.Unexpected, "Platform wallet could not be loaded."));
                }

                if (organizerWallet.BalanceCents < feeCents)
                {
                    var deficit = feeCents - organizerWallet.BalanceCents;
                    return Result<Guid>.Failure(new Error(Error.Codes.Forbidden, $"Insufficient wallet balance. deficitCents={deficit}"));
                }

                var debited = await _walletRepository.AdjustBalanceAsync(organizerId, -feeCents, innerCt).ConfigureAwait(false);
                if (!debited)
                {
                    var refreshed = await _walletRepository.GetByUserIdAsync(organizerId, innerCt).ConfigureAwait(false);
                    var balance = refreshed?.BalanceCents ?? 0;
                    var deficit = Math.Max(0, feeCents - balance);
                    return Result<Guid>.Failure(new Error(Error.Codes.Forbidden, $"Insufficient wallet balance. deficitCents={deficit}"));
                }

                var credited = await _walletRepository.AdjustBalanceAsync(platformUserId.Value, feeCents, innerCt).ConfigureAwait(false);
                if (!credited)
                {
                    return Result<Guid>.Failure(new Error(Error.Codes.Unexpected, "Failed to credit platform wallet."));
                }

                var organizerTx = new Transaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = organizerWallet.Id,
                    EventId = entity.Id,
                    AmountCents = feeCents,
                    Direction = TransactionDirection.Out,
                    Method = TransactionMethod.Wallet,
                    Status = TransactionStatus.Succeeded,
                    Provider = SystemProvider,
                    ProviderRef = entity.Id.ToString("N"),
                    Metadata = CreateEventFeeMetadata(entity.Id, EventCreateFeeNote, platformUserId.Value),
                    CreatedBy = organizerId,
                };

                var platformTx = new Transaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = platformWallet.Id,
                    EventId = entity.Id,
                    AmountCents = feeCents,
                    Direction = TransactionDirection.In,
                    Method = TransactionMethod.Wallet,
                    Status = TransactionStatus.Succeeded,
                    Provider = SystemProvider,
                    ProviderRef = entity.Id.ToString("N"),
                    Metadata = CreateEventFeeMetadata(entity.Id, EventCreateFeeNote, organizerId),
                    CreatedBy = platformUserId.Value,
                };

                await _transactionRepository.CreateAsync(organizerTx, innerCt).ConfigureAwait(false);
                await _transactionRepository.CreateAsync(platformTx, innerCt).ConfigureAwait(false);
            }

            await _eventCommandRepository.CreateAsync(entity, innerCt).ConfigureAwait(false);

            var escrow = new Escrow
            {
                Id = Guid.NewGuid(),
                EventId = entity.Id,
                AmountHoldCents = 0,
                Status = EscrowStatus.Held,
                CreatedBy = organizerId,
            };

            await _escrowRepository.UpsertAsync(escrow, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            return Result<Guid>.Success(entity.Id);
        }, ct: ct).ConfigureAwait(false);
    }

    private static JsonDocument CreateEventFeeMetadata(Guid eventId, string note, Guid counterpartyUserId)
    {
        var payload = new Dictionary<string, object?>
        {
            ["note"] = note,
            ["eventId"] = eventId,
            ["counterpartyUserId"] = counterpartyUserId,
        };

        return JsonSerializer.SerializeToDocument(payload);
    }

    public async Task<Result> OpenAsync(Guid organizerId, Guid eventId, CancellationToken ct = default)
    {
        return await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var ev = await _eventQueryRepository.GetForUpdateAsync(eventId, innerCt).ConfigureAwait(false)
                     ?? await _eventQueryRepository.GetByIdAsync(eventId, innerCt).ConfigureAwait(false);

            if (ev is null)
            {
                return Result.Failure(new Error(Error.Codes.NotFound, "Event not found."));
            }

            if (ev.OrganizerId != organizerId)
            {
                return Result.Failure(new Error(Error.Codes.Forbidden, "Only the organizer can open this event."));
            }

            if (ev.Status != EventStatus.Draft)
            {
                return Result.Failure(new Error(Error.Codes.Conflict, "Event must be Draft to open."));
            }

            var escrow = await _escrowRepository.GetByEventIdAsync(eventId, innerCt).ConfigureAwait(false);
            if (escrow is null)
            {
                escrow = new Escrow
                {
                    Id = Guid.NewGuid(),
                    EventId = eventId,
                    AmountHoldCents = 0,
                    Status = EscrowStatus.Held,
                    CreatedBy = organizerId,
                };

                await _escrowRepository.UpsertAsync(escrow, innerCt).ConfigureAwait(false);
            }

            if (escrow.Status != EscrowStatus.Held)
            {
                return Result.Failure(new Error(Error.Codes.Forbidden, "Escrow must be held before the event can be opened."));
            }

            if (escrow.AmountHoldCents < ev.EscrowMinCents)
            {
                var needed = Math.Max(0, ev.EscrowMinCents - escrow.AmountHoldCents);
                return Result.Failure(new Error(Error.Codes.Forbidden, $"Escrow hold is insufficient. topUpNeededCents={needed}"));
            }

            ev.Status = EventStatus.Open;
            ev.UpdatedBy = organizerId;
            ev.UpdatedAtUtc = DateTime.UtcNow;

            await _eventCommandRepository.UpdateAsync(ev, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            return Result.Success();
        }, ct: ct).ConfigureAwait(false);
    }

    public async Task<Result> CancelAsync(Guid organizerId, Guid eventId, CancellationToken ct = default)
    {
        return await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var ev = await _eventQueryRepository.GetForUpdateAsync(eventId, innerCt).ConfigureAwait(false)
                     ?? await _eventQueryRepository.GetByIdAsync(eventId, innerCt).ConfigureAwait(false);

            if (ev is null)
            {
                return Result.Failure(new Error(Error.Codes.NotFound, "Event not found."));
            }

            if (ev.OrganizerId != organizerId)
            {
                return Result.Failure(new Error(Error.Codes.Forbidden, "Only the organizer can cancel this event."));
            }

            if (ev.Status == EventStatus.Canceled)
            {
                return Result.Success();
            }

            if (DateTime.UtcNow >= ev.StartsAt)
            {
                return Result.Failure(new Error(Error.Codes.Forbidden, "Event cannot be canceled after it starts."));
            }

            var (registrations, _) = await _registrationQueryRepository
                .ListByEventAsync(eventId, new[] { EventRegistrationStatus.Confirmed, EventRegistrationStatus.CheckedIn }, 1, int.MaxValue, innerCt)
                .ConfigureAwait(false);

            foreach (var reg in registrations)
            {
                await _walletRepository.CreateIfMissingAsync(reg.UserId, innerCt).ConfigureAwait(false);
                await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

                var wallet = await _walletRepository.GetByUserIdAsync(reg.UserId, innerCt).ConfigureAwait(false);
                if (wallet is null)
                {
                    return Result.Failure(new Error(Error.Codes.Unexpected, "Wallet could not be loaded for refund."));
                }

                if (ev.PriceCents > 0)
                {
                    var credited = await _walletRepository.AdjustBalanceAsync(reg.UserId, ev.PriceCents, innerCt).ConfigureAwait(false);
                    if (!credited)
                    {
                        return Result.Failure(new Error(Error.Codes.Unexpected, "Failed to refund attendee wallet."));
                    }
                }

                var refundTx = new Transaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    EventId = ev.Id,
                    AmountCents = ev.PriceCents,
                    Direction = TransactionDirection.In,
                    Method = TransactionMethod.Wallet,
                    Status = TransactionStatus.Succeeded,
                    Provider = "LOCAL",
                    CreatedBy = organizerId,
                };

                await _transactionRepository.CreateAsync(refundTx, innerCt).ConfigureAwait(false);

                reg.Status = EventRegistrationStatus.Refunded;
                reg.UpdatedBy = organizerId;
                reg.UpdatedAtUtc = DateTime.UtcNow;

                await _registrationCommandRepository.UpdateAsync(reg, innerCt).ConfigureAwait(false);
            }

            ev.Status = EventStatus.Canceled;
            ev.UpdatedBy = organizerId;
            ev.UpdatedAtUtc = DateTime.UtcNow;

            await _eventCommandRepository.UpdateAsync(ev, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            return Result.Success();
        }, ct: ct).ConfigureAwait(false);
    }
}
