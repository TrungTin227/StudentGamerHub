namespace Services.Implementations;

/// <summary>
/// Handles payment intent confirmations.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private const string LocalProvider = "LOCAL";

    private readonly IGenericUnitOfWork _uow;
    private readonly IPaymentIntentRepository _paymentIntentRepository;
    private readonly IRegistrationQueryRepository _registrationQueryRepository;
    private readonly IRegistrationCommandRepository _registrationCommandRepository;
    private readonly IEventQueryRepository _eventQueryRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IEscrowRepository _escrowRepository;

    public PaymentService(
        IGenericUnitOfWork uow,
        IPaymentIntentRepository paymentIntentRepository,
        IRegistrationQueryRepository registrationQueryRepository,
        IRegistrationCommandRepository registrationCommandRepository,
        IEventQueryRepository eventQueryRepository,
        ITransactionRepository transactionRepository,
        IWalletRepository walletRepository,
        IEscrowRepository escrowRepository)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _paymentIntentRepository = paymentIntentRepository ?? throw new ArgumentNullException(nameof(paymentIntentRepository));
        _registrationQueryRepository = registrationQueryRepository ?? throw new ArgumentNullException(nameof(registrationQueryRepository));
        _registrationCommandRepository = registrationCommandRepository ?? throw new ArgumentNullException(nameof(registrationCommandRepository));
        _eventQueryRepository = eventQueryRepository ?? throw new ArgumentNullException(nameof(eventQueryRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _escrowRepository = escrowRepository ?? throw new ArgumentNullException(nameof(escrowRepository));
    }

    public async Task<Result> ConfirmAsync(Guid userId, Guid paymentIntentId, CancellationToken ct = default)
    {
        return await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var pi = await _paymentIntentRepository.GetByIdAsync(paymentIntentId, innerCt).ConfigureAwait(false);
            if (pi is null)
            {
                return Result.Failure(new Error(Error.Codes.NotFound, "Payment intent not found."));
            }

            if (pi.UserId != userId)
            {
                return Result.Failure(new Error(Error.Codes.Forbidden, "Payment intent does not belong to the user."));
            }

            if (pi.Status == PaymentIntentStatus.Succeeded)
            {
                return Result.Success();
            }

            if (pi.Status == PaymentIntentStatus.Canceled)
            {
                return Result.Failure(new Error(Error.Codes.Conflict, "Payment intent has been canceled."));
            }

            if (pi.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                return Result.Failure(new Error(Error.Codes.Forbidden, "Payment intent has expired."));
            }

            return pi.Purpose switch
            {
                PaymentPurpose.EventTicket => await ConfirmEventTicketAsync(userId, pi, innerCt).ConfigureAwait(false),
                PaymentPurpose.TopUp => await ConfirmTopUpAsync(userId, pi, innerCt).ConfigureAwait(false),
                _ => Result.Failure(new Error(Error.Codes.Validation, "Unsupported payment purpose.")),
            };
        }, ct: ct).ConfigureAwait(false);
    }

    private async Task<Result> ConfirmEventTicketAsync(Guid userId, PaymentIntent pi, CancellationToken ct)
    {
        if (!pi.EventRegistrationId.HasValue)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Ticket payment intent missing registration."));
        }

        var registration = await _registrationQueryRepository.GetByIdAsync(pi.EventRegistrationId.Value, ct).ConfigureAwait(false);
        if (registration is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Registration not found."));
        }

        if (registration.UserId != userId)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Registration does not belong to the user."));
        }

        if (registration.Status is EventRegistrationStatus.Confirmed or EventRegistrationStatus.CheckedIn)
        {
            pi.Status = PaymentIntentStatus.Succeeded;
            await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
            await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
            return Result.Success();
        }

        if (registration.Status is EventRegistrationStatus.Canceled or EventRegistrationStatus.Refunded)
        {
            return Result.Failure(new Error(Error.Codes.Conflict, "Registration is no longer active."));
        }

        var ev = await _eventQueryRepository.GetForUpdateAsync(registration.EventId, ct).ConfigureAwait(false)
                  ?? await _eventQueryRepository.GetByIdAsync(registration.EventId, ct).ConfigureAwait(false);

        if (ev is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Event not found."));
        }

        if (pi.AmountCents != ev.PriceCents)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Payment amount does not match ticket price."));
        }

        if (ev.Capacity.HasValue)
        {
            var confirmedCount = await _eventQueryRepository.CountConfirmedAsync(ev.Id, ct).ConfigureAwait(false);
            if (confirmedCount >= ev.Capacity.Value)
            {
                return Result.Failure(new Error(Error.Codes.Forbidden, "Event capacity reached."));
            }
        }

        await _walletRepository.CreateIfMissingAsync(userId, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var wallet = await _walletRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (wallet is null)
        {
            return Result.Failure(new Error(Error.Codes.Unexpected, "Wallet could not be loaded."));
        }

        if (pi.AmountCents > 0)
        {
            var debited = await _walletRepository.AdjustBalanceAsync(userId, -pi.AmountCents, ct).ConfigureAwait(false);
            if (!debited)
            {
                return Result.Failure(new Error(Error.Codes.Forbidden, "Insufficient wallet balance."));
            }
        }

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            EventId = ev.Id,
            AmountCents = pi.AmountCents,
            Direction = TransactionDirection.Out,
            Method = TransactionMethod.Wallet,
            Status = TransactionStatus.Succeeded,
            Provider = LocalProvider,
            CreatedBy = userId,
        };

        await _transactionRepository.CreateAsync(tx, ct).ConfigureAwait(false);

        registration.Status = EventRegistrationStatus.Confirmed;
        registration.PaidTransactionId = tx.Id;
        registration.UpdatedBy = userId;
        registration.UpdatedAtUtc = DateTime.UtcNow;

        await _registrationCommandRepository.UpdateAsync(registration, ct).ConfigureAwait(false);

        pi.Status = PaymentIntentStatus.Succeeded;
        pi.UpdatedBy = userId;

        await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<Result> ConfirmTopUpAsync(Guid userId, PaymentIntent pi, CancellationToken ct)
    {
        var eventIdResult = await ResolveTopUpEventIdAsync(pi, ct).ConfigureAwait(false);
        if (eventIdResult.IsFailure)
        {
            return Result.Failure(eventIdResult.Error);
        }

        var ev = await _eventQueryRepository.GetForUpdateAsync(eventIdResult.Value, ct).ConfigureAwait(false)
                  ?? await _eventQueryRepository.GetByIdAsync(eventIdResult.Value, ct).ConfigureAwait(false);

        if (ev is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Event not found for top-up."));
        }

        if (ev.OrganizerId != userId)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Only the organizer can top up this event escrow."));
        }

        await _walletRepository.CreateIfMissingAsync(userId, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var wallet = await _walletRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (wallet is null)
        {
            return Result.Failure(new Error(Error.Codes.Unexpected, "Wallet could not be loaded."));
        }

        if (pi.AmountCents <= 0)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Top-up amount must be positive."));
        }

        var credited = await _walletRepository.AdjustBalanceAsync(userId, pi.AmountCents, ct).ConfigureAwait(false);
        if (!credited)
        {
            return Result.Failure(new Error(Error.Codes.Unexpected, "Failed to credit wallet."));
        }

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            EventId = ev.Id,
            AmountCents = pi.AmountCents,
            Direction = TransactionDirection.In,
            Method = TransactionMethod.Wallet,
            Status = TransactionStatus.Succeeded,
            Provider = LocalProvider,
            CreatedBy = userId,
        };

        await _transactionRepository.CreateAsync(tx, ct).ConfigureAwait(false);

        var escrow = await _escrowRepository.GetByEventIdAsync(ev.Id, ct).ConfigureAwait(false)
                     ?? new Escrow
                     {
                         Id = Guid.NewGuid(),
                         EventId = ev.Id,
                         AmountHoldCents = 0,
                         Status = EscrowStatus.Held,
                         CreatedBy = userId,
                     };

        escrow.AmountHoldCents += pi.AmountCents;
        if (escrow.Status != EscrowStatus.Held)
        {
            escrow.Status = EscrowStatus.Held;
        }

        await _escrowRepository.UpsertAsync(escrow, ct).ConfigureAwait(false);

        pi.Status = PaymentIntentStatus.Succeeded;
        pi.UpdatedBy = userId;

        await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<Result<Guid>> ResolveTopUpEventIdAsync(PaymentIntent pi, CancellationToken ct)
    {
        if (pi.EventRegistrationId.HasValue)
        {
            var reg = await _registrationQueryRepository.GetByIdAsync(pi.EventRegistrationId.Value, ct).ConfigureAwait(false);
            if (reg is not null)
            {
                return Result<Guid>.Success(reg.EventId);
            }
        }

        var separators = new[] { ':', '|', ';', ',', ' ' };
        foreach (var token in pi.ClientSecret.Split(separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(token, out var candidate))
            {
                var ev = await _eventQueryRepository.GetByIdAsync(candidate, ct).ConfigureAwait(false);
                if (ev is not null)
                {
                    return Result<Guid>.Success(ev.Id);
                }
            }
        }

        return Result<Guid>.Failure(new Error(Error.Codes.Validation, "Unable to resolve event for top-up payment intent."));
    }
}
