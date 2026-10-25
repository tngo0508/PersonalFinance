using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.WebEncoders;
using PersonalFinance.Shared.Constants;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Registers the JSON localizer and request localization (ADR 0015, spec §2.1–2.3). Call before
/// <c>AddViewLocalization()</c> and <c>AddDataAnnotationsLocalization()</c>: they <c>TryAdd</c> the <c>.resx</c>
/// factory, which must find this one already registered.
/// </summary>
internal static class LocalizationServiceExtensions
{
    /// <summary>Adds the catalog, the localizer, request localization, the encoder and the model-binding messages.</summary>
    public static IServiceCollection AddAppLocalization(this IServiceCollection services, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        string resourcesPath = Path.Combine(environment.ContentRootPath, JsonLocalizationCatalog.ResourcesFolder);
        services.AddSingleton(new JsonLocalizationCatalog(resourcesPath, AppLanguages.All.Select(l => l.Code)));
        services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
        // One shared key space, so views and services can inject the non-generic localizer.
        services.AddSingleton<IStringLocalizer>(sp =>
            sp.GetRequiredService<IStringLocalizerFactory>().Create(typeof(LocalizationServiceExtensions)));
        services.AddSingleton<IConfigureOptions<MvcOptions>, ModelBindingMessagesSetup>();

        // Razor HTML-encodes every non-ASCII character by default ("C&#xF2;n"); let Vietnamese through as UTF-8.
        // <, >, & and quotes are still encoded.
        services.Configure<WebEncoderOptions>(options =>
            options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

        // The UI language comes from the culture cookie, then Accept-Language, then English. There is no query-string
        // provider. The formatting culture is always the language's own (vi -> vi-VN), whatever the provider read.
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture(AppCulture.DefaultCultureName, AppLanguages.Default);
            options.SupportedCultures = AppLanguages.SupportedCultures;
            options.SupportedUICultures = AppLanguages.SupportedUICultures;
            options.RequestCultureProviders =
            [
                new LanguageCultureProvider(new CookieRequestCultureProvider()),
                new LanguageCultureProvider(new AcceptLanguageHeaderRequestCultureProvider())
            ];
        });

        return services;
    }
}
