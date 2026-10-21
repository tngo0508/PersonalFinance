using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Controllers;

/// <summary>
///     MVC Controller for the Spreadsheet Analysis feature.
///     Provides dashboard views, sync operations, and data endpoints.
/// </summary>
[Authorize]
public class AnalysisController : Controller
{
    private readonly IPersonalFinanceApi _api;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(IPersonalFinanceApi api, ILogger<AnalysisController> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <summary>
    ///     Displays the full analysis dashboard.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken = default)
    {
        var result = await _api.GetAnalysisDashboardAsync(year, month, cancellationToken);

        var model = MapToViewModel(result);
        model.StatusMessage = TempData["StatusMessage"] as string;
        model.ErrorMessage = TempData["ErrorMessage"] as string;

        return View(model);
    }

    /// <summary>
    ///     Triggers spreadsheet sync from Google Drive.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(
        int connectionId,
        string fileId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User requested sync for spreadsheet {FileId}", fileId);

        var request = new SyncSpreadsheetRequestDto
        {
            ConnectionId = connectionId,
            FileId = fileId,
            FileName = fileName,
            Overwrite = false
        };

        var result = await _api.SyncSpreadsheetAsync(request, cancellationToken);

        if (result.HasConflict)
        {
            var conflictModel = new SyncConfirmationViewModel
            {
                ConnectionId = connectionId,
                FileId = fileId,
                FileName = fileName,
                Conflicts = result.Conflicts
            };
            return View("SyncConflict", conflictModel);
        }

        if (result.Success)
            TempData["StatusMessage"] =
                $"Successfully synced {result.SnapshotsCreated} month(s) and {result.TransactionsCreated} transactions.";
        else
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Sync failed. Please try again.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    ///     Confirms overwrite of existing month data during sync.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmOverwrite(
        int connectionId,
        string fileId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User confirmed overwrite for spreadsheet {FileId}", fileId);

        var request = new SyncSpreadsheetRequestDto
        {
            ConnectionId = connectionId,
            FileId = fileId,
            FileName = fileName,
            Overwrite = true
        };

        var result = await _api.SyncSpreadsheetAsync(request, cancellationToken);

        if (result.Success)
            TempData["StatusMessage"] =
                $"Successfully synced {result.SnapshotsCreated} month(s) and {result.TransactionsCreated} transactions (overwritten existing data).";
        else
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Sync failed. Please try again.";

        return RedirectToAction(nameof(Index));
    }

    private AnalysisDashboardViewModel MapToViewModel(AnalysisDashboardDto result)
    {
        var model = new AnalysisDashboardViewModel
        {
            HasData = result.HasData,
            SelectedYear = result.Year,
            SelectedMonth = result.Month,
            AvailableYears = result.AvailableYears,
            AvailableMonths = result.AvailableMonths,
            ActiveSource = result.ActiveSource
        };

        if (result.Summary != null)
        {
            model.TotalIncome = result.Summary.TotalIncome;
            model.TotalExpenses = result.Summary.TotalExpenses;
            model.NetSavings = result.Summary.NetSavings;
            model.SavingsRate = result.Summary.SavingsRate;
            model.MonthName = result.Summary.MonthName;
        }
        else if (result.Month is >= 1 and <= 12)
        {
            model.MonthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(result.Month.Value);
        }

        if (result.Velocity != null)
        {
            model.DailyBurnRate = result.Velocity.DailyBurnRate;
            model.ProjectedMonthlySpend = result.Velocity.ProjectedMonthlySpend;
            model.BudgetedExpenses = result.Velocity.BudgetedExpenses;
            model.DaysElapsed = result.Velocity.DaysElapsed;
            model.DaysRemaining = result.Velocity.DaysRemaining;
            model.BudgetConsumptionPercent = result.Velocity.BudgetConsumptionPercent;
            model.TimeElapsedPercent = result.Velocity.TimeElapsedPercent;
            model.VelocityStatus = result.Velocity.Status;
            model.WeeklyTrend = result.Velocity.WeeklyTrend
                .Select(w => new WeeklySpendingViewModel
                {
                    WeekLabel = w.WeekLabel,
                    TotalSpent = w.TotalSpent
                }).ToList();
            model.CategoryVelocities = result.Velocity.CategoryVelocities
                .Select(c => new CategoryVelocityViewModel
                {
                    CategoryName = c.CategoryName,
                    BudgetedAmount = c.BudgetedAmount,
                    ActualAmount = c.ActualAmount,
                    ConsumptionPercent = c.ConsumptionPercent,
                    TimeElapsedPercent = c.TimeElapsedPercent,
                    Status = c.Status
                }).ToList();
        }

        if (result.Trends != null)
            model.TrendPoints = result.Trends.TrendPoints
                .Select(t => new TrendPointViewModel
                {
                    MonthName = t.MonthName,
                    Income = t.Income,
                    Expenses = t.Expenses,
                    NetSavings = t.NetSavings,
                    SavingsRate = t.SavingsRate
                }).ToList();

        if (result.Categories != null)
            model.Categories = result.Categories.Categories
                .Select(c => new CategoryInsightViewModel
                {
                    CategoryName = c.CategoryName,
                    BudgetedAmount = c.BudgetedAmount,
                    ActualAmount = c.ActualAmount,
                    Variance = c.Variance,
                    PercentageUsed = c.PercentageUsed,
                    Status = c.Status
                }).ToList();

        if (result.Variance != null)
            model.VarianceRankings = result.Variance.Rankings
                .Select(v => new VarianceRankingViewModel
                {
                    Rank = v.Rank,
                    CategoryName = v.CategoryName,
                    BudgetedAmount = v.BudgetedAmount,
                    ActualAmount = v.ActualAmount,
                    Variance = v.Variance,
                    VariancePercent = v.VariancePercent,
                    Status = v.Status
                }).ToList();

        return model;
    }
}
