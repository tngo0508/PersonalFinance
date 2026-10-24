namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// One investment of a Form N-PORT report (<c>invstOrSec</c>).
/// </summary>
/// <param name="Name">The issuer's name, or the investment's title when the issuer has none.</param>
/// <param name="AssetCategory">
/// The N-PORT asset category code, e.g. <c>EC</c> (common equity); <c>OTHER</c> when the report gives none.
/// </param>
/// <param name="Percent">
/// The investment's value as a percentage of the fund's net assets (<c>pctVal</c>); negative for a short position.
/// </param>
/// <param name="SecurityKey">
/// The security's ISIN, or its CUSIP when it has no ISIN, or null when the report gives neither: what matches the
/// same security across funds. It is used only in memory during a refresh and is never stored, logged or shown
/// (ADR 0010: Research never stores or shows security identifiers).
/// </param>
/// <param name="IsCashCollateral">
/// True for cash collateral the fund received for securities it lent (<c>securityLending/isCashCollateral</c>):
/// not an investment choice, so it is left out of the overlap between funds.
/// </param>
internal sealed record NportHolding(string Name, string AssetCategory, decimal Percent, string? SecurityKey = null,
    bool IsCashCollateral = false)
{
    /// <summary>Hides <see cref="SecurityKey"/>, so a holding written to a log never shows the identifier.</summary>
    public override string ToString() =>
        $"NportHolding {{ Name = {Name}, AssetCategory = {AssetCategory}, Percent = {Percent} }}";
}
