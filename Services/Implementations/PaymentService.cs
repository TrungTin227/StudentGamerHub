using System;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Services.Configuration;

namespace Services.Implementations;

/// <summary>
/// Handles payment intent confirmations.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private const string LocalProvider = "LOCAL";
    private const string VnPayProvider = "VNPAY";

    private readonly IGenericUnitOfWork _uow;
    private readonly IPaymentIntentRepository _paymentIntentRepository;
    private readonly IRegistrationQueryRepository _registrationQueryRepository;
    private readonly IRegistrationCommandRepository _registrationCommandRepository;
    private readonly IEventQueryRepository _eventQueryRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IEscrowRepository _escrowRepository;
    private readonly IVnPayService _vnPayService;
    private readonly BillingOptions _billingOptions;
    private readonly VnPayConfig _vnPayConfig;

    public PaymentService(
        IGenericUnitOfWork uow,
        IPaymentIntentRepository paymentIntentRepository,
        IRegistrationQueryRepository registrationQueryRepository,
        IRegistrationCommandRepository registrationCommandRepository,
        IEventQueryRepository eventQueryRepository,
        ITransactionRepository transactionRepository,
        IWalletRepository walletRepository,
        IEscrowRepository escrowRepository,
        IVnPayService vnPayService,
        IOptionsSnapshot<BillingOptions> billingOptions,
        IOptionsSnapshot<VnPayConfig> vnPayOptions)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _paymentIntentRepository = paymentIntentRepository ?? throw new ArgumentNullException(nameof(paymentIntentRepository));
        _registrationQueryRepository = registrationQueryRepository ?? throw new ArgumentNullException(nameof(registrationQueryRepository));
        _registrationCommandRepository = registrationCommandRepository ?? throw new ArgumentNullException(nameof(registrationCommandRepository));
        _eventQueryRepository = eventQueryRepository ?? throw new ArgumentNullException(nameof(eventQueryRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _escrowRepository = escrowRepository ?? throw new ArgumentNullException(nameof(escrowRepository));
        _vnPayService = vnPayService ?? throw new ArgumentNullException(nameof(vnPayService));
        _billingOptions = billingOptions?.Value ?? throw new ArgumentNullException(nameof(billingOptions));
        _vnPayConfig = vnPayOptions?.Value ?? throw new ArgumentNullException(nameof(vnPayOptions));
    }

    public Task<Result<Guid>> CreateTopUpIntentAsync(Guid organizerId, Guid eventId, long amountCents, CancellationToken ct = default)
    {
        return _uow.ExecuteTransactionAsync<Guid>(async innerCt =>
        {
            if (amountCents <= 0)
            {
                return Result<Guid>.Failure(new Error(Error.Codes.Validation, "Top-up amount must be positive."));
            }

            var maxEscrow = _billingOptions.MaxEventEscrowTopUpAmountCents;
            if (maxEscrow > 0 && amountCents > maxEscrow)
            {
                return Result<Guid>.Failure(new Error(Error.Codes.Validation, "Top-up amount exceeds the allowed limit."));
            }

            var ev = await _eventQueryRepository.GetByIdAsync(eventId, innerCt).ConfigureAwait(false);
            if (ev is null)
            {
                return Result<Guid>.Failure(new Error(Error.Codes.NotFound, "Event not found."));
            }

            if (ev.OrganizerId != organizerId)
            {
                return Result<Guid>.Failure(new Error(Error.Codes.Forbidden, "Only the organizer can top up this event escrow."));
            }

            var paymentIntent = new PaymentIntent
            {
                Id = Guid.NewGuid(),
                UserId = organizerId,
                AmountCents = amountCents,
                Purpose = PaymentPurpose.TopUp,
                EventId = ev.Id,
                Status = PaymentIntentStatus.RequiresPayment,
                ClientSecret = Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedBy = organizerId,
            };

            await _paymentIntentRepository.CreateAsync(paymentIntent, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            return Result<Guid>.Success(paymentIntent.Id);
        }, ct: ct);
    }

    public Task<Result<Guid>> CreateWalletTopUpIntentAsync(Guid userId, long amountCents, CancellationToken ct = default)
    {
        return _uow.ExecuteTransactionAsync<Guid>(async innerCt =>
        {
            if (amountCents <= 0)
            {
                return Result<Guid>.Failure(new Error(Error.Codes.Validation, "Top-up amount must be positive."));
            }

            var maxWallet = _billingOptions.MaxWalletTopUpAmountCents;
            if (maxWallet > 0 && amountCents > maxWallet)
            {
                return Result<Guid>.Failure(new Error(Error.Codes.Validation, "Top-up amount exceeds the allowed limit."));
            }

            var paymentIntent = new PaymentIntent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AmountCents = amountCents,
                Purpose = PaymentPurpose.WalletTopUp,
                Status = PaymentIntentStatus.RequiresPayment,
                ClientSecret = Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedBy = userId,
            };

            try
            {
                await _paymentIntentRepository.CreateAsync(paymentIntent, innerCt).ConfigureAwait(false);
                await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            }
            catch (DbUpdateException ex) when (IsWalletTopUpPurposeConstraintViolation(ex))
            {
                return Result<Guid>.Failure(new Error(Error.Codes.Validation, "WalletTopUpNotEnabledBySchema"));
            }

            return Result<Guid>.Success(paymentIntent.Id);
        }, ct: ct);
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

            if (pi.ExpiresAt <= DateTime.UtcNow)
            {
                return Result.Failure(new Error(Error.Codes.Forbidden, "Payment intent has expired."));
            }

            return pi.Purpose switch
            {
                PaymentPurpose.EventTicket => await ConfirmEventTicketAsync(userId, pi, innerCt).ConfigureAwait(false),
                PaymentPurpose.TopUp => await ConfirmTopUpAsync(userId, pi, innerCt).ConfigureAwait(false),
                PaymentPurpose.WalletTopUp => Result.Failure(new Error(Error.Codes.Validation, "WalletTopUpRequiresProviderCallback")),
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
            Metadata = CreateMetadata("EVENT_TICKET", ev.Id, null),
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
        if (!pi.EventId.HasValue)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Top-up intent missing EventId."));
        }

        var ev = await _eventQueryRepository.GetForUpdateAsync(pi.EventId.Value, ct).ConfigureAwait(false)
                  ?? await _eventQueryRepository.GetByIdAsync(pi.EventId.Value, ct).ConfigureAwait(false);

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

        var debited = await _walletRepository.AdjustBalanceAsync(userId, -pi.AmountCents, ct).ConfigureAwait(false);
        if (!debited)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Insufficient wallet balance."));
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
            Metadata = CreateMetadata("ESCROW_TOP_UP", ev.Id, null),
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

    public async Task<Result<string>> CreateHostedCheckoutUrlAsync(Guid userId, Guid paymentIntentId, string? returnUrl, string clientIp, CancellationToken ct = default)
    {
        return await _uow.ExecuteTransactionAsync<string>(async innerCt =>
        {
            var pi = await _paymentIntentRepository.GetByIdAsync(paymentIntentId, innerCt).ConfigureAwait(false);
            if (pi is null)
            {
                return Result<string>.Failure(new Error(Error.Codes.NotFound, "Payment intent not found."));
            }

            if (pi.UserId != userId)
            {
                return Result<string>.Failure(new Error(Error.Codes.Forbidden, "Payment intent does not belong to the user."));
            }

            if (pi.Status != PaymentIntentStatus.RequiresPayment)
            {
                return Result<string>.Failure(new Error(Error.Codes.Conflict, "Payment intent is not in RequiresPayment status."));
            }

            if (pi.ExpiresAt <= DateTime.UtcNow)
            {
                return Result<string>.Failure(new Error(Error.Codes.Forbidden, "Payment intent has expired."));
            }

            // For EventTicket: verify amount matches Event.PriceCents
            if (pi.Purpose == PaymentPurpose.EventTicket && pi.EventRegistrationId.HasValue)
            {
                var reg = await _registrationQueryRepository.GetByIdAsync(pi.EventRegistrationId.Value, innerCt).ConfigureAwait(false);
                if (reg is null)
                {
                    return Result<string>.Failure(new Error(Error.Codes.NotFound, "Registration not found."));
                }

                var ev = await _eventQueryRepository.GetByIdAsync(reg.EventId, innerCt).ConfigureAwait(false);
                if (ev is null)
                {
                    return Result<string>.Failure(new Error(Error.Codes.NotFound, "Event not found."));
                }

                if (pi.AmountCents != ev.PriceCents)
                {
                    return Result<string>.Failure(new Error(Error.Codes.Validation, "Payment amount does not match ticket price."));
                }
            }

            // Build VNPAY request
            var vnpTxnRef = pi.Id.ToString("N"); // 32 chars without dashes
            var createDate = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

            var vnpRequest = new DTOs.Payments.VnPay.VnPayCreatePaymentRequest
            {
                vnp_Amount = pi.AmountCents,
                vnp_TxnRef = vnpTxnRef,
                vnp_OrderInfo = $"PI:{pi.Id}",
                vnp_OrderType = "other",
                vnp_Locale = "vn",
                vnp_IpAddr = string.IsNullOrWhiteSpace(clientIp) ? "127.0.0.1" : clientIp,
                vnp_CreateDate = createDate
            };

            var resolvedReturnUrl = ResolveReturnUrl(returnUrl);
            if (string.IsNullOrWhiteSpace(resolvedReturnUrl))
            {
                return Result<string>.Failure(new Error(Error.Codes.Validation, "Return URL is not allowed."));
            }

            var vnpResponse = await _vnPayService.CreatePaymentUrlAsync(vnpRequest, resolvedReturnUrl).ConfigureAwait(false);

            if (!vnpResponse.Success)
            {
                return Result<string>.Failure(new Error(Error.Codes.Unexpected, vnpResponse.Message));
            }

            // Store vnp_TxnRef in ClientSecret for lookup (document this approach)
            pi.ClientSecret = vnpTxnRef;
            await _paymentIntentRepository.UpdateAsync(pi, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            return Result<string>.Success(vnpResponse.PaymentUrl);
        }, ct: ct).ConfigureAwait(false);
    }

    public async Task<Result> HandleVnPayCallbackAsync(IQueryCollection query, IFormCollection? form, CancellationToken ct = default)
    {
        // Build callback request from query parameters
        var cb = new DTOs.Payments.VnPay.VnPayCallbackRequest();

        try
        {
            cb = new DTOs.Payments.VnPay.VnPayCallbackRequest
            {
                vnp_Amount = long.TryParse(query["vnp_Amount"], out var amt) ? amt : 0,
                vnp_BankCode = query["vnp_BankCode"],
                vnp_BankTranNo = query["vnp_BankTranNo"],
                vnp_CardType = query["vnp_CardType"],
                vnp_OrderInfo = query["vnp_OrderInfo"],
                vnp_PayDate = query["vnp_PayDate"],
                vnp_ResponseCode = query["vnp_ResponseCode"].ToString() ?? string.Empty,
                vnp_TmnCode = query["vnp_TmnCode"],
                vnp_TransactionNo = query["vnp_TransactionNo"],
                vnp_TransactionStatus = query["vnp_TransactionStatus"],
                vnp_TxnRef = query["vnp_TxnRef"].ToString() ?? string.Empty,
                vnp_SecureHash = query["vnp_SecureHash"].ToString() ?? string.Empty,
                vnp_SecureHashType = query["vnp_SecureHashType"]
            };
        }
        catch
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Invalid callback parameters."));
        }

        // Validate signature
        if (!_vnPayService.ValidateCallback(cb))
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Invalid signature."));
        }

        // Check response code
        if (cb.vnp_ResponseCode != "00")
        {
            return Result.Failure(new Error(Error.Codes.Validation, $"Payment failed with code {cb.vnp_ResponseCode}."));
        }

        // Parse PaymentIntent ID from vnp_TxnRef
        if (!Guid.TryParseExact(cb.vnp_TxnRef, "N", out var paymentIntentId))
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Invalid TxnRef format."));
        }

        return await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var pi = await _paymentIntentRepository.GetByIdAsync(paymentIntentId, innerCt).ConfigureAwait(false);
            if (pi is null)
            {
                return Result.Failure(new Error(Error.Codes.NotFound, "Payment intent not found."));
            }

            // Idempotency: if already succeeded, return success
            if (pi.Status == PaymentIntentStatus.Succeeded)
            {
                return Result.Success();
            }

            if (pi.Status != PaymentIntentStatus.RequiresPayment)
            {
                return Result.Failure(new Error(Error.Codes.Conflict, "Payment intent is not in RequiresPayment status."));
            }

            // Verify amount matches (vnp_Amount is in VND smallest unit * 100)
            var expectedVnpAmount = pi.AmountCents * 100;
            if (cb.vnp_Amount != expectedVnpAmount)
            {
                return Result.Failure(new Error(Error.Codes.Validation, "Amount mismatch."));
            }

            // Process based on purpose
            return pi.Purpose switch
            {
                PaymentPurpose.EventTicket => await ConfirmEventTicketViaVnPayAsync(pi, cb.vnp_TransactionNo ?? cb.vnp_TxnRef, innerCt).ConfigureAwait(false),
                PaymentPurpose.TopUp => await ConfirmTopUpViaVnPayAsync(pi, cb.vnp_TransactionNo ?? cb.vnp_TxnRef, innerCt).ConfigureAwait(false),
                PaymentPurpose.WalletTopUp => await ConfirmWalletTopUpViaVnPayAsync(pi, cb.vnp_TransactionNo ?? cb.vnp_TxnRef, innerCt).ConfigureAwait(false),
                _ => Result.Failure(new Error(Error.Codes.Validation, "Unsupported payment purpose.")),
            };
        }, ct: ct).ConfigureAwait(false);
    }

    private async Task<Result> ConfirmEventTicketViaVnPayAsync(PaymentIntent pi, string providerRef, CancellationToken ct)
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

        // Check if transaction already exists (idempotency)
        var txExists = await _transactionRepository.ExistsByProviderRefAsync(VnPayProvider, providerRef, ct).ConfigureAwait(false);
        if (txExists)
        {
            // Already processed, mark PI as succeeded
            pi.Status = PaymentIntentStatus.Succeeded;
            pi.UpdatedBy = pi.UserId;
            await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
            await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
            return Result.Success();
        }

        await _walletRepository.CreateIfMissingAsync(registration.UserId, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var wallet = await _walletRepository.GetByUserIdAsync(registration.UserId, ct).ConfigureAwait(false);
        if (wallet is null)
        {
            return Result.Failure(new Error(Error.Codes.Unexpected, "Wallet could not be loaded."));
        }

        // For VNPAY payment, no wallet deduction needed (paid via gateway)
        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            EventId = ev.Id,
            AmountCents = pi.AmountCents,
            Direction = TransactionDirection.Out,
            Method = TransactionMethod.Gateway,
            Status = TransactionStatus.Succeeded,
            Provider = VnPayProvider,
            ProviderRef = providerRef,
            Metadata = CreateMetadata("EVENT_TICKET", ev.Id, null),
            CreatedBy = registration.UserId,
        };

        await _transactionRepository.CreateAsync(tx, ct).ConfigureAwait(false);

        registration.Status = EventRegistrationStatus.Confirmed;
        registration.PaidTransactionId = tx.Id;
        registration.UpdatedBy = registration.UserId;
        registration.UpdatedAtUtc = DateTime.UtcNow;

        await _registrationCommandRepository.UpdateAsync(registration, ct).ConfigureAwait(false);

        pi.Status = PaymentIntentStatus.Succeeded;
        pi.UpdatedBy = pi.UserId;

        if (string.IsNullOrWhiteSpace(pi.ClientSecret))
        {
            pi.ClientSecret = providerRef;
        }

        await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<Result> ConfirmTopUpViaVnPayAsync(PaymentIntent pi, string providerRef, CancellationToken ct)
    {
        if (!pi.EventId.HasValue)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Top-up intent missing EventId."));
        }

        var ev = await _eventQueryRepository.GetForUpdateAsync(pi.EventId.Value, ct).ConfigureAwait(false)
                  ?? await _eventQueryRepository.GetByIdAsync(pi.EventId.Value, ct).ConfigureAwait(false);

        if (ev is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Event not found for top-up."));
        }

        if (ev.OrganizerId != pi.UserId)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Only the organizer can top up this event escrow."));
        }

        var txExists = await _transactionRepository.ExistsByProviderRefAsync(VnPayProvider, providerRef, ct).ConfigureAwait(false);
        if (txExists)
        {
            if (pi.Status != PaymentIntentStatus.Succeeded)
            {
                pi.Status = PaymentIntentStatus.Succeeded;
                pi.UpdatedBy = pi.UserId;
                await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
                await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return Result.Success();
        }

        await _walletRepository.CreateIfMissingAsync(pi.UserId, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var wallet = await _walletRepository.GetByUserIdAsync(pi.UserId, ct).ConfigureAwait(false);
        if (wallet is null)
        {
            return Result.Failure(new Error(Error.Codes.Unexpected, "Wallet could not be loaded."));
        }

        if (pi.AmountCents <= 0)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Top-up amount must be positive."));
        }

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            EventId = ev.Id,
            AmountCents = pi.AmountCents,
            Direction = TransactionDirection.In,
            Method = TransactionMethod.Gateway,
            Status = TransactionStatus.Succeeded,
            Provider = VnPayProvider,
            ProviderRef = providerRef,
            Metadata = CreateMetadata("ESCROW_TOP_UP", ev.Id, null),
            CreatedBy = pi.UserId,
        };

        try
        {
            await _transactionRepository.CreateAsync(tx, ct).ConfigureAwait(false);
            await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            if (pi.Status != PaymentIntentStatus.Succeeded)
            {
                pi.Status = PaymentIntentStatus.Succeeded;
                pi.UpdatedBy = pi.UserId;
                await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
                await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return Result.Success();
        }

        var credited = await _walletRepository.AdjustBalanceAsync(pi.UserId, pi.AmountCents, ct).ConfigureAwait(false);
        if (!credited)
        {
            return Result.Failure(new Error(Error.Codes.Unexpected, "Failed to credit wallet."));
        }

        var escrow = await _escrowRepository.GetByEventIdAsync(ev.Id, ct).ConfigureAwait(false)
                     ?? new Escrow
                     {
                         Id = Guid.NewGuid(),
                         EventId = ev.Id,
                         AmountHoldCents = 0,
                         Status = EscrowStatus.Held,
                         CreatedBy = pi.UserId,
                     };

        escrow.AmountHoldCents += pi.AmountCents;
        if (escrow.Status != EscrowStatus.Held)
        {
            escrow.Status = EscrowStatus.Held;
        }

        await _escrowRepository.UpsertAsync(escrow, ct).ConfigureAwait(false);

        pi.Status = PaymentIntentStatus.Succeeded;
        pi.UpdatedBy = pi.UserId;

        if (string.IsNullOrWhiteSpace(pi.ClientSecret))
        {
            pi.ClientSecret = providerRef;
        }

        await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<Result> ConfirmWalletTopUpViaVnPayAsync(PaymentIntent pi, string providerRef, CancellationToken ct)
    {
        if (pi.AmountCents <= 0)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Top-up amount must be positive."));
        }

        var txExists = await _transactionRepository.ExistsByProviderRefAsync(VnPayProvider, providerRef, ct).ConfigureAwait(false);
        if (txExists)
        {
            if (pi.Status != PaymentIntentStatus.Succeeded)
            {
                pi.Status = PaymentIntentStatus.Succeeded;
                pi.UpdatedBy = pi.UserId;
                await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
                await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return Result.Success();
        }

        await _walletRepository.CreateIfMissingAsync(pi.UserId, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        var wallet = await _walletRepository.GetByUserIdAsync(pi.UserId, ct).ConfigureAwait(false);
        if (wallet is null)
        {
            return Result.Failure(new Error(Error.Codes.Unexpected, "Wallet could not be loaded."));
        }

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            AmountCents = pi.AmountCents,
            Direction = TransactionDirection.In,
            Method = TransactionMethod.Gateway,
            Status = TransactionStatus.Succeeded,
            Provider = VnPayProvider,
            ProviderRef = providerRef,
            Metadata = CreateMetadata("WALLET_TOP_UP", null, null),
            CreatedBy = pi.UserId,
        };

        try
        {
            await _transactionRepository.CreateAsync(tx, ct).ConfigureAwait(false);
            await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            if (pi.Status != PaymentIntentStatus.Succeeded)
            {
                pi.Status = PaymentIntentStatus.Succeeded;
                pi.UpdatedBy = pi.UserId;
                await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
                await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return Result.Success();
        }

        var credited = await _walletRepository.AdjustBalanceAsync(pi.UserId, pi.AmountCents, ct).ConfigureAwait(false);
        if (!credited)
        {
            return Result.Failure(new Error(Error.Codes.Unexpected, "Failed to credit wallet."));
        }

        pi.Status = PaymentIntentStatus.Succeeded;
        pi.UpdatedBy = pi.UserId;

        if (string.IsNullOrWhiteSpace(pi.ClientSecret))
        {
            pi.ClientSecret = providerRef;
        }

        await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    private string? ResolveReturnUrl(string? requestedReturnUrl)
    {
        var defaultUrl = _vnPayConfig.ReturnUrl?.Trim();
        if (string.IsNullOrWhiteSpace(defaultUrl)) return null;

        if (string.IsNullOrWhiteSpace(requestedReturnUrl))
            return defaultUrl;

        var normalized = requestedReturnUrl.Trim();
        var allowed = new[]
        {
        defaultUrl,
        _vnPayConfig.ReturnUrlOrder?.Trim(),
        _vnPayConfig.ReturnUrlCustomDesign?.Trim(),
    };

        if (allowed.Any(u => !string.IsNullOrWhiteSpace(u) &&
                             string.Equals(u, normalized, StringComparison.OrdinalIgnoreCase)))
            return normalized;

        if (normalized.StartsWith("/", StringComparison.Ordinal) &&
            Uri.TryCreate(defaultUrl, UriKind.Absolute, out var baseUri))
            return new Uri(baseUri, normalized).ToString();

        return null;
    }

    private static JsonDocument CreateMetadata(string note, Guid? eventId, Guid? counterpartyUserId)
    {
        var payload = new Dictionary<string, object?>
        {
            ["note"] = note,
        };

        if (eventId.HasValue)
        {
            payload["eventId"] = eventId.Value;
        }

        if (counterpartyUserId.HasValue)
        {
            payload["counterpartyUserId"] = counterpartyUserId.Value;
        }

        return JsonSerializer.SerializeToDocument(payload);
    }

    private static bool IsWalletTopUpPurposeConstraintViolation(DbUpdateException exception)
    {
        if (exception is null)
        {
            return false;
        }

        var message = exception.InnerException?.Message ?? exception.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("chk_payment_intent_purpose_allowed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("payment_intent_purpose_allowed", StringComparison.OrdinalIgnoreCase);
    }
}
