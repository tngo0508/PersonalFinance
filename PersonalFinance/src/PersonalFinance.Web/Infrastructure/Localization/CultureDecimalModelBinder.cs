using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Binds <c>decimal</c> and <c>decimal?</c> with <see cref="CultureDecimalParser"/>, in the value provider's culture:
/// the request culture for posted forms (<c>1.234,56</c> in vi-VN), the invariant culture for query strings and route
/// values. Replaces the framework's lenient binder, which reads <c>1234.56</c> as 123456 in vi-VN. JSON bodies are
/// read by the input formatter, not here.
/// </summary>
internal sealed class CultureDecimalModelBinder : IModelBinder
{
    /// <inheritdoc />
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        ValueProviderResult result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (result == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);
        string? text = result.FirstValue;
        ModelMetadata metadata = bindingContext.ModelMetadata;

        if (string.IsNullOrWhiteSpace(text))
        {
            if (metadata.IsReferenceOrNullableType)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }
            else
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName,
                    metadata.ModelBindingMessageProvider.ValueMustNotBeNullAccessor(text ?? string.Empty));
            }

            return Task.CompletedTask;
        }

        if (CultureDecimalParser.TryParse(text, result.Culture, out decimal value))
        {
            bindingContext.Result = ModelBindingResult.Success(value);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName,
                metadata.ModelBindingMessageProvider.AttemptedValueIsInvalidAccessor(text,
                    metadata.GetDisplayName()));
        }

        return Task.CompletedTask;
    }
}
