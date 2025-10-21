using FluentValidation;
using Microsoft.Extensions.Options;
using Services.Configuration;

namespace DTOs.Wallets.Validation;

internal sealed class WalletTopUpRequestValidator : AbstractValidator<WalletTopUpRequestDto>
{
    public WalletTopUpRequestValidator(IOptionsSnapshot<BillingOptions> billingOptions)
    {
        if (billingOptions is null)
        {
            throw new ArgumentNullException(nameof(billingOptions));
        }

        var options = billingOptions.Value;

        RuleFor(x => x.AmountCents)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero.")
            .LessThanOrEqualTo(options.MaxWalletTopUpAmountCents)
            .WithMessage($"Amount must be less than or equal to {options.MaxWalletTopUpAmountCents}.");
    }
}
