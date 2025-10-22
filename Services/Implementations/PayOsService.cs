using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using BusinessObjects;
using BusinessObjects.Common;
using BusinessObjects.Common.Results;
using DTOs.Payments.PayOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repositories.Interfaces;
using Repositories.WorkSeeds.Extensions;
using Repositories.WorkSeeds.Interfaces;
using Services.Configuration;

namespace Services.Implementations;

public sealed class PayOsService : IPayOsService
{
    private const string Provider = "PAYOS";

    private readonly HttpClient _httpClient;
    private readonly PayOsConfig _config;
    private readonly ILogger<PayOsService> _logger;
    private readonly IGenericUnitOfWork _uow;
    private readonly IPaymentIntentRepository _paymentIntentRepository;
    private readonly IRegistrationQueryRepository _registrationQueryRepository;
    private readonly IRegistrationCommandRepository _registrationCommandRepository;
    private readonly IEventQueryRepository _eventQueryRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IEscrowRepository _escrowRepository;

    public PayOsService(
        HttpClient httpClient,
        IOptionsSnapshot<PayOsConfig> configOptions,
        ILogger<PayOsService> logger,
        IGenericUnitOfWork uow,
        IPaymentIntentRepository paymentIntentRepository,
        IRegistrationQueryRepository registrationQueryRepository,
        IRegistrationCommandRepository registrationCommandRepository,
        IEventQueryRepository eventQueryRepository,
        ITransactionRepository transactionRepository,
        IWalletRepository walletRepository,
        IEscrowRepository escrowRepository)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = configOptions?.Value ?? throw new ArgumentNullException(nameof(configOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _paymentIntentRepository = paymentIntentRepository ?? throw new ArgumentNullException(nameof(paymentIntentRepository));
        _registrationQueryRepository = registrationQueryRepository ?? throw new ArgumentNullException(nameof(registrationQueryRepository));
        _registrationCommandRepository = registrationCommandRepository ?? throw new ArgumentNullException(nameof(registrationCommandRepository));
        _eventQueryRepository = eventQueryRepository ?? throw new ArgumentNullException(nameof(eventQueryRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _escrowRepository = escrowRepository ?? throw new ArgumentNullException(nameof(escrowRepository));
    }

    public async Task<Result<string>> CreatePaymentLinkAsync(PayOsCreatePaymentRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        try
        {
            var requestPayload = new
            {
                orderCode = req.OrderCode,
                amount = req.Amount,
                currency = req.Currency,
                description = req.Description,
                returnUrl = string.IsNullOrWhiteSpace(req.ReturnUrl) ? _config.ReturnUrl : req.ReturnUrl,
                cancelUrl = string.IsNullOrWhiteSpace(req.CancelUrl) ? (_config.CancelUrl ?? req.ReturnUrl ?? _config.ReturnUrl) : req.CancelUrl,
                buyerName = req.BuyerName,
                buyerEmail = req.BuyerEmail,
                buyerPhone = req.BuyerPhone
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildPaymentsEndpoint())
            {
                Content = JsonContent.Create(requestPayload)
            };

            if (!string.IsNullOrWhiteSpace(_config.ClientId))
            {
                request.Headers.TryAddWithoutValidation("x-client-id", _config.ClientId);
            }

            if (!string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                request.Headers.TryAddWithoutValidation("x-api-key", _config.ApiKey);
            }

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayOS create payment link failed with status {StatusCode}. Body={Body}", response.StatusCode, responseBody);
                return Result<string>.Failure(new Error(Error.Codes.Unexpected, "Failed to create PayOS payment link."));
            }

            PayOsPaymentResponse? parsed = null;
            try
            {
                parsed = JsonSerializer.Deserialize<PayOsPaymentResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize PayOS response for OrderCode={OrderCode}", req.OrderCode);
            }

            var checkoutUrl = parsed?.Data?.CheckoutUrl;
            if (string.IsNullOrWhiteSpace(checkoutUrl))
            {
                _logger.LogWarning("PayOS response missing checkoutUrl for OrderCode={OrderCode}. Body={Body}", req.OrderCode, responseBody);
                return Result<string>.Failure(new Error(Error.Codes.Unexpected, "PayOS response missing checkout url."));
            }

            _logger.LogInformation("Created PayOS payment link for OrderCode={OrderCode}", req.OrderCode);
            return Result<string>.Success(checkoutUrl);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error when creating PayOS payment link for OrderCode={OrderCode}", req.OrderCode);
            return Result<string>.Failure(new Error(Error.Codes.Unexpected, "Unexpected error while creating PayOS payment link."));
        }
    }

    public async Task<Result> HandleWebhookAsync(PayOsWebhookPayload payload, string rawBody, string? signatureHeader, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Missing PayOS signature."));
        }

        if (!VerifyChecksum(rawBody, signatureHeader))
        {
            _logger.LogWarning("Invalid PayOS signature for OrderCode={OrderCode}", payload.Data?.OrderCode);
            return Result.Failure(new Error(Error.Codes.Forbidden, "Invalid signature."));
        }

        if (payload.Data is null || string.IsNullOrWhiteSpace(payload.Data.OrderCode))
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Webhook missing order code."));
        }

        if (!Guid.TryParse(payload.Data.OrderCode, out var paymentIntentId))
        {
            if (!Guid.TryParseExact(payload.Data.OrderCode, "N", out paymentIntentId))
            {
                _logger.LogWarning("PayOS webhook order code not a valid GUID: {OrderCode}", payload.Data.OrderCode);
                return Result.Failure(new Error(Error.Codes.Validation, "Invalid order code."));
            }
        }

        var providerRef = payload.Data.TransactionId ?? payload.Data.PaymentLinkId ?? payload.Data.OrderCode;
        var status = payload.Data.Status?.Trim() ?? string.Empty;

        return await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var pi = await _paymentIntentRepository.GetByIdAsync(paymentIntentId, innerCt).ConfigureAwait(false);
            if (pi is null)
            {
                return Result.Failure(new Error(Error.Codes.NotFound, "Payment intent not found."));
            }

            if (payload.Data.Amount != pi.AmountCents)
            {
                _logger.LogWarning(
                    "PayOS amount mismatch for PaymentIntent={PaymentIntentId}. Expected={Expected} Actual={Actual}",
                    paymentIntentId,
                    pi.AmountCents,
                    payload.Data.Amount);
                return Result.Failure(new Error(Error.Codes.Validation, "Amount mismatch."));
            }

            if (!IsSuccessStatus(status))
            {
                pi.Status = PaymentIntentStatus.Canceled;
                pi.UpdatedBy = pi.UserId;
                await _paymentIntentRepository.UpdateAsync(pi, innerCt).ConfigureAwait(false);
                await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
                _logger.LogInformation("Marked PaymentIntent={PaymentIntentId} as canceled due to PayOS status={Status}", paymentIntentId, status);
                return Result.Success();
            }

            return pi.Purpose switch
            {
                PaymentPurpose.EventTicket => await ConfirmEventTicketAsync(pi, providerRef, innerCt).ConfigureAwait(false),
                PaymentPurpose.TopUp => await ConfirmEscrowTopUpAsync(pi, providerRef, innerCt).ConfigureAwait(false),
                PaymentPurpose.WalletTopUp => await ConfirmWalletTopUpAsync(pi, providerRef, innerCt).ConfigureAwait(false),
                _ => Result.Failure(new Error(Error.Codes.Validation, "Unsupported payment purpose."))
            };
        }, ct: ct).ConfigureAwait(false);
    }

    public bool VerifyChecksum(string rawBody, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_config.SecretKey))
        {
            _logger.LogWarning("PayOS secret key is not configured.");
            return false;
        }

        var normalizedSignature = signatureHeader.Trim();
        var payloadBytes = Encoding.UTF8.GetBytes(rawBody ?? string.Empty);
        var keyBytes = Encoding.UTF8.GetBytes(_config.SecretKey);

        using var hmac = new HMACSHA256(keyBytes);
        var computedBytes = hmac.ComputeHash(payloadBytes);
        var computedSignature = Convert.ToHexString(computedBytes).ToLowerInvariant();

        return string.Equals(computedSignature, normalizedSignature, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildPaymentsEndpoint()
    {
        var baseUrl = string.IsNullOrWhiteSpace(_config.BaseUrl) ? "https://api.payos.vn/v1" : _config.BaseUrl;
        return baseUrl.TrimEnd('/') + "/payments";
    }

    private static bool IsSuccessStatus(string status)
    {
        return status.Equals("PAID", StringComparison.OrdinalIgnoreCase)
               || status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)
               || status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Result> ConfirmEventTicketAsync(PaymentIntent pi, string providerRef, CancellationToken ct)
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
            pi.UpdatedBy = pi.UserId;
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

        var txExists = await _transactionRepository.ExistsByProviderRefAsync(Provider, providerRef, ct).ConfigureAwait(false);
        if (txExists)
        {
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

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            EventId = ev.Id,
            AmountCents = pi.AmountCents,
            Direction = TransactionDirection.Out,
            Method = TransactionMethod.Gateway,
            Status = TransactionStatus.Succeeded,
            Provider = Provider,
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

        await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<Result> ConfirmEscrowTopUpAsync(PaymentIntent pi, string providerRef, CancellationToken ct)
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

        var txExists = await _transactionRepository.ExistsByProviderRefAsync(Provider, providerRef, ct).ConfigureAwait(false);
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

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            EventId = ev.Id,
            AmountCents = pi.AmountCents,
            Direction = TransactionDirection.In,
            Method = TransactionMethod.Gateway,
            Status = TransactionStatus.Succeeded,
            Provider = Provider,
            ProviderRef = providerRef,
            Metadata = CreateMetadata("EVENT_ESCROW_TOP_UP", ev.Id, null),
            CreatedBy = pi.UserId,
        };

        await _transactionRepository.CreateAsync(tx, ct).ConfigureAwait(false);

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

        await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<Result> ConfirmWalletTopUpAsync(PaymentIntent pi, string providerRef, CancellationToken ct)
    {
        if (pi.AmountCents <= 0)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Top-up amount must be positive."));
        }

        var txExists = await _transactionRepository.ExistsByProviderRefAsync(Provider, providerRef, ct).ConfigureAwait(false);
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
            Provider = Provider,
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
            _logger.LogWarning(ex, "Duplicate PayOS transaction detected for ProviderRef={ProviderRef}", providerRef);
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

        await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
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
}
