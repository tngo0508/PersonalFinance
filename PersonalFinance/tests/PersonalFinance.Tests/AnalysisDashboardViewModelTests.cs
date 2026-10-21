using PersonalFinance.Web.Models;

namespace PersonalFinance.Tests;

/// <summary>
/// Presentation labels for the Analysis empty state, which must name the requested period
/// even when the active source has no summary for that month.
/// </summary>
public class AnalysisDashboardViewModelTests
{
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
    public void PeriodLabel_WhenNoPeriodSelected_IsThisPeriod()
    {
        var model = new AnalysisDashboardViewModel();

        Assert.Equal("this period", model.PeriodLabel);
    }
}
