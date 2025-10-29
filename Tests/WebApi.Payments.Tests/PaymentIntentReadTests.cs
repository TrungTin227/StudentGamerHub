using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessObjects;
using BusinessObjects.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Persistence;
using WebApi.Payments.Tests.Infrastructure;
using Xunit;

namespace WebApi.Payments.Tests;

public sealed class PaymentIntentReadTests : IAsyncLifetime
{
    private readonly PaymentsApiFactory _factory = new();
    private readonly Guid _userId = PaymentsApiFactory.DefaultUserId;

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
    public async Task GetIntent_ReturnsProjectedDtoWithTransactionDetails()
    {
        await _factory.ResetDatabaseAsync();
        var orderCode = 4455667788990;
        var intentId = Guid.Parse("22222222-3333-4444-5555-666666666666");
        var providerRef = orderCode.ToString();
        var metadata = JsonDocument.Parse("""{ "note": "EVENT_TICKET", "counterpartyUserId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" }""");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.AddAsync(TestEntityFactory.CreateUser(_userId));

            var wallet = TestEntityFactory.CreateWallet(_userId, balanceCents: 200_000);
            await db.Wallets.AddAsync(wallet);

            await db.PaymentIntents.AddAsync(new PaymentIntent
            {
                Id = intentId,
                UserId = _userId,
                AmountCents = 150_000,
                Purpose = PaymentPurpose.WalletTopUp,
                Status = PaymentIntentStatus.Succeeded,
                ClientSecret = "client-secret",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-30),
                CreatedBy = _userId,
                OrderCode = orderCode
            });

            await db.Transactions.AddAsync(new Transaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                AmountCents = 150_000,
                Direction = TransactionDirection.In,
                Method = TransactionMethod.Gateway,
                Status = TransactionStatus.Succeeded,
                Provider = "PAYOS",
                ProviderRef = providerRef,
                Metadata = metadata,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = _userId
            });

            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", _userId.ToString());

        var response = await client.GetAsync($"/api/payments/{intentId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        doc.GetProperty("Id").GetGuid().Should().Be(intentId);
        doc.GetProperty("AmountCents").GetInt64().Should().Be(150_000);
        doc.GetProperty("Status").GetString().Should().Be(nameof(PaymentIntentStatus.Succeeded));
        doc.GetProperty("TransactionId").GetString().Should().Be(providerRef);
        doc.GetProperty("ProviderName").GetString().Should().Be("PAYOS");
        doc.GetProperty("MetadataJson").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
