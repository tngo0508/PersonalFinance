namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The company lookup panel (_CompanyPanel) on the Company page. Each lookup keeps its ticker in the path
/// (<c>/Research/Company/MSFT</c>).
/// </summary>
/// <param name="Ticker">The ticker to look up as the panel opens (checked, upper case), or null.</param>
/// <param name="Query">The search text to pre-fill with its suggestions open (checked), or null.</param>
public sealed record ResearchCompanyPanelViewModel(string? Ticker, string? Query);
