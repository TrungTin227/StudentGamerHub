using System.Net;
using BusinessObjects;
using BusinessObjects.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Persistence;
using WebApi.Payments.Tests.Infrastructure;
using Xunit;

namespace WebApi.Payments.Tests;

public sealed class PaymentConfirmTests : IAsyncLifetime
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
    public async Task ConfirmTopUp_UpdatesWalletAndIntent()
    {
        await _factory.ResetDatabaseAsync();
        const long amount = 80_000;
        var intentId = Guid.Parse("abababab-abab-abab-abab-abababababab");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = TestEntityFactory.CreateUser(_userId);
            var wallet = TestEntityFactory.CreateWallet(_userId, balanceCents: 100_000);
            var ev = TestEntityFactory.CreateEvent(_userId);

            await db.Users.AddAsync(user);
            await db.Wallets.AddAsync(wallet);
            await db.Events.AddAsync(ev);
            await db.PaymentIntents.AddAsync(new PaymentIntent
            {
                Id = intentId,
                UserId = _userId,
                AmountCents = amount,
                Purpose = PaymentPurpose.TopUp,
                EventId = ev.Id,
                Status = PaymentIntentStatus.RequiresPayment,
                ClientSecret = "client-secret",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = _userId
            });

            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", _userId.ToString());

        using var response = await client.PostAsync($"/api/payments/{intentId}/confirm", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updatedIntent = await db.PaymentIntents.SingleAsync(pi => pi.Id == intentId);
            updatedIntent.Status.Should().Be(PaymentIntentStatus.Succeeded);

            var wallet = await db.Wallets.SingleAsync(w => w.UserId == _userId);
            wallet.BalanceCents.Should().Be(20_000);

            var transaction = await db.Transactions.SingleAsync();
            transaction.Provider.Should().Be("LOCAL");
            transaction.AmountCents.Should().Be(amount);
            transaction.Direction.Should().Be(TransactionDirection.Out);
        }
    }

    [Fact]
    public async Task ConfirmEndpoint_HonorsPaymentsWriteRateLimiter()
    {
        await _factory.ResetDatabaseAsync();
        const long amount = 10_000;
        var intentId = Guid.Parse("c4c4c4c4-c4c4-c4c4-c4c4-c4c4c4c4c4c4");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = TestEntityFactory.CreateUser(_userId);
            var wallet = TestEntityFactory.CreateWallet(_userId, balanceCents: 500_000);
            var ev = TestEntityFactory.CreateEvent(_userId);

            await db.Users.AddAsync(user);
            await db.Wallets.AddAsync(wallet);
            await db.Events.AddAsync(ev);
            await db.PaymentIntents.AddAsync(new PaymentIntent
            {
                Id = intentId,
                UserId = _userId,
                AmountCents = amount,
                Purpose = PaymentPurpose.TopUp,
                EventId = ev.Id,
                Status = PaymentIntentStatus.RequiresPayment,
                ClientSecret = "client-secret",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = _userId
            });

            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", _userId.ToString());

        HttpStatusCode? limited = null;
        for (var i = 0; i < 140 && limited is null; i++)
        {
            using var resp = await client.PostAsync($"/api/payments/{intentId}/confirm", content: null);
            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            {
                limited = resp.StatusCode;
            }
        }

        limited.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
