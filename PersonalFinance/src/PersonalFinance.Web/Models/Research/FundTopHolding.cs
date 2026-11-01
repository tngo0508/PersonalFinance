namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// One of a fund's largest holdings on the Fund page, linked to the company search by its name (ADR 0010: never by an
/// identifier).
/// </summary>
/// <param name="Name">The holding's name as the fund reported it.</param>
/// <param name="Percent">Its share of the fund's net assets, in percent.</param>
/// <param name="Category">The N-PORT asset category it sits in, e.g. <c>Equity, common</c>.</param>
public sealed record FundTopHolding(string Name, decimal Percent, string Category);
