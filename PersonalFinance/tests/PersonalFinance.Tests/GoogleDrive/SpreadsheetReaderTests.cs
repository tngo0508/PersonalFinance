using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Services;

namespace PersonalFinance.Tests.GoogleDrive;

/// <summary>
/// Budget figures shown to the user always come from the spreadsheet; when it cannot be read the result says why.
/// </summary>
public class SpreadsheetReaderTests
{
    [Fact]
    public async Task UndownloadableFile_ReturnsUnavailableReportWithReason()
    {
        var reader = CreateReader();

        var report = await reader.GetSpreadsheetBudgetReportAsync("account-1", "file-1", "Monthly budget 2026.xlsx", 7);

        Assert.Empty(report.Months);
        Assert.False(report.IsLiveSpreadsheetData);
        Assert.Equal(0m, report.TotalAnnualIncome);
        Assert.Contains("could not be downloaded", report.ErrorMessage);
    }

    [Fact]
    public async Task FileWithoutBudgetTable_ReturnsUnavailableReportWithReason()
    {
        var reader = CreateReader(Csv("Shopping list\nMilk,Eggs,Bread"));

        var report = await reader.GetSpreadsheetBudgetReportAsync("account-1", "file-1", "Monthly budget 2026.csv");

        Assert.Empty(report.Months);
        Assert.False(report.IsLiveSpreadsheetData);
        Assert.Contains("No monthly budget table", report.ErrorMessage);
    }

    [Fact]
    public async Task UnreadableWorkbook_FallsThroughToTheNextDownload()
    {
        var reader = CreateReader(
            new SpreadsheetDownload("Monthly budget 2026", SpreadsheetFormat.Xlsx, "not a zip"u8.ToArray(), "XLSX export"),
            Csv("Month,Planned Income,Actual Income,Planned Expenses,Actual Expenses\nMarch,5000,5200,3000,3100"));

        var report = await reader.GetSpreadsheetBudgetReportAsync("account-1", "file-1", "Monthly budget 2026");

        Assert.Equal(5200m, report.TotalAnnualIncome);
        Assert.Equal("CSV export", report.DataSource);
    }

    [Fact]
    public async Task MonthTotalsWithoutCategoryRows_HaveNoInventedCategoryBreakdown()
    {
        var reader = CreateReader(Csv("Month,Planned Income,Actual Income,Planned Expenses,Actual Expenses\nMarch,5000,5200,3000,3100"));

        var report = await reader.GetSpreadsheetBudgetReportAsync("account-1", "file-1", "Budget 2026.csv");

        var march = Assert.Single(report.Months, m => m.MonthNumber == 3);
        Assert.Equal(5200m, march.ActualIncome);
        Assert.Equal(3100m, march.ActualExpenses);
        Assert.Empty(march.Categories);
    }

    [Fact]
    public async Task MonthsMissingFromTheSheet_StayEmptyInsteadOfCopyingAnotherMonth()
    {
        var reader = CreateReader(Csv("Month,Planned Income,Actual Income,Planned Expenses,Actual Expenses\nMarch,5000,5200,3000,3100"));

        var report = await reader.GetSpreadsheetBudgetReportAsync("account-1", "file-1", "Budget 2026.csv");

        Assert.All(report.Months.Where(m => m.MonthNumber != 3), m =>
        {
            Assert.Equal(0m, m.ActualIncome);
            Assert.Equal(0m, m.ActualExpenses);
            Assert.Empty(m.Categories);
        });
        Assert.Equal(5200m, report.TotalAnnualIncome);
    }

    [Fact]
    public async Task SampleFile_IsParsedFromTheBuiltInSampleWithoutDownloading()
    {
        var reader = CreateReader();

        var report = await reader.GetSpreadsheetBudgetReportAsync("account-1", "sample-sheet-budget-1",
            "Monthly budget 2026.xlsx");

        Assert.Equal("sample-sheet-budget-1", report.FileId);
        Assert.Equal(12, report.Months.Count);
        Assert.Contains("Sample Data", report.DataSource);
        Assert.True(report.TotalAnnualIncome > 0);
    }

    [Fact]
    public async Task UndownloadableFile_TransactionsSayWhy()
    {
        var reader = CreateReader();

        var transactions = await reader.GetSpreadsheetTransactionsAsync("account-1", "file-1", "Monthly budget 2026.xlsx");

        Assert.Empty(transactions.Transactions);
        Assert.False(transactions.IsLiveSpreadsheetData);
        Assert.Contains("could not be downloaded", transactions.ErrorMessage);
    }

    private static SpreadsheetDownload Csv(string content) =>
        new("Spreadsheet.csv", SpreadsheetFormat.Csv, Encoding.UTF8.GetBytes(content), "CSV export");

    private static SpreadsheetReaderService CreateReader(params SpreadsheetDownload[] downloads) =>
        new(new FakeFileSource(downloads), NullLogger<SpreadsheetReaderService>.Instance);

    private sealed class FakeFileSource(IReadOnlyList<SpreadsheetDownload> downloads) : IGoogleDriveFileSource
    {
        public async IAsyncEnumerable<SpreadsheetDownload> DownloadAsync(string userId, string fileId,
            string? fileName, int? connectionId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var download in downloads)
            {
                await Task.Yield();
                yield return download;
            }
        }
    }
}
