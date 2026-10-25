using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.Constants;

/// <summary>
/// The IRS retirement contribution limits for one tax year, as published in that year's IRS notice. Money Priorities
/// states these figures; it never fetches them. Add a row each November when the IRS announces the next year
/// (research: <c>docs/research/irs-contribution-limits.md</c> on the <c>research/irs-contribution-limits</c> branch).
/// </summary>
public sealed record RetirementContributionLimits
{
    /// <summary>One row per tax year, oldest first.</summary>
    public static IReadOnlyList<RetirementContributionLimits> All { get; } =
    [
        new RetirementContributionLimits
        {
            TaxYear = 2026,
            EmployeeDeferral = 24_500m,
            CatchUp50 = 8_000m,
            CatchUp60To63 = 11_250m,
            Ira = 7_500m,
            IraCatchUp50 = 1_100m,
            RothPhaseOutSingleStart = 153_000m,
            RothPhaseOutSingleEnd = 168_000m,
            RothPhaseOutJointStart = 242_000m,
            RothPhaseOutJointEnd = 252_000m,
            SourceNotice = "Notice 2025-67",
            SourceUrl = "https://www.irs.gov/pub/irs-drop/n-25-67.pdf"
        }
    ];

    /// <summary>The newest row in the table.</summary>
    public static RetirementContributionLimits Latest => All[^1];

    /// <summary>The tax year these limits apply to.</summary>
    public required int TaxYear { get; init; }

    /// <summary>401(k), 403(b), governmental 457(b) and TSP employee deferral limit.</summary>
    public required decimal EmployeeDeferral { get; init; }

    /// <summary>Plan catch-up for age 50 and over, outside the 60 to 63 window.</summary>
    public required decimal CatchUp50 { get; init; }

    /// <summary>Higher plan catch-up for ages 60 to 63 (SECURE 2.0). It replaces <see cref="CatchUp50"/>, it is not added to it.</summary>
    public required decimal CatchUp60To63 { get; init; }

    /// <summary>IRA contribution limit, traditional and Roth combined.</summary>
    public required decimal Ira { get; init; }

    /// <summary>IRA catch-up for age 50 and over.</summary>
    public required decimal IraCatchUp50 { get; init; }

    /// <summary>Start of the Roth IRA MAGI phase-out for single and head of household filers.</summary>
    public required decimal RothPhaseOutSingleStart { get; init; }

    /// <summary>End of the Roth IRA MAGI phase-out for single and head of household filers.</summary>
    public required decimal RothPhaseOutSingleEnd { get; init; }

    /// <summary>Start of the Roth IRA MAGI phase-out for married couples filing jointly.</summary>
    public required decimal RothPhaseOutJointStart { get; init; }

    /// <summary>End of the Roth IRA MAGI phase-out for married couples filing jointly.</summary>
    public required decimal RothPhaseOutJointEnd { get; init; }

    /// <summary>The IRS notice the figures come from, e.g. "Notice 2025-67".</summary>
    public required string SourceNotice { get; init; }

    /// <summary>The URL of that notice on irs.gov.</summary>
    public required string SourceUrl { get; init; }

    /// <summary>
    /// The row for <paramref name="taxYear"/>, or <see cref="Latest"/> when the table has no row for it (for example in
    /// January, before the new year's figures are entered). Callers show <see cref="TaxYear"/> so the fallback is visible.
    /// </summary>
    public static RetirementContributionLimits ForYear(int taxYear) =>
        All.FirstOrDefault(row => row.TaxYear == taxYear) ?? Latest;

    /// <summary>
    /// The 401(k) employee deferral limit for <paramref name="ageBand"/>, catch-up included: the 60 to 63 catch-up
    /// replaces the age-50 one.
    /// </summary>
    public decimal EmployeeDeferralFor(RetirementAgeBand ageBand) => ageBand switch
    {
        RetirementAgeBand.Under50 => EmployeeDeferral,
        RetirementAgeBand.Age50To59Or64Up => EmployeeDeferral + CatchUp50,
        RetirementAgeBand.Age60To63 => EmployeeDeferral + CatchUp60To63,
        _ => throw new ArgumentOutOfRangeException(nameof(ageBand), ageBand, "Unknown age band.")
    };

    /// <summary>The IRA contribution limit for <paramref name="ageBand"/>, catch-up included.</summary>
    public decimal IraFor(RetirementAgeBand ageBand) => ageBand switch
    {
        RetirementAgeBand.Under50 => Ira,
        RetirementAgeBand.Age50To59Or64Up or RetirementAgeBand.Age60To63 => Ira + IraCatchUp50,
        _ => throw new ArgumentOutOfRangeException(nameof(ageBand), ageBand, "Unknown age band.")
    };
}
