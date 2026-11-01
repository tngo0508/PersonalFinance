using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.Web.Models.Research;

/// <summary>
/// The holdings list (<c>_FundHoldingsList</c>): what the Tracked Funds hold in one quarter, fund by fund, then by N-PORT
/// asset category, then each category's largest holdings and "Other (n)". Each holding links to the company search by
/// its name, never by a security identifier (ADR 0010). The Funds page draws it with a quarter picker.
/// </summary>
public sealed class FundHoldingsListViewModel
{
    /// <summary>The quarter the list shows.</summary>
    public required FundsPeriodDto Period { get; init; }

    /// <summary>The Tracked Funds, in the snapshot's order, each listed even when it filed no report for the quarter.</summary>
    public required IReadOnlyList<TrackedFundDto> Funds { get; init; }

    /// <summary>The quarters a picker offers, oldest first; empty when the list has no picker.</summary>
    public IReadOnlyList<string> Quarters { get; init; } = [];

    /// <summary>
    /// The query of the company search a holding links to: <c>?q=</c> and the holding's name, escaped. The name is all
    /// the link carries, so no CUSIP or ISIN ever reaches an address.
    /// </summary>
    /// <param name="holdingName">The holding's name as the fund reported it, e.g. <c>NVIDIA CORP</c>.</param>
    public static string CompanySearchQuery(string holdingName) =>
        "?q=" + Uri.EscapeDataString(holdingName.Trim());

    /// <summary>The fund's report for the quarter, or null when it filed none.</summary>
    public FundHoldingsDto? ReportOf(string ticker) =>
        Period.Funds.FirstOrDefault(f => string.Equals(f.Ticker, ticker, StringComparison.OrdinalIgnoreCase));
}
