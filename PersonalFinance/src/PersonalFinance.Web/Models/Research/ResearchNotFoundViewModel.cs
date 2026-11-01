using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Research "not found" page (status 404): a fund the address names is not a Tracked Fund, or its ticker was
/// rejected. It offers the search box and the list of every fund.
/// </summary>
public sealed class ResearchNotFoundViewModel
{
    /// <summary>The ticker as checked (upper case); null when it was rejected, so the page never repeats raw input.</summary>
    public string? Ticker { get; init; }

    /// <summary>
    /// The funds data's Unavailable State when it could not be read, so the page cannot tell whether the fund is
    /// tracked; null when it was read.
    /// </summary>
    public UnavailableStateViewModel? FundsUnavailableState { get; init; }
}
