using MediatR;
using PersonalFinance.ApiService.SpreadsheetAnalysis.Services;

namespace PersonalFinance.ApiService.SpreadsheetAnalysis;

/// <summary>
///     Query to get the full analysis dashboard data for a selected period.
/// </summary>
public sealed class GetAnalysisDashboardQuery : IRequest<AnalysisDashboardResult>
{
    public string UserId { get; set; } = string.Empty;
    public int Year { get; set; }
    public int? Month { get; set; }
}

/// <summary>
///     Result containing all dashboard data.
/// </summary>
public sealed class AnalysisDashboardResult
{
    public bool HasData { get; set; }
    public int Year { get; set; }
    public int? Month { get; set; }
    public List<int> AvailableYears { get; set; } = new();
    public List<int> AvailableMonths { get; set; } = new();
    public SummaryStatisticsDto? Summary { get; set; }
    public SpendingVelocityDto? Velocity { get; set; }
    public TrendDataDto? Trends { get; set; }
    public CategoryInsightsDto? Categories { get; set; }
    public BudgetVarianceDto? Variance { get; set; }
}

/// <summary>
///     Handler for GetAnalysisDashboardQuery.
/// </summary>
public sealed class
    GetAnalysisDashboardQueryHandler : IRequestHandler<GetAnalysisDashboardQuery, AnalysisDashboardResult>
{
    private readonly ISpreadsheetAnalysisService _analysisService;

    public GetAnalysisDashboardQueryHandler(ISpreadsheetAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    public async Task<AnalysisDashboardResult> Handle(GetAnalysisDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var availableYears = await _analysisService.GetAvailableYearsAsync(request.UserId, cancellationToken);

        if (!availableYears.Any())
            return new AnalysisDashboardResult
            {
                HasData = false,
                Year = request.Year,
                AvailableYears = availableYears
            };

        // Default to most recent year if not specified
        var year = request.Year != 0 ? request.Year : availableYears.First();
        var availableMonths = await _analysisService.GetAvailableMonthsAsync(request.UserId, year, cancellationToken);

        var summary =
            await _analysisService.GetSummaryStatisticsAsync(request.UserId, year, request.Month, cancellationToken);
        var velocity =
            await _analysisService.GetSpendingVelocityAsync(request.UserId, year, request.Month, cancellationToken);
        var trends = await _analysisService.GetTrendDataAsync(request.UserId, year, cancellationToken);
        var categories =
            await _analysisService.GetCategoryInsightsAsync(request.UserId, year, request.Month, cancellationToken);
        var variance =
            await _analysisService.GetBudgetVarianceAsync(request.UserId, year, request.Month, cancellationToken);

        return new AnalysisDashboardResult
        {
            HasData = true,
            Year = year,
            Month = request.Month,
            AvailableYears = availableYears,
            AvailableMonths = availableMonths,
            Summary = summary,
            Velocity = velocity,
            Trends = trends,
            Categories = categories,
            Variance = variance
        };
    }
}