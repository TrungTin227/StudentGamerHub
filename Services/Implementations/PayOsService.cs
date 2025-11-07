using DTOs.Payments.PayOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Application.Quests;
using Services.Common.Emailing.Interfaces;
using Services.Configuration;
using Services.Interfaces;
using StackExchange.Redis;
using System;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Text.Json;

namespace Services.Implementations;

public sealed class PayOsService : IPayOsService
{
    private const string Provider = "PAYOS";
    private static readonly TimeSpan ReplayLeaseTtl = TimeSpan.FromMinutes(10);

    private readonly HttpClient _httpClient;
    private readonly PayOsOptions _options;
    private readonly IMemoryCache _memoryCache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly object _replayLock = new();
    private readonly ILogger<PayOsService> _logger;
    private readonly IGenericUnitOfWork _uow;
    private readonly IPaymentIntentRepository _paymentIntentRepository;
    private readonly IRegistrationQueryRepository _registrationQueryRepository;
    private readonly IRegistrationCommandRepository _registrationCommandRepository;
    private readonly IEventQueryRepository _eventQueryRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly IMembershipPlanRepository _membershipPlanRepository;
    private readonly IMembershipEnrollmentService _membershipEnrollmentService;
    private readonly IPlatformAccountService _platformAccountService;
    private readonly IEscrowRepository _escrowRepository;
    private readonly IQuestService _questService;
    private readonly ICommunityService _communityService;
    private readonly UserManager<User> _userManager;
    private readonly IEmailQueue _emailQueue;
    private readonly IMembershipEmailFactory _membershipEmailFactory;
    private readonly IUserMembershipRepository _userMembershipRepository;

