using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// PROTOTYPE (#340, throwaway). DI registration, the <c>?lang=</c> demo toggle and the fetched JS bundle endpoint.
/// </summary>
public static class LocalizationPrototypeExtensions
{
    /// <summary>The UI languages the prototype supports. Formatting culture (<c>SupportedCultures</c>) stays en-US.</summary>
    public static readonly CultureInfo[] SupportedUICultures =
        [CultureInfo.GetCultureInfo("en"), CultureInfo.GetCultureInfo("vi")];

    /// <summary>Registers the JSON factory before <c>AddViewLocalization</c> so its <c>TryAdd</c> keeps ours.</summary>
    public static IServiceCollection AddJsonLocalizationPrototype(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<JsonStringLocalizerFactory>();
        services.AddSingleton<IStringLocalizerFactory>(sp => sp.GetRequiredService<JsonStringLocalizerFactory>());
        services.AddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
        // One shared key space, so views can inject the non-generic localizer.
        services.AddTransient<IStringLocalizer>(sp =>
            sp.GetRequiredService<IStringLocalizerFactory>().Create(typeof(LocalizationPrototypeExtensions)));
        return services;
    }

    /// <summary>
    /// <c>?lang=vi</c> writes the standard culture cookie (formatting stays en-US, UI culture switches) and redirects
    /// to the same URL without the parameter. Must run before <c>UseRequestLocalization</c>.
    /// </summary>
    public static IApplicationBuilder UseLanguageQueryToggle(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            string? lang = context.Request.Query["lang"];
            if (lang is "en" or "vi")
            {
                context.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture("en-US", lang)),
                    new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, HttpOnly = true,
                        SameSite = SameSiteMode.Lax
                    });
                Dictionary<string, Microsoft.Extensions.Primitives.StringValues> rest = context.Request.Query
                    .Where(q => q.Key != "lang").ToDictionary(q => q.Key, q => q.Value);
                string query = QueryString.Create(rest).ToUriComponent();
                context.Response.Redirect(context.Request.PathBase + context.Request.Path + query);
                return;
            }

            await next();
        });

    /// <summary>
    /// <c>GET /i18n/{culture}.{hash}.json</c>: every <c>*.Js.*</c> key for that culture, resolved through the fallback
    /// chain. The hash is part of the URL, so the response is immutable and cached for a year; a stale hash 404s.
    /// </summary>
    public static IEndpointRouteBuilder MapJsStringBundle(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/i18n/{culture}.{hash}.json",
            (string culture, string hash, JsonStringLocalizerFactory factory, HttpContext context) =>
            {
                if (!SupportedUICultures.Any(c => c.Name == culture))
                {
                    return Results.NotFound();
                }

                Dictionary<string, string> bundle = JsBundle(factory, CultureInfo.GetCultureInfo(culture));
                if (JsonStringLocalizerFactory.Hash(bundle) != hash)
                {
                    return Results.NotFound();
                }

                context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                return Results.Json(bundle);
            }).AllowAnonymous();
        return endpoints;
    }

    /// <summary>The whole-app JS bundle for a culture: every key with a <c>.Js.</c> segment.</summary>
    public static Dictionary<string, string> JsBundle(JsonStringLocalizerFactory factory, CultureInfo culture) =>
        factory.Resolve(culture, string.Empty)
            .Where(p => p.Key.Contains(".Js.", StringComparison.Ordinal))
            .ToDictionary(p => p.Key, p => p.Value);
}
