using BusinessObjects.Common;
using BusinessObjects.Common.Results;
using FluentAssertions;
using Xunit;

namespace Services.Games.Tests;

public sealed class ResultExtensionsSmokeTests
{
    [Fact]
    public async Task TryAsync_ShouldReturnFailure_WhenExceptionThrown()
    {
        var result = await ResultExtensions.TryAsync(async () =>
        {
            await Task.Delay(10);
            throw new InvalidOperationException("boom");
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Unexpected);
    }
}