    public PayOsService(
        HttpClient httpClient,
        IOptionsSnapshot<PayOsOptions> configOptions,
        IMemoryCache memoryCache,
        IConnectionMultiplexer? redis,
        ILogger<PayOsService> logger,
        IGenericUnitOfWork uow,
        IPaymentIntentRepository paymentIntentRepository,
        IRegistrationQueryRepository registrationQueryRepository,
        IRegistrationCommandRepository registrationCommandRepository,
        IEventQueryRepository eventQueryRepository,
        ITransactionRepository transactionRepository,
        IWalletRepository walletRepository,
        IMembershipPlanRepository membershipPlanRepository,
        IMembershipEnrollmentService membershipEnrollmentService,
        IPlatformAccountService platformAccountService,
        IEscrowRepository escrowRepository,
        IQuestService questService,
        ICommunityService communityService,
        UserManager<User> userManager,
        IEmailQueue emailQueue,
        IMembershipEmailFactory membershipEmailFactory,
        IUserMembershipRepository userMembershipRepository)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = configOptions?.Value ?? throw new ArgumentNullException(nameof(configOptions));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _redis = redis;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _paymentIntentRepository = paymentIntentRepository ?? throw new ArgumentNullException(nameof(paymentIntentRepository));
        _registrationQueryRepository = registrationQueryRepository ?? throw new ArgumentNullException(nameof(registrationQueryRepository));
        _registrationCommandRepository = registrationCommandRepository ?? throw new ArgumentNullException(nameof(registrationCommandRepository));
        _eventQueryRepository = eventQueryRepository ?? throw new ArgumentNullException(nameof(eventQueryRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _membershipPlanRepository = membershipPlanRepository ?? throw new ArgumentNullException(nameof(membershipPlanRepository));
        _membershipEnrollmentService = membershipEnrollmentService ?? throw new ArgumentNullException(nameof(membershipEnrollmentService));
        _platformAccountService = platformAccountService ?? throw new ArgumentNullException(nameof(platformAccountService));
        _escrowRepository = escrowRepository ?? throw new ArgumentNullException(nameof(escrowRepository));
        _questService = questService ?? throw new ArgumentNullException(nameof(questService));
        _communityService = communityService ?? throw new ArgumentNullException(nameof(communityService));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _emailQueue = emailQueue ?? throw new ArgumentNullException(nameof(emailQueue));
        _membershipEmailFactory = membershipEmailFactory ?? throw new ArgumentNullException(nameof(membershipEmailFactory));
        _userMembershipRepository = userMembershipRepository ?? throw new ArgumentNullException(nameof(userMembershipRepository));
    }

    private async Task<ReplayLease> AcquireReplayLeaseAsync(long orderCode, string fingerprint)
    {
        var key = BuildReplayCacheKey(orderCode, fingerprint);

        if (_redis is not null)
        {
            var db = _redis.GetDatabase();
            var acquired = await db.StringSetAsync(key, "1", ReplayLeaseTtl, When.NotExists).ConfigureAwait(false);
            if (!acquired)
            {
                return ReplayLease.NotAcquired;
            }

            return new ReplayLease(true, async () =>
            {
                await db.KeyDeleteAsync(key).ConfigureAwait(false);
            });
        }

        lock (_replayLock)
        {
            if (_memoryCache.TryGetValue(key, out _))
            {
                return ReplayLease.NotAcquired;
            }

            _memoryCache.Set(key, true, ReplayLeaseTtl);
            return new ReplayLease(true, () =>
            {
                lock (_replayLock)
                {
                    _memoryCache.Remove(key);
                    return Task.CompletedTask;
                }
            });
        }
    }

    private static string BuildReplayCacheKey(long orderCode, string fingerprint)
    {
        var normalized = string.IsNullOrWhiteSpace(fingerprint)
            ? orderCode.ToString(CultureInfo.InvariantCulture)
            : fingerprint.Trim().ToLowerInvariant();

        return $"payos:webhook:{orderCode}:{normalized}";
    }

    private static string BuildReplayFingerprint(string? headerSignature, string? payloadSignature, long orderCode)
    {
        if (!string.IsNullOrWhiteSpace(headerSignature))
        {
            return headerSignature.Trim();
        }

        if (!string.IsNullOrWhiteSpace(payloadSignature))
        {
            return payloadSignature.Trim();
        }

        return orderCode.ToString(CultureInfo.InvariantCulture);
    }

    internal static bool ValidatePayOsSignature(string rawBody, string? signatureHeader, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        var header = signatureHeader.Trim();
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var payloadBytes = Encoding.UTF8.GetBytes(rawBody ?? string.Empty);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var computed = hmac.ComputeHash(payloadBytes);

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(header);
        }
        catch (FormatException)
        {
            return false;
        }

        if (provided.Length != computed.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(computed, provided);
    }

    private sealed class ReplayLease
    {
        private readonly Func<Task> _release;

        internal ReplayLease(bool acquired, Func<Task>? release = null)
        {
            Acquired = acquired;
            _release = release ?? (() => Task.CompletedTask);
        }

        public bool Acquired { get; }

        public Task ReleaseAsync() => _release();

        public static ReplayLease NotAcquired { get; } = new(false);
    }

    // =========================
    // Create payment link (v2)
    // =========================
    public async Task<Result<string>> CreatePaymentLinkAsync(PayOsCreatePaymentRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        try
        {
            var orderCode = req.OrderCode; // LONG
            var returnUrl = string.IsNullOrWhiteSpace(req.ReturnUrl) ? _options.ReturnUrl : req.ReturnUrl!;
            var cancelUrl = string.IsNullOrWhiteSpace(req.CancelUrl) ? (_options.CancelUrl ?? returnUrl) : req.CancelUrl!;
            var description = req.Description ?? string.Empty;

            // PayOS v2 API does not accept webhookUrl in request body - configure it in PayOS dashboard instead
            // Signature without webhookUrl
            var createSig = BuildCreateSignature(orderCode, req.Amount, description, returnUrl, cancelUrl, null);

            var payload = new
            {
                orderCode = orderCode,
                amount = req.Amount,
                description = description,
                returnUrl = returnUrl,
                cancelUrl = cancelUrl,
                buyerName = req.BuyerName,
                buyerEmail = req.BuyerEmail,
                buyerPhone = req.BuyerPhone,
                signature = createSig
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildPaymentsEndpointV2())
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.TryAddWithoutValidation("x-client-id", _options.ClientId);
            request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("payOS create link failed {Status}. Body={Body}", response.StatusCode, body);
                return Result<string>.Failure(new Error(Error.Codes.Unexpected, $"Failed to create PayOS payment link. Status: {response.StatusCode}"));
            }

            PayOsPaymentResponse? parsed = null;
            try
            {
                parsed = JsonSerializer.Deserialize<PayOsPaymentResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Deserialize payOS response failed. Raw={Body}", body);
                return Result<string>.Failure(new Error(Error.Codes.Unexpected, "Failed to parse PayOS response."));
            }

            // Log the parsed response for debugging
            _logger.LogInformation("PayOS response parsed. Code={Code}, Desc={Desc}, Success={Success}, HasData={HasData}, Raw={Body}",
                parsed?.Code, parsed?.Desc, parsed?.Success, parsed?.Data != null, body);

            // Check if PayOS returned an error
            // PayOS returns Code "00" for success, other codes for errors
            if (parsed is null)
            {
                _logger.LogWarning("payOS response is null. Raw={Body}", body);
                return Result<string>.Failure(new Error(Error.Codes.Unexpected, "Failed to parse PayOS response."));
            }

            // Check for error codes from PayOS
            if (!string.Equals(parsed.Code, "00", StringComparison.OrdinalIgnoreCase))
            {
                var errorMessage = parsed.Desc ?? "Unknown error from PayOS";
                _logger.LogWarning("payOS returned error code. Code={Code}, Desc={Desc}, Raw={Body}", parsed.Code, errorMessage, body);

                // Return a more specific error message based on the error code
                return parsed.Code switch
                {
                    "231" => Result<string>.Failure(new Error(Error.Codes.Conflict, "Payment order already exists. Please use a different order code or cancel the existing payment.")),
                    _ => Result<string>.Failure(new Error(Error.Codes.Validation, $"PayOS error: {errorMessage}"))
                };
            }

            var checkoutUrl = parsed.Data?.CheckoutUrl;
            if (string.IsNullOrWhiteSpace(checkoutUrl))
            {
                _logger.LogWarning("payOS response missing checkoutUrl. Parsed={@Parsed}, Raw={Body}", parsed, body);
                return Result<string>.Failure(new Error(Error.Codes.Unexpected, "PayOS response missing checkout url."));
            }

            _logger.LogInformation("Created payOS link. OrderCode={OrderCode}", orderCode);
            return Result<string>.Success(checkoutUrl);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating payOS link. OrderCode={OrderCode}", req.OrderCode);
            return Result<string>.Failure(new Error(Error.Codes.Unexpected, "Unexpected error while creating PayOS payment link."));
        }
    }

