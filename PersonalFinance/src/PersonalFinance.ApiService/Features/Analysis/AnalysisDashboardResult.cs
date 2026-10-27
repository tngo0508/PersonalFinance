using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.DTOs.Currency;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.Analysis;

/// <summary>
///     Result containing all dashboard data.
/// </summary>
public sealed class AnalysisDashboardResult
{
    /// <summary>The ISO 4217 code every amount is in: the Account's Reporting Currency (ADR 0016).</summary>
    public string ReportingCurrency { get; set; } = string.Empty;

    /// <summary>The rates that brought the period's months into the Reporting Currency; null when none was needed.</summary>
    public ConversionDto? Conversion { get; set; }

    public bool HasData { get; set; }
    public int Year { get; set; }
    public int? Month { get; set; }
    public List<int> AvailableYears { get; set; } = new();
    public List<int> AvailableMonths { get; set; } = new();
    public BudgetDataSource ActiveSource { get; set; } = BudgetDataSource.GoogleSheet;
    public List<BudgetDataSourceSummaryDto> SourceSummaries { get; set; } = new();
    public SummaryStatisticsDto? Summary { get; set; }
    public SpendingVelocityDto? Velocity { get; set; }
    public TrendDataDto? Trends { get; set; }
    public CategoryInsightsDto? Categories { get; set; }
    public BudgetVarianceDto? Variance { get; set; }
}
