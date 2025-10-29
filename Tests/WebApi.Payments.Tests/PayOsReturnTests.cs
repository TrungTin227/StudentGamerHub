using System.Net;
using BusinessObjects;
using BusinessObjects.Common;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Persistence;
using WebApi.Payments.Tests.Infrastructure;
using Xunit;

namespace WebApi.Payments.Tests;

public sealed class PayOsReturnTests : IAsyncLifetime
{
    private readonly PaymentsApiFactory _factory = new();

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PaidStatus_WithKnownOrderCode_RedirectsToSuccess()
    {
        await _factory.ResetDatabaseAsync();
        const long orderCode = 5556677889900;
        var intentId = Guid.Parse("99999999-aaaa-bbbb-cccc-dddddddddddd");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.AddAsync(TestEntityFactory.CreateUser(PaymentsApiFactory.DefaultUserId));
            await db.PaymentIntents.AddAsync(new PaymentIntent
            {
                Id = intentId,
                UserId = PaymentsApiFactory.DefaultUserId,
                AmountCents = 42_000,
                Purpose = PaymentPurpose.WalletTopUp,
                Status = PaymentIntentStatus.RequiresPayment,
                ClientSecret = "client-secret",
                ExpiresAt = DateTime.UtcNow.AddMinutes(20),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = PaymentsApiFactory.DefaultUserId,
                OrderCode = orderCode
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/payments/payos/return?status=PAID&orderCode={orderCode}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().Be($"/payment/result?status=success&intentId={intentId}");
    }

    [Theory]
    [InlineData("FAILED", 777)]
    [InlineData("PAID", 999)]
    [InlineData("", 777)]
    public async Task NonSuccessStatusOrUnknownOrder_RedirectsToFailure(string status, long orderCode)
    {
        await _factory.ResetDatabaseAsync();

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/payments/payos/return?status={status}&orderCode={orderCode}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().Be("/payment/result?status=failed");
    }
}
