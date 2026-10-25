using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Routes the eleven model-binding messages ("The value 'abc' is not valid for Amount.") through the localizer
/// (spec §2.1). The framework formats them, so their placeholders are positional. Each accessor runs per request, so the
/// message follows the request's UI culture.
/// </summary>
internal sealed class ModelBindingMessagesSetup : IConfigureOptions<MvcOptions>
{
    private readonly IStringLocalizer _localizer;

    public ModelBindingMessagesSetup(IStringLocalizer localizer)
    {
        _localizer = localizer;
    }

    /// <inheritdoc />
    public void Configure(MvcOptions options)
    {
        DefaultModelBindingMessageProvider messages = options.ModelBindingMessageProvider;
        messages.SetMissingBindRequiredValueAccessor(name => _localizer["Validation.Binding.MissingBindRequiredValue", name]);
        messages.SetMissingKeyOrValueAccessor(() => _localizer["Validation.Binding.MissingKeyOrValue"]);
        messages.SetMissingRequestBodyRequiredValueAccessor(() => _localizer["Validation.Binding.MissingRequestBodyRequiredValue"]);
        messages.SetValueMustNotBeNullAccessor(value => _localizer["Validation.Binding.ValueMustNotBeNull", value]);
        messages.SetAttemptedValueIsInvalidAccessor((value, name) =>
            _localizer["Validation.Binding.AttemptedValueIsInvalid", value, name]);
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value =>
            _localizer["Validation.Binding.NonPropertyAttemptedValueIsInvalid", value]);
        messages.SetUnknownValueIsInvalidAccessor(name => _localizer["Validation.Binding.UnknownValueIsInvalid", name]);
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => _localizer["Validation.Binding.NonPropertyUnknownValueIsInvalid"]);
        messages.SetValueIsInvalidAccessor(value => _localizer["Validation.Binding.ValueIsInvalid", value]);
        messages.SetValueMustBeANumberAccessor(name => _localizer["Validation.Binding.ValueMustBeANumber", name]);
        messages.SetNonPropertyValueMustBeANumberAccessor(() => _localizer["Validation.Binding.NonPropertyValueMustBeANumber"]);
    }
}
