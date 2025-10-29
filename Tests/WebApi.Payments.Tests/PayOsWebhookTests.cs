using System.Net;
using System.Text;
using System.Text.Json;
using BusinessObjects;
using BusinessObjects.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Persistence;
using WebApi.Payments.Tests.Helpers;
using WebApi.Payments.Tests.Infrastructure;
using System.Net.Http.Json;
using Xunit;

namespace WebApi.Payments.Tests;

public sealed class PayOsWebhookTests : IAsyncLifetime
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
    public async Task ValidSignature_ProcessesWebhookSuccessfully()
    {
        await _factory.ResetDatabaseAsync();
        const long orderCode = 9123456789012;
        const long amount = 150_000;
        const string providerRef = "txn_123";
        var now = DateTimeOffset.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.AddAsync(TestEntityFactory.CreateUser(PaymentsApiFactory.DefaultUserId));
            await db.PaymentIntents.AddAsync(new PaymentIntent
            {
                Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                UserId = PaymentsApiFactory.DefaultUserId,
                AmountCents = amount,
                Purpose = PaymentPurpose.WalletTopUp,
                Status = PaymentIntentStatus.RequiresPayment,
                ClientSecret = "client-secret",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = PaymentsApiFactory.DefaultUserId,
                OrderCode = orderCode
            });
            await db.SaveChangesAsync();
        }

        var payload = PayOsTestHelper.CreatePayload(
            orderCode,
            amount,
            providerRef,
            "Wallet top up",
            code: "00",
            success: true,
            timestamp: now,
            secret: PaymentsApiFactory.SecretKey);

        var rawBody = PayOsTestHelper.SerializeBody(payload);
        var signature = PayOsTestHelper.ComputeBodySignature(rawBody, PaymentsApiFactory.SecretKey);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/payos/webhook")
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("x-signature", signature);

        using var client = _factory.CreateClient();
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("status").GetString().Should().Be("ok");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var intent = await db.PaymentIntents.SingleAsync(x => x.OrderCode == orderCode);
            intent.Status.Should().Be(PaymentIntentStatus.Succeeded);

            var transaction = await db.Transactions.SingleAsync();
            transaction.Provider.Should().Be("PAYOS");
            transaction.ProviderRef.Should().Be(providerRef);
            transaction.AmountCents.Should().Be(amount);
            transaction.Direction.Should().Be(TransactionDirection.In);
        }
    }

    [Fact]
    public async Task InvalidSignature_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        const long orderCode = 8234567890123;
        const long amount = 50_000;
        var now = DateTimeOffset.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.AddAsync(TestEntityFactory.CreateUser(PaymentsApiFactory.DefaultUserId));
            await db.PaymentIntents.AddAsync(new PaymentIntent
            {
                Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                UserId = PaymentsApiFactory.DefaultUserId,
                AmountCents = amount,
                Purpose = PaymentPurpose.WalletTopUp,
                Status = PaymentIntentStatus.RequiresPayment,
                ClientSecret = "secret",
                ExpiresAt = DateTime.UtcNow.AddMinutes(20),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = PaymentsApiFactory.DefaultUserId,
                OrderCode = orderCode
            });
            await db.SaveChangesAsync();
        }

        var payload = PayOsTestHelper.CreatePayload(
            orderCode,
            amount,
            reference: "dup-ref",
            description: "Wallet top up",
            code: "00",
            success: true,
            timestamp: now,
            secret: PaymentsApiFactory.SecretKey);

        var rawBody = PayOsTestHelper.SerializeBody(payload);
        var tamperedSignature = PayOsTestHelper.ComputeBodySignature(rawBody + "tamper", PaymentsApiFactory.SecretKey);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/payos/webhook")
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("x-signature", tamperedSignature);

        using var client = _factory.CreateClient();
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("validation_error");
    }

    [Fact]
    public async Task ReplayPayload_ReturnsIgnored()
    {
        await _factory.ResetDatabaseAsync();
        const long orderCode = 7234567890123;
        const long amount = 75_000;
        const string providerRef = "provider-ref";
        var now = DateTimeOffset.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.AddAsync(TestEntityFactory.CreateUser(PaymentsApiFactory.DefaultUserId));
            await db.PaymentIntents.AddAsync(new PaymentIntent
            {
                Id = Guid.Parse("12345678-9abc-def0-1234-56789abcdef0"),
                UserId = PaymentsApiFactory.DefaultUserId,
                AmountCents = amount,
                Purpose = PaymentPurpose.WalletTopUp,
                Status = PaymentIntentStatus.RequiresPayment,
                ClientSecret = "secret",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = PaymentsApiFactory.DefaultUserId,
                OrderCode = orderCode
            });
            await db.SaveChangesAsync();
        }

        var payload = PayOsTestHelper.CreatePayload(
            orderCode,
            amount,
            providerRef,
            "Wallet top up",
            code: "00",
            success: true,
            timestamp: now,
            secret: PaymentsApiFactory.SecretKey);
        var rawBody = PayOsTestHelper.SerializeBody(payload);
        var signature = PayOsTestHelper.ComputeBodySignature(rawBody, PaymentsApiFactory.SecretKey);

        using var client = _factory.CreateClient();
        using (var first = new HttpRequestMessage(HttpMethod.Post, "/api/payments/payos/webhook")
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
        })
        {
            first.Headers.TryAddWithoutValidation("x-signature", signature);
            var firstResponse = await client.SendAsync(first);
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var replay = new HttpRequestMessage(HttpMethod.Post, "/api/payments/payos/webhook")
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
        };
        replay.Headers.TryAddWithoutValidation("x-signature", signature);

        using var replayResponse = await client.SendAsync(replay);
        replayResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var replayContent = await replayResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(replayContent);
        doc.RootElement.GetProperty("status").GetString().Should().Be("ignored");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var transactions = await db.Transactions.ToListAsync();
            transactions.Should().HaveCount(1);
            transactions[0].ProviderRef.Should().Be(providerRef);
        }
    }

}
