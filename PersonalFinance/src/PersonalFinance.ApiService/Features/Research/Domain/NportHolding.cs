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
/// True for cash collateral the fund received for securities it lent (<c>securityLending/isCashCollateral</c>, or the
/// <c>isCashCollateral</c> attribute of <c>cashCollateralCondition</c>): not an investment choice, so it is left out of
/// the overlap between funds and of their exposure.
/// </param>
/// <param name="Country">
/// The ISO alpha-2 code of the country where the issuer is organized, as the fund reports it (<c>invCountry</c>, Item
/// C.5.a), or null when it gives N/A; not where the business earns its revenue.
/// </param>
/// <param name="Currency">
/// The ISO 4217 code of the currency the investment is priced in (<c>curCd</c>, Item C.2.b), or null; ADRs are USD.
/// </param>
/// <param name="IssuerCategory">
/// The N-PORT issuer type code (<c>issuerCat</c>, Item C.4.b), e.g. <c>CORP</c> or <c>UST</c>, or for a type N-PORT has
/// no code for <c>issuerConditional/@issuerCat</c>; null when the report gives none.
/// </param>
internal sealed record NportHolding(string Name, string AssetCategory, decimal Percent, string? SecurityKey = null,
    bool IsCashCollateral = false, string? Country = null, string? Currency = null, string? IssuerCategory = null)
{
    /// <summary>Hides <see cref="SecurityKey"/>, so a holding written to a log never shows the identifier.</summary>
    public override string ToString() =>
        $"NportHolding {{ Name = {Name}, AssetCategory = {AssetCategory}, Percent = {Percent} }}";
}
