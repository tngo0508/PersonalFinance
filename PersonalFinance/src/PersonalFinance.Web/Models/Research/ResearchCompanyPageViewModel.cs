namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The Research Company page: the company lookup at <c>/Research/Company/{ticker?}</c>. It reads no snapshot; the page
/// fetches the profile, its press releases and the suggestions itself.
/// </summary>
/// <param name="Ticker">The ticker to look up as the page opens (checked, upper case); null for an empty search.</param>
/// <param name="Query">The search text to pre-fill with its suggestions open (checked); null for none.</param>
public sealed record ResearchCompanyPageViewModel(string? Ticker, string? Query)
{
    /// <summary>The lookup panel, shared with the tabbed page's Companies tab.</summary>
    public ResearchCompanyPanelViewModel Panel => new(Ticker, Query, InTab: false, Hidden: false);
}
