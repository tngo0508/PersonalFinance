namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The company lookup panel (_CompanyPanel), shown on the Company page and, until the redesign goes live, as the tabbed
/// page's Companies tab. On the page each lookup keeps its ticker in the path (<c>/Research/Company/MSFT</c>); in the
/// tab, in the query (<c>?tab=companies&amp;ticker=MSFT</c>).
/// </summary>
/// <param name="Ticker">The ticker to look up as the panel opens (checked, upper case), or null.</param>
/// <param name="Query">The search text to pre-fill with its suggestions open (checked), or null.</param>
/// <param name="InTab">True in the tabbed page's Companies tab.</param>
/// <param name="Hidden">True while another tab is chosen.</param>
public sealed record ResearchCompanyPanelViewModel(string? Ticker, string? Query, bool InTab, bool Hidden);
