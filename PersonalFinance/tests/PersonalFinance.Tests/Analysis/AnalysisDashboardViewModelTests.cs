using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Models.Analysis;


namespace PersonalFinance.Tests.Analysis;

/// <summary>
/// Presentation labels for the Analysis empty state, which must name the requested period
/// even when the active source has no summary for that month.
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
}
