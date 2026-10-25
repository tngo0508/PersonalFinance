using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.Options;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Puts <see cref="CultureDecimalModelBinderProvider"/> just ahead of the framework's floating-point provider, so it
/// has the same precedence (after the body, header and services binders) and wins only for decimals. A post-configure
/// step, so the framework's providers are already there whatever order the services were added in.
/// </summary>
internal sealed class CultureDecimalModelBinderSetup : IPostConfigureOptions<MvcOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, MvcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        IModelBinderProvider floatingPoint = options.ModelBinderProviders
            .FirstOrDefault(provider => provider is FloatingPointTypeModelBinderProvider)
            ?? throw new InvalidOperationException("MVC's floating-point model binder provider was not found.");
        options.ModelBinderProviders.Insert(options.ModelBinderProviders.IndexOf(floatingPoint),
            new CultureDecimalModelBinderProvider());
    }
}
