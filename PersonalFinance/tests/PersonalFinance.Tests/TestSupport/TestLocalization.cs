using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>
/// The Web project's real translations (<c>Resources/i18n/*.json</c>), for tests that construct a controller or
/// service needing an <see cref="IStringLocalizer"/>. Lookups follow <c>CultureInfo.CurrentUICulture</c>.
/// </summary>
internal static class TestLocalization
{
    public static JsonLocalizationCatalog Catalog { get; } = new(
        Path.Combine(RepositoryRoot.SourceProject("PersonalFinance.Web"), JsonLocalizationCatalog.ResourcesFolder),
        AppLanguages.All.Select(language => language.Code));

    public static IStringLocalizer Localizer { get; } = new JsonStringLocalizer(Catalog, NullLogger.Instance);
}
