using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Models.Analysis;


namespace PersonalFinance.Tests.Analysis;

/// <summary>
/// Presentation helpers for the Analysis page: the period label the empty state names even when the active source has
/// no summary for that month, and the pace, category and month-on-month figures the redesigned page reads.
/// </summary>
public sealed class AnalysisDashboardViewModelTests : IDisposable
{
    private readonly RequestCultureScope _culture = new("en-US");

    public void Dispose() => _culture.Dispose();

    [Fact]
    public void PeriodLabel_WhenMonthAndYearSelected_IsMonthNameAndYear()
    {
        var model = new AnalysisDashboardViewModel
        {
            SelectedYear = 2026,
            SelectedMonth = 3
        };

        Assert.Equal("March 2026", model.PeriodLabel);
    }

    [Fact]
    public void PeriodLabel_WhenOnlyYearSelected_IsYear()
    {
        var model = new AnalysisDashboardViewModel
        {
            SelectedYear = 2026,
            SelectedMonth = null
        };

        Assert.Equal("2026", model.PeriodLabel);
    }

    [Fact]
    public void PeriodLabel_WhenNoPeriodSelected_IsNull_SoTheViewSaysThisPeriod()
    {
        var model = new AnalysisDashboardViewModel();

        Assert.Null(model.PeriodLabel);
    }

    [Theory]
    [InlineData("Over Budget", "over")]
    [InlineData("Ahead of Schedule", "ahead")]
    [InlineData("On Track", "ok")]
    [InlineData("Behind Schedule", "ok")]
    [InlineData("", "ok")]
    public void PaceTone_ReadsTheVelocityStatus_SpendingSlowerThanTimeIsGood(string status, string expected)
    {
        Assert.Equal(expected, new AnalysisDashboardViewModel { VelocityStatus = status }.PaceTone);
    }

    [Theory]
    [InlineData("Over Budget", "over")]
    [InlineData("Near Limit", "near")]
    [InlineData("Within Budget", "ok")]
    public void CategoryTone_ReadsTheCategoryStatus(string status, string expected)
    {
        Assert.Equal(expected, new CategoryInsightViewModel { Status = status }.Tone);
    }

    [Fact]
    public void IsVelocityMonthOpen_OnlyWhileDaysRemain()
    {
        Assert.True(new AnalysisDashboardViewModel { DaysRemaining = 0.5 }.IsVelocityMonthOpen);
        Assert.False(new AnalysisDashboardViewModel { DaysRemaining = 0 }.IsVelocityMonthOpen);
    }

    [Fact]
    public void CategoriesByAttention_PutsTheMostUsedBudgetFirst_ThenByName_AndSkipsEmptyCategories()
    {
        var model = new AnalysisDashboardViewModel
        {
            Categories =
            [
                new CategoryInsightViewModel { CategoryName = "Rent", PercentageUsed = 100, BudgetedAmount = 2100m },
                new CategoryInsightViewModel { CategoryName = "Food", PercentageUsed = 122.4, BudgetedAmount = 750m },
                new CategoryInsightViewModel { CategoryName = "Bills", PercentageUsed = 100, BudgetedAmount = 260m },
                new CategoryInsightViewModel { CategoryName = "Health", PercentageUsed = 64, ActualAmount = 96m },
                new CategoryInsightViewModel { CategoryName = "Gifts" }
            ]
        };

        Assert.Equal(["Food", "Bills", "Rent", "Health"], model.CategoriesByAttention.Select(c => c.CategoryName));
    }

    [Fact]
    public void CategoryScaleMax_IsTheLargestBudgetOrActual_AndZeroWithoutCategories()
    {
        var model = new AnalysisDashboardViewModel
        {
            Categories =
            [
                new CategoryInsightViewModel { BudgetedAmount = 750m, ActualAmount = 918m },
                new CategoryInsightViewModel { BudgetedAmount = 2100m, ActualAmount = 2100m },
                new CategoryInsightViewModel { BudgetedAmount = 300m, ActualAmount = 0m }
            ]
        };

        Assert.Equal(2100m, model.CategoryScaleMax);
        Assert.Equal(0m, new AnalysisDashboardViewModel().CategoryScaleMax);
    }

    [Fact]
    public void TrendPoints_ForTheSelectedMonth_FindItAndTheMonthBefore()
    {
        var model = new AnalysisDashboardViewModel
        {
            SelectedMonth = 3,
            TrendPoints =
            [
                new TrendPointViewModel { MonthNumber = 2, Income = 6500m },
                new TrendPointViewModel { MonthNumber = 3, Income = 6650m }
            ]
        };

        Assert.Equal(6650m, model.SelectedTrendPoint?.Income);
        Assert.Equal(6500m, model.PreviousTrendPoint?.Income);
    }

    [Fact]
    public void PreviousTrendPoint_IsNull_ForTheFirstMonthAndForAWholeYear()
    {
        List<TrendPointViewModel> points = [new TrendPointViewModel { MonthNumber = 1 }, new TrendPointViewModel { MonthNumber = 2 }];

        Assert.Null(new AnalysisDashboardViewModel { SelectedMonth = 1, TrendPoints = points }.PreviousTrendPoint);
        Assert.Null(new AnalysisDashboardViewModel { SelectedMonth = null, TrendPoints = points }.PreviousTrendPoint);
    }

    [Fact]
    public void ShortMonthName_FollowsTheCulture_AndIsEmptyOutsideTheYear()
    {
        Assert.Equal("Mar", AnalysisText.ShortMonthName(3));
        Assert.Equal(string.Empty, AnalysisText.ShortMonthName(13));
    }
}
