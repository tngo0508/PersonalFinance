namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// A starting mix for the Fund Basket, offered on the Research Home page.
/// </summary>
/// <param name="LabelKey">The resource key of the mix's name.</param>
/// <param name="Funds">Each fund's ticker and whole-percent weight, adding up to 100.</param>
public sealed record ResearchBasketPreset(string LabelKey, IReadOnlyList<(string Ticker, int Weight)> Funds)
{
    /// <summary>The mix as the address writes a Fund Basket, e.g. <c>VTI:60,VXUS:30,BND:10</c>.</summary>
    public string Basket => string.Join(',', Funds.Select(f => $"{f.Ticker}:{f.Weight}"));
}
