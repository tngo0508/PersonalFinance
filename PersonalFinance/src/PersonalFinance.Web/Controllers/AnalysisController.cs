using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Web.Models.Analysis;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Web.Controllers;

/// <summary>
///     MVC Controller for the Spreadsheet Analysis feature.
///     Provides dashboard views, sync operations, and data endpoints.
/// </summary>
[Authorize]
public class AnalysisController : Controller
{
    private readonly ISpreadsheetAnalysisApi _api;
    private readonly IBudgetTrackerApi _budgetTrackerApi;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(ISpreadsheetAnalysisApi api, IBudgetTrackerApi budgetTrackerApi,
        ILogger<AnalysisController> logger)
    {
        _api = api;
        _budgetTrackerApi = budgetTrackerApi;
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
        AnalysisDashboardDto result = await _api.GetAnalysisDashboardAsync(year, month, cancellationToken);

        AnalysisDashboardViewModel model = MapToViewModel(result);
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
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User requested sync for spreadsheet {FileId}", fileId);
        }

        var request = new SyncSpreadsheetRequestDto
        {
            ConnectionId = connectionId,
            FileId = fileId,
            FileName = fileName,
            Overwrite = false
        };

        SyncSpreadsheetResponseDto result = await _api.SyncSpreadsheetAsync(request, cancellationToken);

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
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User confirmed overwrite for spreadsheet {FileId}", fileId);
        }

        var request = new SyncSpreadsheetRequestDto
        {
            ConnectionId = connectionId,
            FileId = fileId,
            FileName = fileName,
            Overwrite = true
        };

        SyncSpreadsheetResponseDto result = await _api.SyncSpreadsheetAsync(request, cancellationToken);

        if (result.Success)
            TempData["StatusMessage"] =
                $"Successfully synced {result.SnapshotsCreated} month(s) and {result.TransactionsCreated} transactions (overwritten existing data).";
        else
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Sync failed. Please try again.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    ///     Shows what removing a Data Source would delete, and asks the user to confirm.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> RemoveSource(
        [FromQuery] BudgetDataSource source,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(source))
        {
            return RedirectToAction(nameof(Index));
        }

        var model = new RemoveSourceViewModel { Source = source };
        if (!await LoadCountsAsync(model, cancellationToken))
        {
            TempData["ErrorMessage"] = $"There is no {model.SourceName} data to remove.";
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    /// <summary>
    ///     Removes (soft-deletes) all of one Data Source's data once confirmed. The Budget Tracker also needs
    ///     <see cref="RemoveSourceViewModel.ConfirmationWord"/> typed, since its entries exist nowhere else.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(RemoveSource))]
    public async Task<IActionResult> RemoveSourceConfirmed(
        RemoveSourceViewModel input,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(input.Source))
        {
            return RedirectToAction(nameof(Index));
        }

        var model = new RemoveSourceViewModel { Source = input.Source, Confirmation = input.Confirmation };
        if (model.RequiresTypedConfirmation &&
            !string.Equals(input.Confirmation?.Trim(), RemoveSourceViewModel.ConfirmationWord, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(RemoveSourceViewModel.Confirmation),
                $"Type {RemoveSourceViewModel.ConfirmationWord} to confirm.");
            if (!await LoadCountsAsync(model, cancellationToken))
            {
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        BudgetDataSourceSummaryDto removed = input.Source == BudgetDataSource.BudgetTracker
            ? await _budgetTrackerApi.RemoveDataAsync(cancellationToken)
            : await _api.RemoveSyncedDataAsync(cancellationToken);

        TempData["StatusMessage"] =
            $"Removed your {model.SourceName} data: {removed.MonthCount} month(s) and {removed.TransactionCount} transaction(s).";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Fills in the source's current counts; false when it holds no data.</summary>
    private async Task<bool> LoadCountsAsync(RemoveSourceViewModel model, CancellationToken cancellationToken)
    {
        List<BudgetDataSourceSummaryDto> summaries =
            await _budgetTrackerApi.GetDataSourceSummariesAsync(cancellationToken);
        BudgetDataSourceSummaryDto? summary = summaries.FirstOrDefault(s => s.Source == model.Source);
        if (summary is not { HasData: true })
        {
            return false;
        }

        model.MonthCount = summary.MonthCount;
        model.TransactionCount = summary.TransactionCount;
        return true;
    }

    private static AnalysisDashboardViewModel MapToViewModel(AnalysisDashboardDto result)
    {
        var model = new AnalysisDashboardViewModel
        {
            HasData = result.HasData,
            SelectedYear = result.Year,
            SelectedMonth = result.Month,
            AvailableYears = result.AvailableYears,
            AvailableMonths = result.AvailableMonths,
            ActiveSource = result.ActiveSource,
            RemovableSources = result.SourceSummaries.Where(s => s.HasData).Select(s => s.Source).ToList()
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

        model.MoneyFlow = MoneyFlowBuilder.Build(result.Categories);

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
