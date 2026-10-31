using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Research Home page: when each user-facing dataset's source data is as of, and the Fund Basket's starting mixes.
/// It holds no dataset itself: only the status, so the page renders without reading any snapshot.
/// </summary>
public sealed class ResearchHomeViewModel
{
    /// <summary>
    /// The stored keys of the datasets the freshness list shows, in its order. They mirror
    /// <c>PersonalFinance.ApiService/Features/Research/ResearchDatasets.cs</c>, which Web cannot reference; the
    /// internal datasets the status also lists (the SEC filers, the fund filings, the exchange rates, ...) are left out.
    /// </summary>
    public static readonly string[] DatasetKeys =
        ["sectors", "etfs", "fund-returns", "fund-facts", "fund-overlap", "fund-exposure", "world", "macro"];

    /// <summary>Builds the page from <paramref name="status"/>, or with every dataset not loaded when it is null.</summary>
    /// <param name="status">The datasets' status, or null when it could not be read.</param>
    public ResearchHomeViewModel(ResearchStatusDto? status)
    {
        Dictionary<string, DateOnly> asOf = (status?.Datasets ?? [])
            .GroupBy(d => d.Dataset, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().SourceAsOf, StringComparer.Ordinal);
        Datasets = DatasetKeys
            .Select(key => new ResearchDatasetFreshness(key, LabelKeyOf(key),
                asOf.TryGetValue(key, out DateOnly date) ? date : null))
            .ToList();
    }

    /// <summary>One row per user-facing dataset, in <see cref="DatasetKeys"/> order.</summary>
    public IReadOnlyList<ResearchDatasetFreshness> Datasets { get; }

    /// <summary>The Fund Basket's starting mixes.</summary>
    public IReadOnlyList<ResearchBasketPreset> Presets { get; } = ResearchBasketPresets.All;

    /// <summary>The resource key of a dataset's name: <c>fund-returns</c> gives <c>Research.Home.Dataset.FundReturns</c>.</summary>
    private static string LabelKeyOf(string key) =>
        "Research.Home.Dataset." + string.Concat(key.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