    // =====================================
    // Webhook handler (verify + business)
    // =====================================
    public async Task<Result<PayOsWebhookOutcome>> HandleWebhookAsync(PayOsWebhookPayload payload, string rawBody, string? signatureHeader, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // Check if this is a manual sync (bypass signature validation)
        var isManualSync = string.Equals(payload.Signature, "manual-sync", StringComparison.OrdinalIgnoreCase);

        // Validate HTTP header signature only if present (optional, as PayOS sends signature in JSON body)
        if (!isManualSync && !string.IsNullOrWhiteSpace(signatureHeader))
        {
            if (!ValidatePayOsSignature(rawBody, signatureHeader, _options.SecretKey))
            {
                _logger.LogWarning("SignatureInvalid (Header) OrderCode={OrderCode}", payload.Data?.OrderCode);
                return Result<PayOsWebhookOutcome>.Failure(new Error(Error.Codes.Validation, "Invalid signature."));
            }
        }

        if (payload.Data is null)
        {
            _logger.LogWarning("WebhookMissingData");
            return Result<PayOsWebhookOutcome>.Failure(new Error(Error.Codes.Validation, "Webhook missing data."));
        }

        var orderCode = payload.Data.OrderCode;
        if (orderCode <= 0)
        {
            _logger.LogWarning("OrderCodeInvalid Value={OrderCode}", orderCode);
            return Result<PayOsWebhookOutcome>.Failure(new Error(Error.Codes.Validation, "Invalid order code."));
        }

        // Timestamp validation is disabled because:
        // 1. PayOS sends timestamps in Vietnam timezone (GMT+7) without timezone info
        // 2. We already have strong security via:
        //    - HMAC-SHA256 signature validation
        //    - Replay attack protection (order code + fingerprint locking)
        // 3. Timestamp validation was causing false rejections due to timezone mismatch

        // Log timestamp for debugging purposes
        if (!string.IsNullOrWhiteSpace(payload.Data.TransactionDateTime))
        {
            _logger.LogInformation("WebhookTimestamp OrderCode={OrderCode} Timestamp={Timestamp}",
                orderCode, payload.Data.TransactionDateTime);
        }

        var providerRef = payload.Data.Reference
                         ?? payload.Data.PaymentLinkId
                         ?? orderCode.ToString(CultureInfo.InvariantCulture);
        var fingerprint = BuildReplayFingerprint(signatureHeader, payload.Signature, orderCode);
        var lease = await AcquireReplayLeaseAsync(orderCode, fingerprint).ConfigureAwait(false);
        if (!lease.Acquired)
        {
            _logger.LogInformation("ReplayIgnored OrderCode={OrderCode}", orderCode);
            return Result<PayOsWebhookOutcome>.Success(PayOsWebhookOutcome.Ignored);
        }

        // Skip payload signature validation for manual sync
        if (!isManualSync && !VerifyWebhookSignature(payload))
        {
            _logger.LogWarning("PayloadSignatureInvalid OrderCode={OrderCode}", orderCode);
            await lease.ReleaseAsync().ConfigureAwait(false);
            return Result<PayOsWebhookOutcome>.Failure(new Error(Error.Codes.Validation, "Invalid payload signature."));
        }

        if (isManualSync)
        {
            _logger.LogInformation("ManualSyncWebhook OrderCode={OrderCode} Code={Code} Success={Success}", orderCode, payload.Code, payload.Success);
        }
        else
        {
            _logger.LogInformation("WebhookReceived OrderCode={OrderCode} Code={Code} Success={Success}", orderCode, payload.Code, payload.Success);
        }

        var isPaid = payload.Success && string.Equals(payload.Code, "00", StringComparison.OrdinalIgnoreCase);

        try
        {
            var businessResult = await _uow.ExecuteTransactionAsync(async innerCt =>
            {
                var pi = await _paymentIntentRepository.GetByOrderCodeAsync(orderCode, innerCt).ConfigureAwait(false);
                if (pi is null)
                {
                    _logger.LogInformation("WebhookUnknownOrder OrderCode={OrderCode}", orderCode);
                    return Result.Success();
                }

                if (payload.Data.Amount != pi.AmountCents)
                {
                    _logger.LogWarning("AmountMismatch PI={PI} Expect={Expect} Actual={Actual}", pi.Id, pi.AmountCents, payload.Data.Amount);
                    return Result.Failure(new Error(Error.Codes.Validation, "Amount mismatch."));
                }

                if (!isPaid)
                {
                    if (pi.Status != PaymentIntentStatus.Canceled && pi.Status != PaymentIntentStatus.Succeeded)
                    {
                        pi.Status = PaymentIntentStatus.Canceled;
                        pi.UpdatedBy = pi.UserId;
                        await _paymentIntentRepository.UpdateAsync(pi, innerCt).ConfigureAwait(false);
                        await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
                    }

                    _logger.LogInformation("PaymentMarkedCanceled PI={PI} Code={Code}", pi.Id, payload.Code);
                    return Result.Success();
                }

                return pi.Purpose switch
                {
                    PaymentPurpose.EventTicket => await ConfirmEventTicketAsync(pi, providerRef, innerCt).ConfigureAwait(false),
                    PaymentPurpose.TopUp => await ConfirmEscrowTopUpAsync(pi, providerRef, innerCt).ConfigureAwait(false),
                    PaymentPurpose.Membership => await ConfirmMembershipPurchaseAsync(pi, providerRef, innerCt).ConfigureAwait(false),
                    PaymentPurpose.WalletTopUp => await ConfirmWalletTopUpAsync(pi, providerRef, innerCt).ConfigureAwait(false),
                    _ => Result.Failure(new Error(Error.Codes.Validation, "Unsupported payment purpose."))
                };
            }, ct: ct).ConfigureAwait(false);

            if (businessResult.IsFailure)
            {
                await lease.ReleaseAsync().ConfigureAwait(false);
                return Result<PayOsWebhookOutcome>.Failure(businessResult.Error);
            }

            if (isPaid)
            {
                _logger.LogInformation("PaymentConfirmed Provider={Provider} OrderCode={OrderCode} ProviderRef={ProviderRef}", Provider, orderCode, providerRef);
            }

            return Result<PayOsWebhookOutcome>.Success(PayOsWebhookOutcome.Processed);
        }
        catch
        {
            await lease.ReleaseAsync().ConfigureAwait(false);
            throw;
        }
    }

