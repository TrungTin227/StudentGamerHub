using DTOs.Payments.PayOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Application.Quests;
using Services.Common.Emailing.Interfaces;
using Services.Configuration;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;

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
    private readonly IMembershipPlanRepository _membershipPlanRepository;
    private readonly IMembershipEnrollmentService _membershipEnrollmentService;
    private readonly IPayOsService _payOsService;
    private readonly BillingOptions _billingOptions;
    private readonly PayOsOptions _payOsOptions;
    private readonly IQuestService _questService;
    private readonly ICommunityService _communityService;
    private readonly IPlatformAccountService _platformAccountService;
    private readonly UserManager<User> _userManager;
    private readonly IEmailQueue _emailQueue;
    private readonly IMembershipEmailFactory _membershipEmailFactory;
    private readonly IUserMembershipRepository _userMembershipRepository;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IGenericUnitOfWork uow,
        IPaymentIntentRepository paymentIntentRepository,
        IRegistrationQueryRepository registrationQueryRepository,
        IRegistrationCommandRepository registrationCommandRepository,
        IEventQueryRepository eventQueryRepository,
        ITransactionRepository transactionRepository,
        IWalletRepository walletRepository,
        IMembershipPlanRepository membershipPlanRepository,
        IMembershipEnrollmentService membershipEnrollmentService,
        IPayOsService payOsService,
        IOptionsSnapshot<BillingOptions> billingOptions,
        IOptionsSnapshot<PayOsOptions> payOsOptions,
        IQuestService questService,
        ICommunityService communityService,
        IPlatformAccountService platformAccountService,
        UserManager<User> userManager,
        IEmailQueue emailQueue,
        IMembershipEmailFactory membershipEmailFactory,
        IUserMembershipRepository userMembershipRepository,
        ILogger<PaymentService> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _paymentIntentRepository = paymentIntentRepository ?? throw new ArgumentNullException(nameof(paymentIntentRepository));
        _registrationQueryRepository = registrationQueryRepository ?? throw new ArgumentNullException(nameof(registrationQueryRepository));
        _registrationCommandRepository = registrationCommandRepository ?? throw new ArgumentNullException(nameof(registrationCommandRepository));
        _eventQueryRepository = eventQueryRepository ?? throw new ArgumentNullException(nameof(eventQueryRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _membershipPlanRepository = membershipPlanRepository ?? throw new ArgumentNullException(nameof(membershipPlanRepository));
        _membershipEnrollmentService = membershipEnrollmentService ?? throw new ArgumentNullException(nameof(membershipEnrollmentService));
        _payOsService = payOsService ?? throw new ArgumentNullException(nameof(payOsService));
        _billingOptions = billingOptions?.Value ?? throw new ArgumentNullException(nameof(billingOptions));
        _payOsOptions = payOsOptions?.Value ?? throw new ArgumentNullException(nameof(payOsOptions));
        _questService = questService ?? throw new ArgumentNullException(nameof(questService));
        _communityService = communityService ?? throw new ArgumentNullException(nameof(communityService));
        _platformAccountService = platformAccountService ?? throw new ArgumentNullException(nameof(platformAccountService));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _emailQueue = emailQueue ?? throw new ArgumentNullException(nameof(emailQueue));
        _membershipEmailFactory = membershipEmailFactory ?? throw new ArgumentNullException(nameof(membershipEmailFactory));
        _userMembershipRepository = userMembershipRepository ?? throw new ArgumentNullException(nameof(userMembershipRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Result<MembershipPurchaseResultDto>> BuyMembershipAsync(Guid userId, Guid planId, CancellationToken ct = default)
    {
        return _uow.ExecuteTransactionAsync<MembershipPurchaseResultDto>(async innerCt =>
        {
            var plan = await _membershipPlanRepository
                .GetByIdAsync(planId, innerCt)
                .ConfigureAwait(false);

            if (plan is null)
            {
                return Result<MembershipPurchaseResultDto>.Failure(new Error(Error.Codes.NotFound, "Membership plan not found."));
            }

            if (!plan.IsActive)
            {
                return Result<MembershipPurchaseResultDto>.Failure(new Error(Error.Codes.Conflict, "Membership plan is inactive."));
            }

            var wallet = await _walletRepository
                .EnsureAsync(userId, innerCt)
                .ConfigureAwait(false);

            var priceCents = ConvertPriceToCents(plan.Price);

            if (priceCents <= 0)
            {
                var membershipInfo = await _membershipEnrollmentService
                    .AssignAsync(userId, plan, userId, innerCt)
                    .ConfigureAwait(false);

                await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

                // Send membership confirmation email for free membership
                await SendMembershipConfirmationEmailAsync(userId, plan, innerCt).ConfigureAwait(false);

                var successDto = new MembershipPurchaseResultDto(false, null, membershipInfo);
                return Result<MembershipPurchaseResultDto>.Success(successDto);
            }

            if (wallet.BalanceCents >= priceCents)
            {
                var ensurePlatformUser = await _platformAccountService
                    .GetOrCreatePlatformUserIdAsync(innerCt)
                    .ConfigureAwait(false);

                if (ensurePlatformUser.IsFailure)
                {
                    return Result<MembershipPurchaseResultDto>.Failure(ensurePlatformUser.Error);
                }

                var platformUserId = ensurePlatformUser.Value;

                var debited = await _walletRepository
                    .AdjustBalanceAsync(userId, -priceCents, innerCt)
                    .ConfigureAwait(false);
                if (!debited)
                {
                    return Result<MembershipPurchaseResultDto>.Failure(new Error(Error.Codes.Forbidden, "Insufficient wallet balance."));
                }

                var credited = await _walletRepository
                    .AdjustBalanceAsync(platformUserId, priceCents, innerCt)
                    .ConfigureAwait(false);
                if (!credited)
                {
                    return Result<MembershipPurchaseResultDto>.Failure(new Error(Error.Codes.Unexpected, "Failed to credit platform wallet."));
                }

                var platformWallet = await _walletRepository
                    .EnsureAsync(platformUserId, innerCt)
                    .ConfigureAwait(false);

                var outTx = new Transaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    AmountCents = priceCents,
                    Direction = TransactionDirection.Out,
                    Method = TransactionMethod.Wallet,
                    Status = TransactionStatus.Succeeded,
                    Provider = LocalProvider,
                    Metadata = CreateMembershipMetadata(plan.Id, plan.Name),
                    CreatedBy = userId,
                };

                var inTx = new Transaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = platformWallet.Id,
                    AmountCents = priceCents,
                    Direction = TransactionDirection.In,
                    Method = TransactionMethod.Wallet,
                    Status = TransactionStatus.Succeeded,
                    Provider = LocalProvider,
                    Metadata = CreateMembershipMetadata(plan.Id, plan.Name),
                    CreatedBy = platformUserId,
                };

                await _transactionRepository.CreateAsync(outTx, innerCt).ConfigureAwait(false);
                await _transactionRepository.CreateAsync(inTx, innerCt).ConfigureAwait(false);

                var membershipInfo = await _membershipEnrollmentService
                    .AssignAsync(userId, plan, userId, innerCt)
                    .ConfigureAwait(false);

                await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

                // Send membership confirmation email for wallet payment
                await SendMembershipConfirmationEmailAsync(userId, plan, innerCt).ConfigureAwait(false);

                var successDto = new MembershipPurchaseResultDto(false, null, membershipInfo);
                return Result<MembershipPurchaseResultDto>.Success(successDto);
            }

            var now = DateTime.UtcNow;
            var pendingIntent = new PaymentIntent
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AmountCents = priceCents,
                Purpose = PaymentPurpose.Membership,
                MembershipPlanId = plan.Id,
                Status = PaymentIntentStatus.RequiresPayment,
                ClientSecret = Guid.NewGuid().ToString("N"),
                ExpiresAt = now.AddMinutes(15),
                CreatedAtUtc = now,
                CreatedBy = userId,
            };

            await _paymentIntentRepository.CreateAsync(pendingIntent, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            var dto = new MembershipPurchaseResultDto(true, pendingIntent.Id, null);
            return Result<MembershipPurchaseResultDto>.Success(dto);
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

            // Ensure wallet exists before creating top-up intent - maintains one-wallet-per-user invariant
            await _walletRepository.EnsureAsync(userId, innerCt).ConfigureAwait(false);

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
                PaymentPurpose.TopUp => Result.Failure(new Error(Error.Codes.Validation, "Event escrow top-ups are no longer supported.")),
                PaymentPurpose.Membership => Result.Failure(new Error(Error.Codes.Validation, "Membership purchases are settled immediately and do not require manual confirmation.")),
                PaymentPurpose.WalletTopUp => Result.Failure(new Error(Error.Codes.Validation, "WalletTopUpRequiresProviderCallback")),
                _ => Result.Failure(new Error(Error.Codes.Validation, "Unsupported payment purpose.")),
            };
        }, ct: ct);
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
            await TriggerAttendQuestAsync(registration.UserId, registration.EventId, ct).ConfigureAwait(false);
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

        var wallet = await _walletRepository.EnsureAsync(userId, ct).ConfigureAwait(false);

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

        await TriggerAttendQuestAsync(registration.UserId, registration.EventId, ct).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<string>> CreateHostedCheckoutUrlAsync(Guid userId, Guid paymentIntentId, string? returnUrl, string clientIp, CancellationToken ct = default)
    {
        _ = clientIp;

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

            var resolvedReturnUrl = ResolveReturnUrl(returnUrl);
            if (string.IsNullOrWhiteSpace(resolvedReturnUrl))
            {
                return Result<string>.Failure(new Error(Error.Codes.Validation, "Return URL is not allowed."));
            }

            var cancelUrl = ResolveCancelUrl(returnUrl) ?? resolvedReturnUrl;
            var orderCode = await EnsureOrderCodeAsync(pi, innerCt).ConfigureAwait(false);
            var description = BuildDescription(pi, orderCode);

            var request = new PayOsCreatePaymentRequest
            {
                OrderCode = orderCode,         // long ?
                Amount = pi.AmountCents,
                Description = description,
                ReturnUrl = resolvedReturnUrl,
                CancelUrl = cancelUrl,
            };

            var linkResult = await _payOsService.CreatePaymentLinkAsync(request, innerCt).ConfigureAwait(false);
            if (linkResult.IsFailure)
            {
                return Result<string>.Failure(linkResult.Error);
            }

            pi.ClientSecret = linkResult.Value!;
            pi.UpdatedBy = userId;

            await _paymentIntentRepository.UpdateAsync(pi, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            return Result<string>.Success(linkResult.Value!);
        }, ct: ct);
    }
    private async Task<long> EnsureOrderCodeAsync(PaymentIntent pi, CancellationToken ct)
    {
        if (pi.OrderCode.HasValue && pi.OrderCode.Value > 0)
        {
            return pi.OrderCode.Value;
        }

        for (var i = 0; i < 5; i++)
        {
            var candidate = NewOrderCode();
            if (await _paymentIntentRepository.TrySetOrderCodeAsync(pi.Id, candidate, ct).ConfigureAwait(false))
            {
                pi.OrderCode = candidate;
                return candidate;
            }
        }

        var refreshed = await _paymentIntentRepository.GetByIdAsync(pi.Id, ct).ConfigureAwait(false);
        if (refreshed?.OrderCode is long existing && existing > 0)
        {
            pi.OrderCode = existing;
            return existing;
        }

        throw new InvalidOperationException("Could not reserve unique order code.");
    }

    private static long NewOrderCode()
    {
        var ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000_000_000L; // 12 digits
        var rnd = RandomNumberGenerator.GetInt32(100, 1000); // 3 digits
        return ms * 1000 + rnd; // 15 digits
    }

    private string? ResolveReturnUrl(string? requestedReturnUrl)
    {
        if (!string.IsNullOrWhiteSpace(requestedReturnUrl))
        {
            var normalized = requestedReturnUrl.Trim();
            if (Uri.TryCreate(normalized, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            var frontendResolved = BuildFrontendUrl(normalized);
            if (!string.IsNullOrWhiteSpace(frontendResolved))
            {
                return frontendResolved;
            }
        }

        if (!string.IsNullOrWhiteSpace(_payOsOptions.FrontendBaseUrl))
        {
            var frontendDefault = BuildFrontendUrl("/payment/result");
            if (!string.IsNullOrWhiteSpace(frontendDefault))
            {
                return frontendDefault;
            }
        }

        if (!string.IsNullOrWhiteSpace(_payOsOptions.ReturnUrl))
        {
            return _payOsOptions.ReturnUrl.Trim();
        }

        return null;
    }

    private string? ResolveCancelUrl(string? requestedReturnUrl)
    {
        if (!string.IsNullOrWhiteSpace(_payOsOptions.CancelUrl))
        {
            return _payOsOptions.CancelUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(requestedReturnUrl))
        {
            var normalized = requestedReturnUrl.Trim();
            if (Uri.TryCreate(normalized, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            var frontendResolved = BuildFrontendUrl(normalized);
            if (!string.IsNullOrWhiteSpace(frontendResolved))
            {
                return frontendResolved;
            }
        }

        var fallback = BuildFrontendUrl("/payment/result");
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        return ResolveReturnUrl(null);
    }

    private string? BuildFrontendUrl(string pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(_payOsOptions.FrontendBaseUrl))
        {
            return null;
        }

        var baseValue = _payOsOptions.FrontendBaseUrl.Trim();
        if (!Uri.TryCreate(baseValue, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        var relative = pathOrUrl.StartsWith("/", StringComparison.Ordinal)
            ? pathOrUrl
            : "/" + pathOrUrl;

        return new Uri(baseUri, relative).ToString();
    }

    private static string BuildDescription(PaymentIntent intent, long orderCode)
    {
        var prefix = intent.Purpose switch
        {
            PaymentPurpose.EventTicket => "Ticket",
            PaymentPurpose.TopUp => "EscrowTop",
            PaymentPurpose.Membership => "Membership",
            PaymentPurpose.WalletTopUp => "WalletTop",
            _ => "Payment"
        };

        var suffix = Math.Abs(orderCode % 1_000_000);
        var description = $"{prefix}#{suffix:D6}";

        return description.Length <= 25
            ? description
            : description[..25];
    }

    private async Task<Result> EnsureCommunityMembershipAsync(Guid? communityId, Guid userId, CancellationToken ct)
    {
        if (!communityId.HasValue)
        {
            return Result.Success();
        }

        var joinResult = await _communityService.JoinCommunityAsync(communityId.Value, userId, ct).ConfigureAwait(false);
        return joinResult.IsSuccess
            ? Result.Success()
            : Result.Failure(joinResult.Error);
    }

    private async Task TriggerAttendQuestAsync(Guid userId, Guid eventId, CancellationToken ct)
    {
        try
        {
            var questResult = await _questService.MarkAttendEventAsync(userId, eventId, ct).ConfigureAwait(false);
            _ = questResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Best-effort: quest completion failures should not block payment confirmation.
        }
    }

    private async Task SendMembershipConfirmationEmailAsync(Guid userId, MembershipPlan plan, CancellationToken ct)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
            if (user is not null && !string.IsNullOrWhiteSpace(user.Email))
            {
                var membership = await _userMembershipRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
                if (membership is not null)
                {
                    var emailMessage = _membershipEmailFactory.BuildMembershipPurchaseConfirmation(user, plan, membership);
                    await _emailQueue.EnqueueAsync(emailMessage, ct).ConfigureAwait(false);
                    _logger.LogInformation("Membership confirmation email queued for user {UserId}, plan {PlanName}", userId, plan.Name);
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail the transaction if email fails
            _logger.LogWarning(ex, "Failed to send membership confirmation email for user {UserId}", userId);
        }
    }

    private static long ConvertPriceToCents(decimal price)
    {
        if (price <= 0)
        {
            return 0;
        }

        return (long)Math.Round(price, MidpointRounding.AwayFromZero);
    }

    private static JsonDocument CreateMembershipMetadata(Guid planId, string planName)
    {
        var payload = new Dictionary<string, object?>
        {
            ["note"] = "MEMBERSHIP_PURCHASE",
            ["membershipPlanId"] = planId,
            ["planName"] = planName
        };

        return JsonSerializer.SerializeToDocument(payload);
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


























