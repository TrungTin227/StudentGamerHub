using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BusinessObjects;
using BusinessObjects.Common;
using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Repositories.Persistence;
using WebApi.Payments.Tests.Helpers;
using WebApi.Payments.Tests.Infrastructure;
using Xunit;
using Services.Interfaces;
using Services.Configuration;

namespace WebApi.Payments.Tests;

public sealed class WalletTopUpFlowTests : IAsyncLifetime
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
    public async Task WalletTopUp_WithPayOsWebhook_UpdatesWalletBalance()
    {
        await _factory.ResetDatabaseAsync();
        const long topUpAmount = 125_000;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.AddAsync(TestEntityFactory.CreateUser(PaymentsApiFactory.DefaultUserId));
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var createIntentResponse = await client.PostAsJsonAsync(
            "/api/wallet/topups",
            new { AmountCents = topUpAmount },
            new JsonSerializerOptions { PropertyNamingPolicy = null });

        createIntentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var intentPayload = await createIntentResponse.Content.ReadFromJsonAsync<JsonElement>();
        var intentId = intentPayload.GetProperty("paymentIntentId").GetGuid();

        var checkoutResponse = await client.PostAsJsonAsync(
            "/api/payments/payos/create",
            new { IntentId = intentId, ReturnUrl = "https://frontend.test.local/payment/result" },
            new JsonSerializerOptions { PropertyNamingPolicy = null });

        var checkoutBody = await checkoutResponse.Content.ReadAsStringAsync();
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK, checkoutBody);

        long orderCode;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            orderCode = await db.PaymentIntents
                .Where(pi => pi.Id == intentId)
                .Select(pi => pi.OrderCode ?? 0)
                .SingleAsync();
            orderCode.Should().BeGreaterThan(0);
        }

        var providerRef = $"ref-{Guid.NewGuid():N}";
        var payload = PayOsTestHelper.CreatePayload(
            orderCode,
            topUpAmount,
            providerRef,
            "Wallet top up",
            code: "00",
            success: true,
            timestamp: DateTimeOffset.UtcNow,
            secret: PaymentsApiFactory.SecretKey);

        var rawBody = PayOsTestHelper.SerializeBody(payload);
        var signature = PayOsTestHelper.ComputeBodySignature(rawBody, PaymentsApiFactory.SecretKey);

        using var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments/payos/webhook")
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
        };
        webhookRequest.Headers.TryAddWithoutValidation("x-signature", signature);

        var webhookResponse = await client.SendAsync(webhookRequest);
        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var wallet = await db.Wallets.SingleAsync(w => w.UserId == PaymentsApiFactory.DefaultUserId);
            wallet.BalanceCents.Should().Be(topUpAmount);

            var intent = await db.PaymentIntents.SingleAsync(pi => pi.Id == intentId);
            intent.Status.Should().Be(PaymentIntentStatus.Succeeded);
            intent.OrderCode.Should().Be(orderCode);

            var transaction = await db.Transactions.SingleAsync();
            transaction.Provider.Should().Be("PAYOS");
            transaction.ProviderRef.Should().Be(providerRef);
            transaction.AmountCents.Should().Be(topUpAmount);
            transaction.Direction.Should().Be(TransactionDirection.In);
        }
    }

    [Fact]
    public async Task EventCreation_WithCustomPrice_UsesRequestedValuesAndAdjustsWallets()
    {
        await _factory.ResetDatabaseAsync();
        const long initialBalance = 250_000;
        const long eventPriceCents = 180_000;
        const long escrowMinCents = 90_000;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.AddAsync(TestEntityFactory.CreateUser(PaymentsApiFactory.DefaultUserId));
            await db.Wallets.AddAsync(TestEntityFactory.CreateWallet(PaymentsApiFactory.DefaultUserId, initialBalance));
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();

        var createEventRequest = new
        {
            CommunityId = (Guid?)null,
            Title = "Custom priced event",
            Description = "Integration test event",
            Mode = (int)EventMode.Online,
            Location = (string?)null,
            StartsAt = DateTime.UtcNow.AddDays(2),
            EndsAt = DateTime.UtcNow.AddDays(2).AddHours(2),
            PriceCents = eventPriceCents,
            Capacity = 50,
            EscrowMinCents = escrowMinCents,
            PlatformFeeRate = 0.1m,
            GatewayFeePolicy = (int)GatewayFeePolicy.OrganizerPays
        };

        var createEventResponse = await client.PostAsJsonAsync(
            "/api/events",
            createEventRequest,
            new JsonSerializerOptions { PropertyNamingPolicy = null });

        createEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var eventPayload = await createEventResponse.Content.ReadFromJsonAsync<JsonElement>();
        var eventId = eventPayload.GetProperty("eventId").GetGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var evt = await db.Events.SingleAsync(e => e.Id == eventId);
            evt.PriceCents.Should().Be(eventPriceCents);
            evt.EscrowMinCents.Should().Be(escrowMinCents);

            var organizerWallet = await db.Wallets.SingleAsync(w => w.UserId == PaymentsApiFactory.DefaultUserId);
            var billingOptions = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<BillingOptions>>().Value;
            organizerWallet.BalanceCents.Should().Be(initialBalance - billingOptions.EventCreationFeeCents);

            var platformAccountService = scope.ServiceProvider.GetRequiredService<IPlatformAccountService>();
            var platformUserIdResult = await platformAccountService.GetOrCreatePlatformUserIdAsync();
            platformUserIdResult.IsSuccess.Should().BeTrue();

            var platformWallet = await db.Wallets.SingleAsync(w => w.UserId == platformUserIdResult.Value);
            platformWallet.BalanceCents.Should().Be(billingOptions.EventCreationFeeCents);

            var transactions = await db.Transactions.Where(t => t.EventId == eventId).ToListAsync();
            transactions.Should().HaveCount(2);
            transactions.Should().ContainSingle(t => t.Direction == TransactionDirection.Out && t.AmountCents == billingOptions.EventCreationFeeCents);
            transactions.Should().ContainSingle(t => t.Direction == TransactionDirection.In && t.AmountCents == billingOptions.EventCreationFeeCents);
        }
    }
}
