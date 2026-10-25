using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Primitives;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Makes the formatting culture follow the UI language (spec §2.2): whatever a provider reads (<c>vi</c>, <c>vi-VN</c>,
/// a cookie pairing <c>vi</c> with <c>en-US</c>), the request gets the first supported language and that language's
/// <see cref="AppLanguage.FormattingCulture"/>, so separators, dates and typed input always match the page language.
/// Returns <c>null</c> when the inner provider names no supported language, so the next provider decides.
/// </summary>
internal sealed class LanguageCultureProvider : IRequestCultureProvider
{
    private readonly IRequestCultureProvider _inner;

    public LanguageCultureProvider(IRequestCultureProvider inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc />
    public async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ProviderCultureResult? result = await _inner.DetermineProviderCultureResult(httpContext);
        if (result is null)
        {
            return null;
        }

        foreach (StringSegment candidate in result.UICultures)
        {
            AppLanguage? language = AppLanguages.Find(LanguagePart(candidate));
            if (language is not null)
            {
                return new ProviderCultureResult(language.FormattingCulture, language.Code);
            }
        }

        return null;
    }

    private static string LanguagePart(StringSegment culture)
    {
        int dash = culture.IndexOf('-');
        return (dash < 0 ? culture : culture.Subsegment(0, dash)).ToString();
    }
}
