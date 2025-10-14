using DTOs.Payments.VnPay;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Configuration;
using Services.Helpers;
using System.Collections.Specialized;
using System.Web;

namespace Services.Implementations;

/// <summary>
/// VNPAY payment gateway implementation.
/// </summary>
public sealed class VnPayService : IVnPayService
{
    private readonly VnPayConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<VnPayService> _logger;

    public VnPayService(
        IOptions<VnPayConfig> options,
        HttpClient httpClient,
        ILogger<VnPayService> logger)
    {
        _config = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<VnPayCreatePaymentResponse> CreatePaymentUrlAsync(VnPayCreatePaymentRequest req)
    {
        return CreatePaymentUrlAsync(req, _config.ReturnUrl);
    }

    public Task<VnPayCreatePaymentResponse> CreatePaymentUrlAsync(VnPayCreatePaymentRequest req, string returnUrl)
    {
        ArgumentNullException.ThrowIfNull(req);
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            throw new ArgumentException("Return URL is required.", nameof(returnUrl));
        }

        try
        {
            var lib = new VnPayLibrary();

            // Amount in VND smallest unit (already in cents, multiply by 100 for VNPAY spec)
            var vnpAmount = req.vnp_Amount * 100;

            lib.AddRequestData("vnp_Version", _config.Version);
            lib.AddRequestData("vnp_Command", "pay");
            lib.AddRequestData("vnp_TmnCode", _config.TmnCode);
            lib.AddRequestData("vnp_Amount", vnpAmount.ToString());
            lib.AddRequestData("vnp_CurrCode", _config.CurrCode);
            lib.AddRequestData("vnp_TxnRef", req.vnp_TxnRef);
            lib.AddRequestData("vnp_OrderInfo", req.vnp_OrderInfo);
            lib.AddRequestData("vnp_OrderType", req.vnp_OrderType);
            lib.AddRequestData("vnp_Locale", req.vnp_Locale);
            lib.AddRequestData("vnp_ReturnUrl", returnUrl);
            lib.AddRequestData("vnp_IpAddr", req.vnp_IpAddr);
            lib.AddRequestData("vnp_CreateDate", req.vnp_CreateDate);

            if (!string.IsNullOrEmpty(req.vnp_BankCode))
            {
                lib.AddRequestData("vnp_BankCode", req.vnp_BankCode);
            }

            var paymentUrl = lib.CreateRequestUrl(_config.BaseUrl, _config.HashSecret);

            _logger.LogInformation("Created VNPAY payment URL for TxnRef={TxnRef}, Amount={Amount}",
                req.vnp_TxnRef, vnpAmount);

            return Task.FromResult(new VnPayCreatePaymentResponse
            {
                Success = true,
                PaymentUrl = paymentUrl,
                Message = "Payment URL created successfully."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create VNPAY payment URL for TxnRef={TxnRef}", req.vnp_TxnRef);

            return Task.FromResult(new VnPayCreatePaymentResponse
            {
                Success = false,
                PaymentUrl = string.Empty,
                Message = $"Failed to create payment URL: {ex.Message}"
            });
        }
    }

    public Task<VnPayQueryResponse> QueryPaymentAsync(VnPayQueryRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);

        // Query API implementation can be added later if needed
        throw new NotImplementedException("VNPAY query API not yet implemented.");
    }

    public bool ValidateCallback(VnPayCallbackRequest cb)
    {
        ArgumentNullException.ThrowIfNull(cb);

        try
        {
            var lib = new VnPayLibrary();

            lib.AddResponseData("vnp_Amount", cb.vnp_Amount.ToString());
            if (!string.IsNullOrEmpty(cb.vnp_BankCode))
                lib.AddResponseData("vnp_BankCode", cb.vnp_BankCode);
            if (!string.IsNullOrEmpty(cb.vnp_BankTranNo))
                lib.AddResponseData("vnp_BankTranNo", cb.vnp_BankTranNo);
            if (!string.IsNullOrEmpty(cb.vnp_CardType))
                lib.AddResponseData("vnp_CardType", cb.vnp_CardType);
            if (!string.IsNullOrEmpty(cb.vnp_OrderInfo))
                lib.AddResponseData("vnp_OrderInfo", cb.vnp_OrderInfo);
            if (!string.IsNullOrEmpty(cb.vnp_PayDate))
                lib.AddResponseData("vnp_PayDate", cb.vnp_PayDate);
            lib.AddResponseData("vnp_ResponseCode", cb.vnp_ResponseCode);
            if (!string.IsNullOrEmpty(cb.vnp_TmnCode))
                lib.AddResponseData("vnp_TmnCode", cb.vnp_TmnCode);
            if (!string.IsNullOrEmpty(cb.vnp_TransactionNo))
                lib.AddResponseData("vnp_TransactionNo", cb.vnp_TransactionNo);
            if (!string.IsNullOrEmpty(cb.vnp_TransactionStatus))
                lib.AddResponseData("vnp_TransactionStatus", cb.vnp_TransactionStatus);
            lib.AddResponseData("vnp_TxnRef", cb.vnp_TxnRef);

            var isValid = lib.ValidateSignature(cb.vnp_SecureHash, _config.HashSecret);

            if (!isValid)
            {
                _logger.LogWarning("Invalid VNPAY callback signature for TxnRef={TxnRef}", cb.vnp_TxnRef);
            }
            else
            {
                _logger.LogInformation("Valid VNPAY callback signature for TxnRef={TxnRef}, ResponseCode={ResponseCode}",
                    cb.vnp_TxnRef, cb.vnp_ResponseCode);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating VNPAY callback for TxnRef={TxnRef}", cb.vnp_TxnRef);
            return false;
        }
    }

    public bool ValidateCallbackFromQuery(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var queryCollection = new NameValueCollection();

            foreach (var (key, value) in request.Query)
            {
                if (!string.IsNullOrEmpty(key) && value.Count > 0)
                {
                    queryCollection[key] = value.ToString();
                }
            }

            var isValid = VnPayLibrary.ValidateSignatureFromQuery(queryCollection, _config.HashSecret);

            var txnRef = queryCollection["vnp_TxnRef"];
            var responseCode = queryCollection["vnp_ResponseCode"];

            if (!isValid)
            {
                _logger.LogWarning("Invalid VNPAY callback signature from query for TxnRef={TxnRef}", txnRef);
            }
            else
            {
                _logger.LogInformation("Valid VNPAY callback signature from query for TxnRef={TxnRef}, ResponseCode={ResponseCode}",
                    txnRef, responseCode);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating VNPAY callback from query");
            return false;
        }
    }
}
