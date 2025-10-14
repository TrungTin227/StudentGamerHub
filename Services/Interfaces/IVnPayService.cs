using DTOs.Payments.VnPay;
using Microsoft.AspNetCore.Http;

namespace Services.Interfaces;

/// <summary>
/// VNPAY payment gateway adapter.
/// </summary>
public interface IVnPayService
{
    Task<VnPayCreatePaymentResponse> CreatePaymentUrlAsync(VnPayCreatePaymentRequest req);
    Task<VnPayCreatePaymentResponse> CreatePaymentUrlAsync(VnPayCreatePaymentRequest req, string returnUrl);
    Task<VnPayQueryResponse> QueryPaymentAsync(VnPayQueryRequest req);
    bool ValidateCallback(VnPayCallbackRequest cb);
    bool ValidateCallbackFromQuery(HttpRequest request);
}
