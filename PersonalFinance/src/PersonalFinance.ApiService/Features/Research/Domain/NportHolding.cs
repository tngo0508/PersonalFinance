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
internal sealed record NportHolding(string Name, string AssetCategory, decimal Percent);
