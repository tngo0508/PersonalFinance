using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.MoneyPriorities;

/// <summary>
/// The per-tax-year IRS limits table. Expected figures are the spec's 2026 figures (Notice 2025-67), not recomputed.
/// </summary>
public class RetirementContributionLimitsTests
{
    [Fact]
    public void ForYear2026_HoldsNotice2025_67Figures()
    {
        RetirementContributionLimits limits = RetirementContributionLimits.ForYear(2026);

        Assert.Equal(2026, limits.TaxYear);
        Assert.Equal(24_500m, limits.EmployeeDeferral);
        Assert.Equal(8_000m, limits.CatchUp50);
        Assert.Equal(11_250m, limits.CatchUp60To63);
        Assert.Equal(7_500m, limits.Ira);
        Assert.Equal(1_100m, limits.IraCatchUp50);
        Assert.Equal("Notice 2025-67", limits.SourceNotice);
        Assert.Equal("https://www.irs.gov/pub/irs-drop/n-25-67.pdf", limits.SourceUrl);
    }

    [Fact]
    public void Table_HasARowForTheCurrentOrPrecedingTaxYear()
    {
        // Fails in the first week of the year after next if nobody adds the new IRS figures each November.
        int currentYear = DateTime.UtcNow.Year;

        Assert.Contains(RetirementContributionLimits.All,
            row => row.TaxYear == currentYear || row.TaxYear == currentYear - 1);
    }

    [Theory]
    [InlineData(2099)]
    [InlineData(1999)]
    public void ForYear_WithoutARow_FallsBackToTheLatestRow(int taxYear)
    {
        RetirementContributionLimits limits = RetirementContributionLimits.ForYear(taxYear);

        Assert.Same(RetirementContributionLimits.Latest, limits);
        Assert.Equal(RetirementContributionLimits.All.Max(row => row.TaxYear), limits.TaxYear);
    }

    [Fact]
    public void Table_IsOrderedOldestFirst_OneRowPerYear()
    {
        List<int> years = RetirementContributionLimits.All.Select(row => row.TaxYear).ToList();

        Assert.Equal(years.Order().Distinct().ToList(), years);
    }

    [Theory]
    [InlineData(RetirementAgeBand.Under50, 24_500, 7_500)]
    [InlineData(RetirementAgeBand.Age50To59Or64Up, 32_500, 8_600)]
    [InlineData(RetirementAgeBand.Age60To63, 35_750, 8_600)]
    public void ForYear2026_LimitsByAgeBand_MatchTheSpec(RetirementAgeBand ageBand, int employeeDeferral, int ira)
    {
        RetirementContributionLimits limits = RetirementContributionLimits.ForYear(2026);

        Assert.Equal((decimal)employeeDeferral, limits.EmployeeDeferralFor(ageBand));
        Assert.Equal((decimal)ira, limits.IraFor(ageBand));
    }

    [Fact]
    public void ForYear2026_RothPhaseOut_MatchesTheSpecFootnote()
    {
        RetirementContributionLimits limits = RetirementContributionLimits.ForYear(2026);

        Assert.Equal(153_000m, limits.RothPhaseOutSingleStart);
        Assert.Equal(168_000m, limits.RothPhaseOutSingleEnd);
        Assert.Equal(242_000m, limits.RothPhaseOutJointStart);
        Assert.Equal(252_000m, limits.RothPhaseOutJointEnd);
    }
}
