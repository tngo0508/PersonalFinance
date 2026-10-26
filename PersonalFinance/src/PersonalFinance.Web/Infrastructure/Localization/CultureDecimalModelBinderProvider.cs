using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>Chooses <see cref="CultureDecimalModelBinder"/> for <c>decimal</c> and <c>decimal?</c>.</summary>
internal sealed class CultureDecimalModelBinderProvider : IModelBinderProvider
{
    private static readonly CultureDecimalModelBinder Binder = new();

    /// <inheritdoc />
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Metadata.UnderlyingOrModelType == typeof(decimal) ? Binder : null;
    }
}
