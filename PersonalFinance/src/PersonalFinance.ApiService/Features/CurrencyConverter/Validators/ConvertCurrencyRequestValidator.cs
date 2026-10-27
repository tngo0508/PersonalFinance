using FluentValidation;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.CurrencyConverter;

namespace PersonalFinance.ApiService.Features.CurrencyConverter.Validators;

/// <summary>
/// Validator for one conversion: an amount of zero or more, From and To among <see cref="ConvertibleCurrencies"/>, and
/// a date (when given) from <see cref="EarliestDate"/> to today (UTC). Every rule carries its
/// <see cref="ApiErrorKeys.CurrencyConverter"/> key (ADR 0017).
/// </summary>
internal sealed class ConvertCurrencyRequestValidator : AbstractValidator<ConvertCurrencyRequestDto>
{
    /// <summary>The earliest day a conversion may ask for.</summary>
    public static readonly DateOnly EarliestDate = new(2015, 1, 1);

    public ConvertCurrencyRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Amount)
            .NotNull()
                .WithErrorCode(ApiErrorKeys.CurrencyConverter.AmountRequired)
                .WithMessage("Enter an amount.")
            .GreaterThanOrEqualTo(0m)
                .WithErrorCode(ApiErrorKeys.CurrencyConverter.AmountNegative)
                .WithMessage("Amount cannot be negative.");

        RuleFor(x => x.From)
            .Must(ConvertibleCurrencies.IsConvertible)
                .WithErrorCode(ApiErrorKeys.CurrencyConverter.FromUnsupported)
                .WithMessage("Choose a currency to convert from.");

        RuleFor(x => x.To)
            .Must(ConvertibleCurrencies.IsConvertible)
                .WithErrorCode(ApiErrorKeys.CurrencyConverter.ToUnsupported)
                .WithMessage("Choose a currency to convert to.");

        RuleFor(x => x.Date)
            .Must(date => date is null || date.Value >= EarliestDate)
                .WithErrorCode(ApiErrorKeys.CurrencyConverter.DateTooEarly)
                .WithMessage("Choose a date on or after 1 January 2015.")
            .Must(date => date is null || date.Value <= DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
                .WithErrorCode(ApiErrorKeys.CurrencyConverter.DateInFuture)
                .WithMessage("Choose today or an earlier date.");
    }
}
