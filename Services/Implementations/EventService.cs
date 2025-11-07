using FluentValidation;
using Services.Common.Results;
using Services.Interfaces;

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
    private readonly IUserMembershipRepository _userMembershipRepository;
    private readonly IValidator<EventCreateRequestDto> _createValidator;

    public EventService(
        IGenericUnitOfWork uow,
        IEventCommandRepository eventCommandRepository,
        IEventQueryRepository eventQueryRepository,
        IEscrowRepository escrowRepository,
        IRegistrationQueryRepository registrationQueryRepository,
        IRegistrationCommandRepository registrationCommandRepository,
        IWalletRepository walletRepository,
        ITransactionRepository transactionRepository,
        IUserMembershipRepository userMembershipRepository,
        IValidator<EventCreateRequestDto> createValidator)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _eventCommandRepository = eventCommandRepository ?? throw new ArgumentNullException(nameof(eventCommandRepository));
        _eventQueryRepository = eventQueryRepository ?? throw new ArgumentNullException(nameof(eventQueryRepository));
        _escrowRepository = escrowRepository ?? throw new ArgumentNullException(nameof(escrowRepository));
        _registrationQueryRepository = registrationQueryRepository ?? throw new ArgumentNullException(nameof(registrationQueryRepository));
        _registrationCommandRepository = registrationCommandRepository ?? throw new ArgumentNullException(nameof(registrationCommandRepository));
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _userMembershipRepository = userMembershipRepository ?? throw new ArgumentNullException(nameof(userMembershipRepository));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
    }

    public async Task<Result<Guid>> CreateAsync(Guid organizerId, EventCreateRequestDto req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var validation = await _createValidator.ValidateToResultAsync(req, ct).ConfigureAwait(false);
        if (validation.IsFailure)
        {
            return Result<Guid>.Failure(validation.Error);
        }

        var normalizedPriceCents = req.PriceCents;
        if (normalizedPriceCents <= 0)
        {
            return Result<Guid>.Failure(new Error(Error.Codes.Validation, "PriceCents must be greater than zero."));
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
            PriceCents = normalizedPriceCents,
            Capacity = req.Capacity,
            Status = EventStatus.Draft,
            CreatedBy = organizerId,
        };

        return await _uow.ExecuteTransactionAsync<Guid>(async innerCt =>
        {
            var membership = await _userMembershipRepository
                .GetForUpdateAsync(organizerId, innerCt)
                .ConfigureAwait(false);

            var utcNow = DateTime.UtcNow;

            if (membership is null)
            {
                return Result<Guid>.Failure(new Error(Error.Codes.Forbidden, "You need an active membership plan to create events. Please purchase a membership plan first."));
            }

            if (membership.EndDate < utcNow)
            {
                return Result<Guid>.Failure(new Error(Error.Codes.Forbidden, "Your membership has expired. Please renew your membership to create events."));
            }

            var plan = membership.MembershipPlan;
            if (plan is null)
            {
                return Result<Guid>.Failure(new Error(Error.Codes.Unexpected, "Membership plan information is missing."));
            }

            var policy = ResolveMembershipEventPolicy(plan);
            entity.EscrowMinCents = policy.EscrowMinCents;
            entity.PlatformFeeRate = policy.PlatformFeeRate;
            entity.GatewayFeePolicy = policy.GatewayFeePolicy;

            var resetApplied = membership.ResetMonthlyQuotaIfNeeded(utcNow);
            if (resetApplied)
            {
                membership.UpdatedAtUtc = utcNow;
                membership.UpdatedBy = organizerId;

                await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            }

            var isUnlimited = plan.MonthlyEventLimit == -1;

            if (!isUnlimited)
            {
                var remaining = await _userMembershipRepository
                    .DecrementQuotaIfAvailableAsync(membership.Id, organizerId, utcNow, innerCt)
                    .ConfigureAwait(false);

                if (remaining is null)
                {
                    return Result<Guid>.Failure(new Error(Error.Codes.Forbidden, $"You have reached your monthly event creation limit for the '{plan.Name}' plan. Your quota will reset next month or you can upgrade to a plan with higher limits."));
                }
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

    private static MembershipEventPolicy ResolveMembershipEventPolicy(MembershipPlan plan)
    {
        var monthlyLimit = plan.MonthlyEventLimit;

        if (monthlyLimit <= 0)
        {
            return new MembershipEventPolicy(0, 0m, GatewayFeePolicy.OrganizerPays);
        }

        if (monthlyLimit <= 3)
        {
            return new MembershipEventPolicy(0, 0.05m, GatewayFeePolicy.OrganizerPays);
        }

        if (monthlyLimit <= 10)
        {
            return new MembershipEventPolicy(0, 0.03m, GatewayFeePolicy.OrganizerPays);
        }

        return new MembershipEventPolicy(0, 0.02m, GatewayFeePolicy.OrganizerPays);
    }

    private readonly record struct MembershipEventPolicy(long EscrowMinCents, decimal PlatformFeeRate, GatewayFeePolicy GatewayFeePolicy);

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
                var wallet = await _walletRepository.EnsureAsync(reg.UserId, innerCt).ConfigureAwait(false);

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
