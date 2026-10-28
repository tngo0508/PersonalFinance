using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Infrastructure;

/// <summary>
/// The range of a money field: from 0 to a bound written in whole US dollars and scaled to the currency of the row
/// (<see cref="MoneyBounds"/>, #444). The currency is read from the model's <see cref="CurrencyProperty"/> when the
/// form is validated, so a budget posted in đồng is checked against the đồng bound while the same figure in dollars
/// is still rejected. <see cref="ValidationAttribute.ErrorMessage"/> is an en.json key whose <c>{min}</c> and
/// <c>{max}</c> are filled with whole amounts in that currency, formatted for the request culture, through the
/// request's <see cref="IStringLocalizer"/>; without one (a bare <see cref="Validator"/> call) the key itself is the
/// template.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MoneyRangeAttribute : ValidationAttribute
{
    /// <param name="dollarMaximum">The bound in whole US dollars: one of the <c>*Limits</c> constants in Shared.</param>
    public MoneyRangeAttribute(long dollarMaximum)
    {
        DollarMaximum = dollarMaximum;
    }

    /// <summary>The bound in whole US dollars.</summary>
    public long DollarMaximum { get; }

    /// <summary>
    /// The model property holding the ISO 4217 code of the amounts. A null or unsupported code reads as US dollars; the
    /// controller rejects an unsupported one on its own.
    /// </summary>
    public string CurrencyProperty { get; init; } = "CurrencyCode";

    /// <summary>Whether 0 itself is out of range (an income must be over 0).</summary>
    public bool ExclusiveMinimum { get; init; }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not decimal amount)
        {
            return ValidationResult.Success;
        }

        string currency = CurrencyOf(validationContext.ObjectInstance);
        decimal maximum = MoneyBounds.Max(DollarMaximum, currency);
        bool belowMinimum = ExclusiveMinimum ? amount <= 0m : amount < 0m;
        if (!belowMinimum && amount <= maximum)
        {
            return ValidationResult.Success;
        }

        string[]? members = validationContext.MemberName is { } member ? [member] : null;
        return new ValidationResult(Message(validationContext, currency, maximum), members);
    }

    private string CurrencyOf(object instance)
    {
        object? code = instance.GetType().GetProperty(CurrencyProperty)?.GetValue(instance);
        return SupportedCurrencies.CodeOrDefault(code as string);
    }

    private string Message(ValidationContext validationContext, string currency, decimal maximum)
    {
        string key = ErrorMessage ?? string.Empty;
        string template = key.Length > 0 && validationContext.GetService(typeof(IStringLocalizer)) is IStringLocalizer localizer
            ? localizer[key].Value
            : key;
        CultureInfo culture = CultureInfo.CurrentCulture;
        return LocalizerExtensions.FormatNamed(template, new Dictionary<string, object?>
        {
            ["min"] = MoneyFormatter.FormatWhole(0m, currency, culture),
            ["max"] = MoneyFormatter.FormatWhole(maximum, currency, culture)
        });
    }
}
