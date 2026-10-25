using Microsoft.Extensions.Localization;

namespace PersonalFinance.Web.Infrastructure.Localization;

/// <summary>
/// Hands out <see cref="JsonStringLocalizer"/>s over the one <see cref="JsonLocalizationCatalog"/>. The base name and
/// location MVC passes in (one per view or type) are ignored: Razor, DataAnnotations, model binding and JS share one key
/// space (spec §2.1).
/// </summary>
internal sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly JsonStringLocalizer _localizer;

    public JsonStringLocalizerFactory(JsonLocalizationCatalog catalog, ILogger<JsonStringLocalizer> logger)
    {
        // Stateless apart from the catalog, so one instance serves every caller.
        _localizer = new JsonStringLocalizer(catalog, logger);
    }

    /// <inheritdoc />
    public IStringLocalizer Create(Type resourceSource) => _localizer;

    /// <inheritdoc />
    public IStringLocalizer Create(string baseName, string location) => _localizer;
}
