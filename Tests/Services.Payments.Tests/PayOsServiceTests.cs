using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Repositories.Interfaces;
using Repositories.WorkSeeds.Interfaces;
using Services.Configuration;
using Services.Implementations;
using Services.Application.Quests;
using Services.Interfaces;
using Xunit;

namespace Services.Payments.Tests;

public sealed class PayOsServiceTests
{
    [Fact]
    public void ValidatePayOsSignature_ReturnsTrueForValidSignature()
    {
        const string secret = "test-secret";
        const string payload = "{\"orderCode\":123}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        PayOsService.ValidatePayOsSignature(payload, signature, secret).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ffzz")]
    public void ValidatePayOsSignature_ReturnsFalseForInvalidSignature(string? signature)
    {
        const string secret = "test-secret";
        const string payload = "{\"orderCode\":123}";

        PayOsService.ValidatePayOsSignature(payload, signature, secret).Should().BeFalse();
    }

    [Fact]
    public async Task AcquireReplayLease_PreventsImmediateReplay()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(cache);

        var method = typeof(PayOsService)
            .GetMethod("AcquireReplayLeaseAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Expected replay lease method not found.");

        var firstTask = (Task)method.Invoke(service, new object[] { 123456L, "fingerprint" })!;
        await firstTask.ConfigureAwait(false);
        var firstLease = firstTask.GetType().GetProperty("Result")!.GetValue(firstTask)!;
        var acquiredProperty = firstLease.GetType().GetProperty("Acquired")!;
        ((bool)acquiredProperty.GetValue(firstLease)!).Should().BeTrue();

        var replayTask = (Task)method.Invoke(service, new object[] { 123456L, "fingerprint" })!;
        await replayTask.ConfigureAwait(false);
        var replayLease = replayTask.GetType().GetProperty("Result")!.GetValue(replayTask)!;
        ((bool)acquiredProperty.GetValue(replayLease)!).Should().BeFalse();
    }

    private static PayOsService CreateService(IMemoryCache cache)
    {
        var options = Substitute.For<IOptionsSnapshot<PayOsOptions>>();
        options.Value.Returns(new PayOsOptions { SecretKey = "unit-test-secret" });

        return new PayOsService(
            httpClient: new HttpClient(),
            configOptions: options,
            memoryCache: cache,
            redis: null,
            logger: NullLogger<PayOsService>.Instance,
            uow: Substitute.For<IGenericUnitOfWork>(),
            paymentIntentRepository: Substitute.For<IPaymentIntentRepository>(),
            registrationQueryRepository: Substitute.For<IRegistrationQueryRepository>(),
            registrationCommandRepository: Substitute.For<IRegistrationCommandRepository>(),
            eventQueryRepository: Substitute.For<IEventQueryRepository>(),
            transactionRepository: Substitute.For<ITransactionRepository>(),
            walletRepository: Substitute.For<IWalletRepository>(),
            escrowRepository: Substitute.For<IEscrowRepository>(),
            questService: Substitute.For<IQuestService>());
    }
}