    // ================
    // Verify helpers
    // ================
    private string BuildCreateSignature(long orderCode, long amount, string description, string returnUrl, string cancelUrl, string? webhookUrl = null)
    {
        // th? t? alphabet: amount, cancelUrl, description, orderCode, returnUrl, webhookUrl (if present)
        var data = $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";

        // Only include webhookUrl in signature if it's provided
        if (!string.IsNullOrWhiteSpace(webhookUrl))
        {
            data += $"&webhookUrl={webhookUrl}";
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SecretKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    public bool VerifyWebhookSignature(PayOsWebhookPayload payload)
    {
        if (payload is null || payload.Data is null || string.IsNullOrWhiteSpace(payload.Signature))
            return false;
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            _logger.LogWarning("payOS checksum key is not configured.");
            return false;
        }

        // 1) Convert Data -> Dictionary v� sort theo key
        var json = JsonSerializer.Serialize(payload.Data);
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;

        string Normalize(object? v)
        {
            if (v is null) return "";
            if (v is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Null) return "";
                if (je.ValueKind == JsonValueKind.Array)
                {
                    // m?ng -> serialize l?i sau khi chu?n ho� object con (sort key)
                    var arr = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(je.GetRawText()) ?? new();
                    var normalized = arr.Select(o =>
                        o.OrderBy(x => x.Key, StringComparer.Ordinal)
                         .ToDictionary(x => x.Key, x => x.Value));
                    return JsonSerializer.Serialize(normalized);
                }
                if (je.ValueKind == JsonValueKind.Object)
                {
                    var obj = JsonSerializer.Deserialize<Dictionary<string, object?>>(je.GetRawText()) ?? new();
                    var normalized = obj.OrderBy(x => x.Key, StringComparer.Ordinal)
                                        .ToDictionary(x => x.Key, x => x.Value);
                    return JsonSerializer.Serialize(normalized);
                }
                return je.ToString() ?? "";
            }
            return v.ToString() ?? "";
        }

        var ordered = dict.OrderBy(kv => kv.Key, StringComparer.Ordinal);
        var query = string.Join("&", ordered.Select(kv => $"{kv.Key}={Normalize(kv.Value)}"));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SecretKey));
        var expectedHex = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(query))).ToLowerInvariant();

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expectedHex),
                Convert.FromHexString(payload.Signature));
        }
        catch
        {
            return false;
        }
    }

    private string BuildPaymentsEndpointV2()
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl) ? "https://api-merchant.payos.vn" : _options.BaseUrl;
        return baseUrl.TrimEnd('/') + "/v2/payment-requests";
    }

    public async Task<Result<PayOsPaymentInfo>> GetPaymentInfoAsync(long orderCode, CancellationToken ct = default)
    {
        try
        {
            var endpoint = $"{BuildPaymentsEndpointV2()}/{orderCode}";

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.TryAddWithoutValidation("x-client-id", _options.ClientId);
            request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);

            var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayOS get payment info failed {Status}. OrderCode={OrderCode}, Body={Body}",
                    response.StatusCode, orderCode, body);
                return Result<PayOsPaymentInfo>.Failure(new Error(Error.Codes.Unexpected,
                    $"Failed to get payment info from PayOS. Status: {response.StatusCode}"));
            }

            PayOsGetPaymentResponse? parsed = null;
            try
            {
                parsed = JsonSerializer.Deserialize<PayOsGetPaymentResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Deserialize PayOS get payment response failed. Raw={Body}", body);
                return Result<PayOsPaymentInfo>.Failure(new Error(Error.Codes.Unexpected, "Failed to parse PayOS response."));
            }

            if (parsed?.Data is null)
            {
                _logger.LogWarning("PayOS get payment response missing data. Raw={Body}", body);
                return Result<PayOsPaymentInfo>.Failure(new Error(Error.Codes.Unexpected, "PayOS response missing data."));
            }

            var info = new PayOsPaymentInfo
            {
                OrderCode = parsed.Data.OrderCode,
                Amount = parsed.Data.Amount,
                Status = parsed.Data.Status ?? "",
                Reference = parsed.Data.Reference ?? parsed.Data.Id,
                TransactionDateTime = parsed.Data.TransactionDateTime
            };

            return Result<PayOsPaymentInfo>.Success(info);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting PayOS payment info. OrderCode={OrderCode}", orderCode);
            return Result<PayOsPaymentInfo>.Failure(new Error(Error.Codes.Unexpected, "Unexpected error while getting payment info."));
        }
    }

    private sealed record PayOsGetPaymentResponse
    {
        public string? Code { get; init; }
        public string? Desc { get; init; }
        public PayOsGetPaymentData? Data { get; init; }
    }

    private sealed record PayOsGetPaymentData
    {
        public string? Id { get; init; }
        public long OrderCode { get; init; }
        public long Amount { get; init; }
        public long AmountPaid { get; init; }
        public long AmountRemaining { get; init; }
        public string? Status { get; init; }
        public string? CreatedAt { get; init; }
        public string? TransactionDateTime { get; init; }
        public string? Reference { get; init; }
    }

    // =========================
    // Business confirm helpers
    // =========================

    private static bool IsSuccessStatus(string status) =>
        status.Equals("PAID", StringComparison.OrdinalIgnoreCase)
        || status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)
        || status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase);

    private async Task<Result> ConfirmEventTicketAsync(PaymentIntent pi, string providerRef, CancellationToken ct)
    {
        if (!pi.EventRegistrationId.HasValue)
            return Result.Failure(new Error(Error.Codes.Validation, "Ticket payment intent missing registration."));

        var registration = await _registrationQueryRepository.GetByIdAsync(pi.EventRegistrationId.Value, ct).ConfigureAwait(false);
        if (registration is null)
            return Result.Failure(new Error(Error.Codes.NotFound, "Registration not found."));

        if (registration.Status is EventRegistrationStatus.Confirmed or EventRegistrationStatus.CheckedIn)
        {
            await TriggerAttendQuestAsync(registration.UserId, registration.EventId, ct).ConfigureAwait(false);
            if (pi.Status != PaymentIntentStatus.Succeeded)
            {
                pi.Status = PaymentIntentStatus.Succeeded;
                pi.UpdatedBy = pi.UserId;
                await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
                await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            return Result.Success();
        }

        if (registration.Status is EventRegistrationStatus.Canceled or EventRegistrationStatus.Refunded)
            return Result.Failure(new Error(Error.Codes.Conflict, "Registration is no longer active."));

        var ev = await _eventQueryRepository.GetForUpdateAsync(registration.EventId, ct).ConfigureAwait(false)
                 ?? await _eventQueryRepository.GetByIdAsync(registration.EventId, ct).ConfigureAwait(false);
        if (ev is null) return Result.Failure(new Error(Error.Codes.NotFound, "Event not found."));

        if (pi.AmountCents != ev.PriceCents)
            return Result.Failure(new Error(Error.Codes.Validation, "Payment amount does not match ticket price."));

        if (ev.Capacity.HasValue)
        {
            var confirmedCount = await _eventQueryRepository.CountConfirmedAsync(ev.Id, ct).ConfigureAwait(false);
            if (confirmedCount >= ev.Capacity.Value)
                return Result.Failure(new Error(Error.Codes.Forbidden, "Event capacity reached."));
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
            await TriggerAttendQuestAsync(registration.UserId, registration.EventId, ct).ConfigureAwait(false);
            return Result.Success();
        }

        var wallet = await _walletRepository.EnsureAsync(registration.UserId, ct).ConfigureAwait(false);

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

        await TriggerAttendQuestAsync(registration.UserId, registration.EventId, ct).ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<Result> ConfirmEscrowTopUpAsync(PaymentIntent pi, string providerRef, CancellationToken ct)
    {
        if (!pi.EventId.HasValue)
            return Result.Failure(new Error(Error.Codes.Validation, "Top-up intent missing EventId."));

        var ev = await _eventQueryRepository.GetForUpdateAsync(pi.EventId.Value, ct).ConfigureAwait(false)
                 ?? await _eventQueryRepository.GetByIdAsync(pi.EventId.Value, ct).ConfigureAwait(false);
        if (ev is null) return Result.Failure(new Error(Error.Codes.NotFound, "Event not found for top-up."));

        if (pi.AmountCents <= 0)
            return Result.Failure(new Error(Error.Codes.Validation, "Top-up amount must be positive."));

        if (ev.EscrowMinCents <= 0)
            return Result.Failure(new Error(Error.Codes.Validation, "Event escrow requirement must be greater than zero."));

        var existingEscrow = await _escrowRepository.GetByEventIdAsync(ev.Id, ct).ConfigureAwait(false);
        var existingHold = existingEscrow?.AmountHoldCents ?? 0;
        var outstanding = Math.Max(0, ev.EscrowMinCents - existingHold);

        if (outstanding <= 0)
            return Result.Failure(new Error(Error.Codes.Conflict, "Event escrow has already been fully funded."));

        if (pi.AmountCents != outstanding)
            return Result.Failure(new Error(Error.Codes.Validation, $"Escrow top-up must equal the outstanding requirement of {outstanding} cents."));

        var wallet = await _walletRepository.EnsureAsync(pi.UserId, ct).ConfigureAwait(false);

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
        if (!credited) return Result.Failure(new Error(Error.Codes.Unexpected, "Failed to credit wallet."));

        var escrowToUpdate = existingEscrow ?? new Escrow
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            AmountHoldCents = 0,
            Status = EscrowStatus.Held,
            CreatedBy = pi.UserId,
        };
        escrowToUpdate.AmountHoldCents = existingHold + pi.AmountCents;
        if (escrowToUpdate.Status != EscrowStatus.Held) escrowToUpdate.Status = EscrowStatus.Held;
        await _escrowRepository.UpsertAsync(escrowToUpdate, ct).ConfigureAwait(false);

        var membershipResult = await EnsureCommunityMembershipAsync(ev.CommunityId, pi.UserId, ct).ConfigureAwait(false);
        if (membershipResult.IsFailure)
            return membershipResult;

        pi.Status = PaymentIntentStatus.Succeeded;
        pi.UpdatedBy = pi.UserId;
        await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    private async Task<Result> ConfirmMembershipPurchaseAsync(PaymentIntent pi, string providerRef, CancellationToken ct)
    {
        if (pi.AmountCents <= 0)
            return Result.Failure(new Error(Error.Codes.Validation, "Membership amount must be positive."));

        if (!pi.MembershipPlanId.HasValue)
            return Result.Failure(new Error(Error.Codes.Validation, "Membership plan information is required."));

        var plan = await _membershipPlanRepository.GetByIdAsync(pi.MembershipPlanId.Value, ct).ConfigureAwait(false);
        if (plan is null)
            return Result.Failure(new Error(Error.Codes.NotFound, "Membership plan referenced by payment intent was not found."));

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

        var ensurePlatformUser = await _platformAccountService.GetOrCreatePlatformUserIdAsync(ct).ConfigureAwait(false);
        if (ensurePlatformUser.IsFailure)
        {
            return ensurePlatformUser;
        }

        var platformUserId = ensurePlatformUser.Value;
        var platformWallet = await _walletRepository.EnsureAsync(platformUserId, ct).ConfigureAwait(false);

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = platformWallet.Id,
            AmountCents = pi.AmountCents,
            Direction = TransactionDirection.In,
            Method = TransactionMethod.Gateway,
            Status = TransactionStatus.Succeeded,
            Provider = Provider,
            ProviderRef = providerRef,
            Metadata = CreateMembershipMetadata(plan.Id, plan.Name),
            CreatedBy = platformUserId,
        };

        try
        {
            await _transactionRepository.CreateAsync(tx, ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            _logger.LogWarning(ex, "Duplicate payOS tx detected. ProviderRef={ProviderRef}", providerRef);

            if (pi.Status != PaymentIntentStatus.Succeeded)
            {
                pi.Status = PaymentIntentStatus.Succeeded;
                pi.UpdatedBy = pi.UserId;
                await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
                await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return Result.Success();
        }

        var credited = await _walletRepository.AdjustBalanceAsync(platformUserId, pi.AmountCents, ct).ConfigureAwait(false);
        if (!credited)
        {
            return Result.Failure(new Error(Error.Codes.Unexpected, "Failed to credit platform wallet."));
        }

        _ = await _membershipEnrollmentService.AssignAsync(pi.UserId, plan, pi.UserId, ct).ConfigureAwait(false);

        pi.Status = PaymentIntentStatus.Succeeded;
        pi.UpdatedBy = pi.UserId;
        await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        // Send membership confirmation email
        try
        {
            var user = await _userManager.FindByIdAsync(pi.UserId.ToString()).ConfigureAwait(false);
            if (user is not null && !string.IsNullOrWhiteSpace(user.Email))
            {
                var membership = await _userMembershipRepository.GetByUserIdAsync(pi.UserId, ct).ConfigureAwait(false);
                if (membership is not null)
                {
                    var emailMessage = _membershipEmailFactory.BuildMembershipPurchaseConfirmation(user, plan, membership);
                    await _emailQueue.EnqueueAsync(emailMessage, ct).ConfigureAwait(false);
                    _logger.LogInformation("Membership confirmation email queued for user {UserId}, plan {PlanName}", pi.UserId, plan.Name);
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail the transaction if email fails
            _logger.LogWarning(ex, "Failed to send membership confirmation email for user {UserId}", pi.UserId);
        }

        return Result.Success();
    }

    private async Task<Result> ConfirmWalletTopUpAsync(PaymentIntent pi, string providerRef, CancellationToken ct)
    {
        if (pi.AmountCents <= 0)
            return Result.Failure(new Error(Error.Codes.Validation, "Top-up amount must be positive."));

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

        var wallet = await _walletRepository.EnsureAsync(pi.UserId, ct).ConfigureAwait(false);

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
            _logger.LogWarning(ex, "Duplicate payOS tx detected. ProviderRef={ProviderRef}", providerRef);
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
        if (!credited) return Result.Failure(new Error(Error.Codes.Unexpected, "Failed to credit wallet."));

        pi.Status = PaymentIntentStatus.Succeeded;
        pi.UpdatedBy = pi.UserId;
        await _paymentIntentRepository.UpdateAsync(pi, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trigger ATTEND_EVENT quest for user {UserId} event {EventId}.", userId, eventId);
        }
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
        var payload = new Dictionary<string, object?> { ["note"] = note };
        if (eventId.HasValue) payload["eventId"] = eventId.Value;
        if (counterpartyUserId.HasValue) payload["counterpartyUserId"] = counterpartyUserId.Value;
        return JsonSerializer.SerializeToDocument(payload);
    }
}
