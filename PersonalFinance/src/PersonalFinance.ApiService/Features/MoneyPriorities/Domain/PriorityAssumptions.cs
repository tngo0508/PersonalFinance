namespace PersonalFinance.ApiService.Features.MoneyPriorities.Domain;

/// <summary>
/// The fixed, stated assumptions behind every Money Priorities figure (#303). All rates are nominal yearly percentages
/// (7 is 7%). Contributions arrive at month end and compound monthly; taxes, fees and inflation are not modelled. The
/// page states each one under the result.
/// </summary>
internal static class PriorityAssumptions
{
    /// <summary>The base investment return, also what a debt's APR and an employer match are compared against.</summary>
    public const decimal BaseReturnPercent = 7m;

    /// <summary>The conservative investment return.</summary>
    public const decimal ConservativeReturnPercent = 4m;

    /// <summary>The aggressive investment return.</summary>
    public const decimal AggressiveReturnPercent = 10m;

    /// <summary>The yield on high-yield savings or short-term Treasuries (L4).</summary>
    public const decimal CashYieldPercent = 3.5m;

    /// <summary>The yearly rate of <paramref name="scenario"/>.</summary>
    public static decimal RatePercent(GrowthScenario scenario) => scenario switch
    {
        GrowthScenario.Cash => CashYieldPercent,
        GrowthScenario.Conservative => ConservativeReturnPercent,
        GrowthScenario.Base => BaseReturnPercent,
        GrowthScenario.Aggressive => AggressiveReturnPercent,
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown growth scenario.")
    };
}
