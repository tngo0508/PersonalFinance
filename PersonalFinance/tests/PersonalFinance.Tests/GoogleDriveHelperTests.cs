using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs;
using PersonalFinance.Shared.Helpers;
using Xunit;

namespace PersonalFinance.Tests;

public class GoogleDriveHelperTests
{
    private static AppDbContext CreateTestDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var context = new AppDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    [Theory]
    [InlineData("https://drive.google.com/drive/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_?usp=drive_link", "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_")]
    [InlineData("https://drive.google.com/drive/u/0/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_", "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_")]
    [InlineData("https://drive.google.com/drive/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_", "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_")]
    [InlineData("https://drive.google.com/open?id=127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_", "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_")]
    [InlineData("https://drive.google.com/file/d/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_/view", "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_")]
    [InlineData("127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_", "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_")]
    public void ExtractFolderId_WithValidInputs_ExtractsCorrectId(string input, string expectedId)
    {
        var result = GoogleDriveHelper.ExtractFolderId(input);
        Assert.Equal(expectedId, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("short")]
    [InlineData("https://google.com/search?q=test")]
    public void ExtractFolderId_WithInvalidInputs_ReturnsNull(string? input)
    {
        var result = GoogleDriveHelper.ExtractFolderId(input);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null, "-")]
    [InlineData(-5L, "-")]
    [InlineData(0L, "0 B")]
    [InlineData(512L, "512 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1048576L, "1 MB")]
    [InlineData(1073741824L, "1 GB")]
    public void FormatBytes_FormatsCorrectly(long? bytes, string expected)
    {
        var result = GoogleDriveHelper.FormatBytes(bytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("application/vnd.google-apps.folder", "folder", "Folder", true)]
    [InlineData("application/pdf", "document.pdf", "PDF Document", false)]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "budget.xlsx", "Spreadsheet", false)]
    [InlineData("application/vnd.google-apps.spreadsheet", "Financial Model", "Spreadsheet", false)]
    [InlineData("text/csv", "transactions.csv", "Spreadsheet", false)]
    [InlineData("application/vnd.ms-excel", "data.xls", "Spreadsheet", false)]
    [InlineData("application/vnd.google-apps.document", "Meeting Notes", "Document", false)]
    [InlineData("application/vnd.google-apps.presentation", "Quarterly Review", "Presentation", false)]
    [InlineData("image/png", "scan.png", "Image", false)]
    [InlineData("video/mp4", "clip.mp4", "Video", false)]
    [InlineData("application/zip", "archive.zip", "Archive", false)]
    public void ResolveTypeInfo_IdentifiesExpectedTypes(string mimeType, string filename, string expectedCategory, bool expectedIsFolder)
    {
        var (category, _, isFolder) = GoogleDriveHelper.ResolveTypeInfo(mimeType, filename);
        Assert.Equal(expectedCategory, category);
        Assert.Equal(expectedIsFolder, isFolder);
    }

    [Fact]
    public async Task GoogleDriveService_WithValidFolderAndNoKey_ReturnsPreviewResponse()
    {
        using var dbContext = CreateTestDbContext();
        var inMemorySettings = new Dictionary<string, string?>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var httpClient = new HttpClient();
        var service = new GoogleDriveService(httpClient, configuration, dbContext, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "https://drive.google.com/drive/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_?usp=drive_link"
        };

        var response = await service.GetFolderFilesAsync(request);

        Assert.True(response.Success);
        Assert.Equal("127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_", response.FolderId);
        Assert.NotEmpty(response.Files);
        Assert.True(response.IsDemoData);
        Assert.NotNull(response.WarningMessage);
    }

    [Fact]
    public async Task GoogleDriveService_WithInvalidUrl_ReturnsErrorResponse()
    {
        using var dbContext = CreateTestDbContext();
        var configuration = new ConfigurationBuilder().Build();
        var httpClient = new HttpClient();
        var service = new GoogleDriveService(httpClient, configuration, dbContext, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "invalid-link"
        };

        var response = await service.GetFolderFilesAsync(request);

        Assert.False(response.Success);
        Assert.True(string.IsNullOrEmpty(response.FolderId));
        Assert.NotNull(response.ErrorMessage);
    }

    [Fact]
    public async Task GoogleDriveService_WhenHttpClientTimesOut_FallsBackToPreviewGracefully()
    {
        using var dbContext = CreateTestDbContext();
        var configuration = new ConfigurationBuilder().Build();
        var handler = new DelegatingTestHandler((req, ct) =>
            throw new TaskCanceledException("A task was canceled.", new TimeoutException("The operation timed out.")));
        var httpClient = new HttpClient(handler);
        var service = new GoogleDriveService(httpClient, configuration, dbContext, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ApiKey = "AIzaSyFakeKey"
        };

        var response = await service.GetFolderFilesAsync(request);

        Assert.True(response.Success);
        Assert.True(response.IsDemoData);
        Assert.Contains("timed out", response.WarningMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GoogleDriveService_WhenCallerCancels_ReturnsCanceledResult()
    {
        using var dbContext = CreateTestDbContext();
        var configuration = new ConfigurationBuilder().Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        var handler = new DelegatingTestHandler((req, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var httpClient = new HttpClient(handler);
        var service = new GoogleDriveService(httpClient, configuration, dbContext, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ApiKey = "AIzaSyFakeKey"
        };

        var response = await service.GetFolderFilesAsync(request, cts.Token);

        Assert.False(response.Success);
        Assert.Equal("The request was canceled.", response.ErrorMessage);
    }

    [Fact]
    public async Task GoogleDriveService_WhenHttp403Forbidden_FallsBackToPreviewGracefully()
    {
        using var dbContext = CreateTestDbContext();
        var configuration = new ConfigurationBuilder().Build();
        var handler = new DelegatingTestHandler((req, ct) =>
        {
            var msg = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                ReasonPhrase = "Forbidden"
            };
            return Task.FromResult(msg);
        });
        var httpClient = new HttpClient(handler);
        var service = new GoogleDriveService(httpClient, configuration, dbContext, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ApiKey = "AIzaSyFakeKey"
        };

        var response = await service.GetFolderFilesAsync(request);

        Assert.True(response.Success);
        Assert.True(response.IsDemoData);
        Assert.Contains("403", response.WarningMessage);
        Assert.Contains("Anyone with the link", response.WarningMessage);
    }

    [Fact]
    public async Task GoogleDriveService_WhenHttp404NotFound_ExplainsSharingPermissions()
    {
        using var dbContext = CreateTestDbContext();
        var configuration = new ConfigurationBuilder().Build();
        var handler = new DelegatingTestHandler((req, ct) =>
        {
            var msg = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                ReasonPhrase = "Not Found"
            };
            return Task.FromResult(msg);
        });
        var httpClient = new HttpClient(handler);
        var service = new GoogleDriveService(httpClient, configuration, dbContext, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ApiKey = "AIzaSyFakeKey"
        };

        var response = await service.GetFolderFilesAsync(request);

        Assert.True(response.Success);
        Assert.True(response.IsDemoData);
        Assert.Contains("404", response.WarningMessage);
        Assert.Contains("Anyone with the link", response.WarningMessage);
    }

    [Fact]
    public async Task GoogleDriveService_WhenLiveQueryReturnsEmptyFiles_ProvidesSharingGuidance()
    {
        using var dbContext = CreateTestDbContext();
        var configuration = new ConfigurationBuilder().Build();
        var emptyJsonResponse = @"{ ""files"": [] }";
        var handler = new DelegatingTestHandler((req, ct) =>
        {
            var msg = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(emptyJsonResponse, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(msg);
        });
        var httpClient = new HttpClient(handler);
        var service = new GoogleDriveService(httpClient, configuration, dbContext, NullLogger<GoogleDriveService>.Instance);

        var request = new GoogleDriveRequestDto
        {
            FolderUrl = "127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_",
            ApiKey = "AIzaSyFakeKey"
        };

        var response = await service.GetFolderFilesAsync(request);

        Assert.True(response.Success);
        Assert.False(response.IsDemoData);
        Assert.Empty(response.Files);
        Assert.NotNull(response.WarningMessage);
        Assert.Contains("Anyone with the link", response.WarningMessage);
    }

    [Fact]
    public void GoogleDriveController_IsDecoratedWithAuthorizeAttribute()
    {
        var controllerType = typeof(PersonalFinance.Web.Controllers.GoogleDriveController);
        var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

        Assert.NotNull(authorizeAttribute);
    }

    [Theory]
    [InlineData("Monthly budget 2026.xlsx", "Spreadsheet", null, true)]
    [InlineData("Monthly budget.xlsx", "Spreadsheet", null, true)]
    [InlineData("Monthly budget - Personal.csv", "Spreadsheet", null, true)]
    [InlineData("MONTHLY BUDGET 2025.XLSX", "Spreadsheet", null, true)]
    [InlineData("Monthly budget.gsheet", "Spreadsheet", "application/vnd.google-apps.spreadsheet", true)]
    [InlineData("Monthly budget", "Spreadsheet", null, true)]
    [InlineData("Household_Budget_Model.xlsx", "Spreadsheet", null, false)]
    [InlineData("Annual_Financial_Report_2025.pdf", "PDF Document", null, false)]
    [InlineData("Monthly budget.pdf", "PDF Document", null, false)]
    [InlineData("Weekly budget.xlsx", "Spreadsheet", null, false)]
    [InlineData(null, "Spreadsheet", null, false)]
    [InlineData("", "Spreadsheet", null, false)]
    public void IsMonthlyBudgetSpreadsheet_EvaluatesCorrectly(string? fileName, string? fileType, string? mimeType, bool expected)
    {
        var result = GoogleDriveHelper.IsMonthlyBudgetSpreadsheet(fileName, fileType, mimeType);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GenerateMonthlyBudgetReport_ReturnsTwelveMonthsWithValidTotals()
    {
        // Act
        var report = GoogleDriveHelper.GenerateMonthlyBudgetReport("Monthly budget 2026.xlsx", "file-123");

        // Assert
        Assert.NotNull(report);
        Assert.Equal("Monthly budget 2026.xlsx", report.FileName);
        Assert.Equal("file-123", report.FileId);
        Assert.Equal(2026, report.Year);
        Assert.Equal(12, report.Months.Count);
        Assert.True(report.TotalAnnualIncome > 0);
        Assert.True(report.TotalAnnualExpenses > 0);
        Assert.Equal(report.TotalAnnualIncome - report.TotalAnnualExpenses, report.TotalAnnualSavings);
        Assert.True(report.AverageSavingsRate > 0);

        for (int i = 0; i < 12; i++)
        {
            var month = report.Months[i];
            Assert.Equal(i + 1, month.MonthNumber);
            Assert.False(string.IsNullOrWhiteSpace(month.MonthName));
            Assert.True(month.ActualIncome > 0);
            Assert.True(month.ActualExpenses > 0);
            Assert.NotEmpty(month.Categories);
            Assert.Equal(month.ActualIncome - month.ActualExpenses, month.NetSavings);
            Assert.False(string.IsNullOrWhiteSpace(month.Status));

            foreach (var cat in month.Categories)
            {
                Assert.False(string.IsNullOrWhiteSpace(cat.CategoryName));
                Assert.True(cat.BudgetedAmount > 0);
                Assert.True(cat.ActualAmount > 0);
                Assert.Equal(cat.BudgetedAmount - cat.ActualAmount, cat.Variance);
                Assert.False(string.IsNullOrWhiteSpace(cat.Status));
            }
        }
    }

    [Fact]
    public void ParseCsvTable_HandlesQuotedFieldsCommasAndNewlinesCorrectly()
    {
        var csv = "\"Category\",\"Planned Amount\",\"Actual Amount\"\n\"Housing, Rent & HOA\",\"$1,500.00\",\"$1,550.00\"\n\"Groceries \"\"Supermarket\"\"\",\"$650.00\",\"$680.00\"";
        var table = GoogleDriveHelper.ParseCsvTable(csv);

        Assert.Equal(3, table.Count);
        Assert.Equal("Category", table[0][0]);
        Assert.Equal("Planned Amount", table[0][1]);
        Assert.Equal("Housing, Rent & HOA", table[1][0]);
        Assert.Equal("$1,500.00", table[1][1]);
        Assert.Equal("$1,550.00", table[1][2]);
        Assert.Equal("Groceries \"Supermarket\"", table[2][0]);
    }

    [Fact]
    public void ParseSpreadsheetBudgetReport_ParsesActualIncomeAndExpensesCorrectly()
    {
        var sampleCsv = @"Monthly budget 2026

INCOME,Planned,Actual
Salary,5000,5200
Side Gig,1000,850

EXPENSES,Planned,Actual
Rent,1600,1600
Groceries,600,650
Utilities,300,280
Entertainment,400,450";

        var report = GoogleDriveHelper.ParseSpreadsheetBudgetReport(sampleCsv, "Monthly budget 2026.xlsx", "drive-file-999");

        Assert.NotNull(report);
        Assert.Equal("Monthly budget 2026.xlsx", report.FileName);
        Assert.Equal("drive-file-999", report.FileId);
        Assert.Equal(2026, report.Year);
        Assert.True(report.IsLiveSpreadsheetData);
        Assert.Equal(12, report.Months.Count);

        var firstMonth = report.Months[0];
        Assert.Equal(6050m, firstMonth.ActualIncome); // 5200 + 850
        Assert.Equal(6000m, firstMonth.BudgetedIncome); // 5000 + 1000
        Assert.Equal(2980m, firstMonth.ActualExpenses); // 1600 + 650 + 280 + 450
        Assert.Equal(2900m, firstMonth.BudgetedExpenses); // 1600 + 600 + 300 + 400
        Assert.Equal(3070m, firstMonth.NetSavings);
        Assert.Equal("On Track", firstMonth.Status);

        Assert.Equal(4, firstMonth.Categories.Count);
        var rentCat = firstMonth.Categories.First(c => c.CategoryName == "Rent");
        Assert.Equal(1600m, rentCat.BudgetedAmount);
        Assert.Equal(1600m, rentCat.ActualAmount);
        Assert.Equal(0m, rentCat.Variance);

        var grocCat = firstMonth.Categories.First(c => c.CategoryName == "Groceries");
        Assert.Equal(600m, grocCat.BudgetedAmount);
        Assert.Equal(650m, grocCat.ActualAmount);
        Assert.Equal(-50m, grocCat.Variance);
        Assert.Equal("Over Budget", grocCat.Status);
    }

    [Fact]
    public async Task GoogleDriveService_GetSpreadsheetBudgetReportAsync_ForSampleFile_ReturnsParsedActualData()
    {
        using var dbContext = CreateTestDbContext();
        var configuration = new ConfigurationBuilder().Build();
        var handler = new DelegatingTestHandler((req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var httpClient = new HttpClient(handler);
        var service = new GoogleDriveService(httpClient, configuration, dbContext, NullLogger<GoogleDriveService>.Instance);

        var report = await service.GetSpreadsheetBudgetReportAsync("sample-sheet-budget-1", "Monthly budget 2026.xlsx");

        Assert.NotNull(report);
        Assert.Equal("Monthly budget 2026.xlsx", report.FileName);
        Assert.Equal("sample-sheet-budget-1", report.FileId);
        Assert.True(report.IsLiveSpreadsheetData);
        Assert.Contains("Actual Sample Data", report.DataSource);
        Assert.Equal(12, report.Months.Count);
        Assert.True(report.TotalAnnualIncome > 0);
        Assert.True(report.TotalAnnualExpenses > 0);
    }

    [Fact]
    public async Task GoogleDriveService_GetSpreadsheetBudgetReportAsync_WithLiveGoogleSheetExport_ParsesLiveCsv()
    {
        using var dbContext = CreateTestDbContext();
        var configuration = new ConfigurationBuilder().Build();

        var csvFromGoogle = @"Monthly budget 2026
INCOME,Planned,Actual
Consulting,8000,8500
EXPENSES,Planned,Actual
Housing,2000,2000
Food,800,750";

        var handler = new DelegatingTestHandler((req, ct) =>
        {
            var msg = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(csvFromGoogle, Encoding.UTF8, "text/csv")
            };
            return Task.FromResult(msg);
        });

        var httpClient = new HttpClient(handler);
        var service = new GoogleDriveService(httpClient, configuration, dbContext, NullLogger<GoogleDriveService>.Instance);

        var report = await service.GetSpreadsheetBudgetReportAsync("live-sheet-123", "Monthly budget 2026.gsheet");

        Assert.NotNull(report);
        Assert.Equal("live-sheet-123", report.FileId);
        Assert.Contains("Live Spreadsheet", report.DataSource);
        var m1 = report.Months[0];
        Assert.Equal(8500m, m1.ActualIncome);
        Assert.Equal(2750m, m1.ActualExpenses);
    }

    [Fact]
    public void ParseXlsxBudgetReport_WithOpenXmlSpreadsheet_ParsesActualDataCorrectly()
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var ssEntry = archive.CreateEntry("xl/sharedStrings.xml");
            using (var writer = new StreamWriter(ssEntry.Open(), Encoding.UTF8))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"5\" uniqueCount=\"5\"><si><t>INCOME</t></si><si><t>Salary</t></si><si><t>EXPENSES</t></si><si><t>Housing</t></si><si><t>Groceries</t></si></sst>");
            }

            var wsEntry = archive.CreateEntry("xl/worksheets/sheet1.xml");
            using (var writer = new StreamWriter(wsEntry.Open(), Encoding.UTF8))
            {
                writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c></row><row r=\"2\"><c r=\"A2\" t=\"s\"><v>1</v></c><c r=\"B2\"><v>5000</v></c><c r=\"C2\"><v>5400</v></c></row><row r=\"3\"><c r=\"A3\" t=\"s\"><v>2</v></c></row><row r=\"4\"><c r=\"A4\" t=\"s\"><v>3</v></c><c r=\"B4\"><v>1500</v></c><c r=\"C4\"><v>1500</v></c></row><row r=\"5\"><c r=\"A5\" t=\"s\"><v>4</v></c><c r=\"B5\"><v>600</v></c><c r=\"C5\"><v>650</v></c></row></sheetData></worksheet>");
            }
        }

        ms.Position = 0;
        var report = GoogleDriveHelper.ParseXlsxBudgetReport(ms, "Monthly budget 2026.xlsx", "drive-xlsx-100");

        Assert.NotNull(report);
        Assert.Equal("Monthly budget 2026.xlsx", report.FileName);
        Assert.Equal("drive-xlsx-100", report.FileId);
        Assert.Equal(12, report.Months.Count);

        var m1 = report.Months[0];
        Assert.Equal(5400m, m1.ActualIncome);
        Assert.Equal(5000m, m1.BudgetedIncome);
        Assert.Equal(2150m, m1.ActualExpenses); // 1500 + 650
        Assert.Equal(2100m, m1.BudgetedExpenses); // 1500 + 600
        Assert.Equal(3250m, m1.NetSavings);
    }

    [Theory]
    [InlineData("Monthly budget - September 2026", 9)]
    [InlineData("Monthly budget - September 2026.xlsx", 9)]
    [InlineData("Monthly budget - Sep 2026", 9)]
    [InlineData("Monthly budget - Sept 2026", 9)]
    [InlineData("Monthly budget - January 2026.xlsx", 1)]
    [InlineData("Monthly budget - December 2026", 12)]
    [InlineData("Monthly budget 2026-09.xlsx", 9)]
    [InlineData("Monthly budget 09-2026.csv", 9)]
    [InlineData("Monthly budget 2026.xlsx", null)]
    [InlineData("Monthly budget.xlsx", null)]
    public void DetectMonthFromText_DetectsExpectedMonth(string input, int? expectedMonth)
    {
        var result = GoogleDriveHelper.DetectMonthFromText(input);
        Assert.Equal(expectedMonth, result);
    }

    [Fact]
    public void GenerateMonthlyBudgetReport_WithSingleMonthName_ReturnsOnlyThatMonth()
    {
        // Act
        var report = GoogleDriveHelper.GenerateMonthlyBudgetReport("Monthly budget - September 2026.xlsx", "file-sep-123");

        // Assert
        Assert.NotNull(report);
        Assert.Equal("Monthly budget - September 2026.xlsx", report.FileName);
        Assert.Equal("file-sep-123", report.FileId);
        Assert.Equal(2026, report.Year);
        Assert.True(report.IsSingleMonth);
        Assert.Equal("September", report.SingleMonthName);
        Assert.Single(report.Months);

        var sepMonth = report.Months[0];
        Assert.Equal(9, sepMonth.MonthNumber);
        Assert.Equal("September", sepMonth.MonthName);
        Assert.True(sepMonth.ActualIncome > 0);
        Assert.True(sepMonth.ActualExpenses > 0);
        Assert.NotEmpty(sepMonth.Categories);
        Assert.Equal(report.TotalAnnualIncome, sepMonth.ActualIncome);
        Assert.Equal(report.TotalAnnualExpenses, sepMonth.ActualExpenses);
    }

    [Fact]
    public void ParseSpreadsheetBudgetReport_WithSingleMonthName_ReturnsOnlySpecifiedMonth()
    {
        var sampleCsv = @"Monthly budget - September 2026

INCOME,Planned,Actual
Salary,5500,5600

EXPENSES,Planned,Actual
Rent,1500,1500
Food,650,620";

        var report = GoogleDriveHelper.ParseSpreadsheetBudgetReport(sampleCsv, "Monthly budget - September 2026.xlsx", "file-sep-456");

        Assert.NotNull(report);
        Assert.True(report.IsSingleMonth);
        Assert.Single(report.Months);
        Assert.Equal(9, report.Months[0].MonthNumber);
        Assert.Equal("September", report.Months[0].MonthName);
        Assert.Equal(5600m, report.Months[0].ActualIncome);
        Assert.Equal(2120m, report.Months[0].ActualExpenses); // 1500 + 620
    }

    [Fact]
    public void ParseSpreadsheetBudgetReport_WithStartAndEndBalancesAndPlannedActuals_CalculatesCorrectMetrics()
    {
        var sampleCsv = @"Monthly budget - September 2026

STARTING BALANCE,1000.00,1000.00
ENDING BALANCE,1500.00,1700.00

INCOME,Planned,Actual,Difference
Salary,4500.00,4500.00,0.00
Bonus,500.00,600.00,100.00
Total Income,5000.00,5100.00,100.00

EXPENSES,Planned,Actual,Difference
Housing & Rent,1500.00,1500.00,0.00
Groceries & Food,800.00,750.00,50.00
Utilities,300.00,280.00,20.00
Dining & Entertainment,400.00,370.00,30.00
Total Expenses,3000.00,2900.00,100.00";

        var report = GoogleDriveHelper.ParseSpreadsheetBudgetReport(sampleCsv, "Monthly budget - September 2026.xlsx", "file-sep-789");

        Assert.NotNull(report);
        Assert.True(report.IsSingleMonth);
        Assert.Single(report.Months);

        var month = report.Months[0];
        Assert.Equal(9, month.MonthNumber);
        Assert.Equal("September", month.MonthName);

        // Balances
        Assert.Equal(1000.00m, month.StartingBalance);
        Assert.Equal(1700.00m, month.EndingBalance);

        // Total Income: Planned 5000, Actual 5100
        Assert.Equal(5000.00m, month.BudgetedIncome);
        Assert.Equal(5100.00m, month.ActualIncome);
        Assert.Equal(5100.00m, report.TotalAnnualIncome);

        // Total Expenses: Planned 3000, Actual 2900 (NOT corrupted by start/end balance or double counted)
        Assert.Equal(3000.00m, month.BudgetedExpenses);
        Assert.Equal(2900.00m, month.ActualExpenses);
        Assert.Equal(2900.00m, report.TotalAnnualExpenses);

        // Net Savings / Surplus: Actual Income (5100) - Actual Expenses (2900) = 2200
        Assert.Equal(2200.00m, month.NetSavings);
        Assert.Equal(2200.00m, report.TotalAnnualSavings);

        // Savings Rate: 2200 / 5100 * 100 = 43.137%
        Assert.True(month.SavingsRate > 43.0 && month.SavingsRate < 43.2);

        // Status: Under Budget (Actual 2900 <= Budgeted 3000)
        Assert.Equal("Under Budget", month.Status);

        // Expense categories should only have the 4 expense items, not Start Balance or End Balance or Total rows
        Assert.Equal(4, month.Categories.Count);
        Assert.DoesNotContain(month.Categories, c => c.CategoryName.Contains("Balance", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(month.Categories, c => c.CategoryName.Contains("Total", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParseSpreadsheetBudgetReport_WithSideBySideTables_ParsesBothExpensesAndIncomeCorrectly()
    {
        var sampleCsv = @"Monthly budget - September 2026
Start balance, 2500, , End balance, 4200
Expenses, Planned, Actual, Difference, , Income, Planned, Actual, Difference
Rent, 1200, 1200, 0, , Salary, 4000, 4000, 0
Groceries, 600, 580, 20, , Freelance, 1000, 1200, 200
Utilities, 250, 220, 30, , Dividend, 200, 300, 100
Total Expenses, 2050, 2000, 50, , Total Income, 5200, 5500, 300";

        var report = GoogleDriveHelper.ParseSpreadsheetBudgetReport(sampleCsv, "Monthly budget - September 2026.xlsx", "file-sep-side");

        Assert.NotNull(report);
        Assert.Single(report.Months);
        var month = report.Months[0];

        Assert.Equal(2500m, month.StartingBalance);
        Assert.Equal(4200m, month.EndingBalance);

        Assert.Equal(5200m, month.BudgetedIncome);
        Assert.Equal(5500m, month.ActualIncome);

        Assert.Equal(2050m, month.BudgetedExpenses);
        Assert.Equal(2000m, month.ActualExpenses);

        Assert.Equal(3500m, month.NetSavings); // 5500 - 2000
        Assert.Equal("Under Budget", month.Status);
    }
}

/// <summary>
/// Simple mock delegating handler for testing HttpClient responses without external networks.
/// </summary>
public class DelegatingTestHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public DelegatingTestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _handler(request, cancellationToken);
    }
}
