namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// One Research dataset on the Home page's freshness list: what it is and the date its source data is as of.
/// </summary>
/// <param name="Dataset">The dataset's stored key, e.g. <c>fund-returns</c>.</param>
/// <param name="LabelKey">The resource key of the dataset's name and source.</param>
/// <param name="AsOf">The date the source data is as of; null when the dataset has not loaded.</param>
public sealed record ResearchDatasetFreshness(string Dataset, string LabelKey, DateOnly? AsOf);
