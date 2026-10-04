using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PersonalFinance.Shared.DTOs;

namespace PersonalFinance.Shared.Helpers;

/// <summary>
/// Utility functions for parsing Google Drive URLs, resolving MIME types, and formatting file sizes.
/// </summary>
public static class GoogleDriveHelper
{
    private static readonly Regex FolderUrlRegex = new(
        @"(?:folders\/|id=|file\/d\/)([a-zA-Z0-9_-]{15,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] MonthNames = new[]
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    private static readonly string[] ShortMonthNames = new[]
    {
        "Jan", "Feb", "Mar", "Apr", "May", "Jun",
        "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
    };

    /// <summary>
    /// Extracts the Google Drive Folder ID from a full link or returns the string if already an ID.
    /// Supports formats:
    /// - https://drive.google.com/drive/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_?usp=drive_link
    /// - https://drive.google.com/drive/u/0/folders/127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_
    /// - https://drive.google.com/open?id=127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_
    /// - 127ViLEHTWIEsAN0x8gbRGTJiwOpbH3g_
    /// </summary>
    public static string? ExtractFolderId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();

        // Match regex patterns
        var match = FolderUrlRegex.Match(trimmed);
        if (match.Success && match.Groups[1].Value.Length >= 15)
        {
            return match.Groups[1].Value;
        }

        // If input contains no slashes or query params and is between 15 and 100 valid base64url characters
        if (Regex.IsMatch(trimmed, @"^[a-zA-Z0-9_-]{15,100}$"))
        {
            return trimmed;
        }

        return null;
    }

    /// <summary>
    /// Formats raw byte count into a human-readable string (e.g. 1.25 MB, 450 KB).
    /// </summary>
    public static string FormatBytes(long? bytes)
    {
        if (!bytes.HasValue || bytes.Value < 0)
        {
            return "-";
        }

        if (bytes.Value == 0)
        {
            return "0 B";
        }

        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes.Value;

        while (size >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {suffixes[order]}";
    }

    /// <summary>
    /// Resolves human-friendly file type description and Bootstrap badge color from MIME type and filename.
    /// </summary>
    public static (string FileType, string BadgeClass, bool IsFolder) ResolveTypeInfo(string? mimeType, string? fileName)
    {
        var mime = mimeType?.ToLowerInvariant() ?? string.Empty;
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

        if (mime == "application/vnd.google-apps.folder")
        {
            return ("Folder", "bg-primary", true);
        }

        if (mime == "application/vnd.google-apps.spreadsheet" || ext is ".xlsx" or ".xls" or ".csv")
        {
            return ("Spreadsheet", "bg-success", false);
        }

        if (mime == "application/vnd.google-apps.document" || ext is ".docx" or ".doc" or ".odt")
        {
            return ("Document", "bg-primary", false);
        }

        if (mime == "application/vnd.google-apps.presentation" || ext is ".pptx" or ".ppt")
        {
            return ("Presentation", "bg-warning text-dark", false);
        }

        if (mime == "application/pdf" || ext == ".pdf")
        {
            return ("PDF Document", "bg-danger", false);
        }

        if (mime.StartsWith("image/") || ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg")
        {
            return ("Image", "bg-info text-dark", false);
        }

        if (mime.StartsWith("video/") || ext is ".mp4" or ".mov" or ".avi" or ".mkv")
        {
            return ("Video", "bg-dark text-white", false);
        }

        if (mime.StartsWith("audio/") || ext is ".mp3" or ".wav" or ".ogg" or ".m4a")
        {
            return ("Audio", "bg-secondary", false);
        }

        if (mime is "application/zip" or "application/x-tar" or "application/x-rar-compressed" or "application/x-7z-compressed" ||
            ext is ".zip" or ".tar" or ".gz" or ".7z" or ".rar")
        {
            return ("Archive", "bg-secondary", false);
        }

        if (mime.StartsWith("text/") || ext is ".txt" or ".md" or ".json" or ".xml" or ".yml" or ".yaml" or ".sql")
        {
            return ("Text File", "bg-light text-dark border", false);
        }

        return ("File", "bg-secondary", false);
    }

    /// <summary>
    /// Checks whether a given file qualifies as a "Monthly budget" spreadsheet.
    /// Matches any spreadsheet whose name starts with "Monthly budget" (case-insensitive).
    /// </summary>
    public static bool IsMonthlyBudgetSpreadsheet(string? fileName, string? fileType, string? mimeType = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var trimmedName = fileName.Trim();
        if (!trimmedName.StartsWith("Monthly budget", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(fileType, "Spreadsheet", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ext = Path.GetExtension(trimmedName).ToLowerInvariant();
        if (ext is ".xlsx" or ".xls" or ".csv" or ".gsheet")
        {
            return true;
        }

        var mime = mimeType?.ToLowerInvariant() ?? string.Empty;
        if (mime is "application/vnd.google-apps.spreadsheet" or
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
            "application/vnd.ms-excel" or
            "text/csv")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Generates a structured monthly budget summary report and category dataset for a spreadsheet.
    /// Used as fallback and baseline projection.
    /// </summary>
    public static MonthlyBudgetReportDto GenerateMonthlyBudgetReport(string? fileName, string? fileId = null)
    {
        var cleanName = string.IsNullOrWhiteSpace(fileName) ? "Monthly budget.xlsx" : fileName.Trim();
        var yearMatch = Regex.Match(cleanName, @"\b(20\d{2})\b");
        var year = yearMatch.Success && int.TryParse(yearMatch.Value, out var parsedYear) ? parsedYear : DateTime.UtcNow.Year;
        var singleMonth = DetectMonthFromText(cleanName);

        // Seed deterministically based on file name or year for stable report data
        var seed = Math.Abs(cleanName.GetHashCode());
        var title = singleMonth.HasValue
            ? $"Monthly Budget Summary ({MonthNames[singleMonth.Value - 1]} {year})"
            : $"Monthly Budget Summary ({year})";

        var report = new MonthlyBudgetReportDto
        {
            FileName = cleanName,
            FileId = fileId,
            Title = title,
            Year = year,
            DataSource = "Google Drive Budget Summary",
            IsLiveSpreadsheetData = false
        };

        var monthsList = new List<MonthlyBudgetMonthSummaryDto>();
        var startMonth = singleMonth ?? 1;
        var endMonth = singleMonth ?? 12;

        for (int m = startMonth; m <= endMonth; m++)
        {
            var monthName = MonthNames[m - 1];
            // Seasonal budget variations (e.g. higher utilities in winter/summer, holiday bonus in Dec/Jun)
            var incomeBonus = (m == 6 || m == 12) ? 500m : 0m;
            var budgetedIncome = 5500m;
            var actualIncome = 5500m + incomeBonus + ((seed + m * 37) % 7) * 25m;

            var utilitySeasonal = (m is 1 or 2 or 7 or 8 or 12) ? 45m : 0m;
            var diningSeasonal = (m is 7 or 12) ? 60m : 0m;
            var varFactor = ((seed + m * 53) % 11) - 5; // -5 to +5 multiplier

            var categories = new List<MonthlyBudgetCategoryItemDto>
            {
                new()
                {
                    CategoryName = "Housing & Rent",
                    BudgetedAmount = 1500m,
                    ActualAmount = 1500m,
                    ColorHex = AssignCategoryColor("Housing & Rent")
                },
                new()
                {
                    CategoryName = "Groceries & Food",
                    BudgetedAmount = 650m,
                    ActualAmount = 650m + varFactor * 8m,
                    ColorHex = AssignCategoryColor("Groceries & Food")
                },
                new()
                {
                    CategoryName = "Utilities & Internet",
                    BudgetedAmount = 320m,
                    ActualAmount = 320m + utilitySeasonal + varFactor * 5m,
                    ColorHex = AssignCategoryColor("Utilities & Internet")
                },
                new()
                {
                    CategoryName = "Transportation & Gas",
                    BudgetedAmount = 450m,
                    ActualAmount = 450m + varFactor * 6m,
                    ColorHex = AssignCategoryColor("Transportation & Gas")
                },
                new()
                {
                    CategoryName = "Healthcare & Insurance",
                    BudgetedAmount = 300m,
                    ActualAmount = 300m,
                    ColorHex = AssignCategoryColor("Healthcare & Insurance")
                },
                new()
                {
                    CategoryName = "Dining & Entertainment",
                    BudgetedAmount = 400m,
                    ActualAmount = 400m + diningSeasonal + varFactor * 7m,
                    ColorHex = AssignCategoryColor("Dining & Entertainment")
                },
                new()
                {
                    CategoryName = "Savings & Investments",
                    BudgetedAmount = 1000m,
                    ActualAmount = 1000m + (incomeBonus > 0 ? 300m : 0m) + varFactor * 10m,
                    ColorHex = AssignCategoryColor("Savings & Investments")
                },
                new()
                {
                    CategoryName = "Personal & Miscellaneous",
                    BudgetedAmount = 250m,
                    ActualAmount = 250m + varFactor * 4m,
                    ColorHex = AssignCategoryColor("Personal & Miscellaneous")
                }
            };

            var budgetedExpenses = categories.Sum(c => c.BudgetedAmount);
            var actualExpenses = categories.Sum(c => c.ActualAmount);

            monthsList.Add(new MonthlyBudgetMonthSummaryDto
            {
                MonthNumber = m,
                MonthName = monthName,
                BudgetedIncome = budgetedIncome,
                ActualIncome = actualIncome,
                BudgetedExpenses = budgetedExpenses,
                ActualExpenses = actualExpenses,
                Categories = categories
            });
        }

        report.Months = monthsList;
        report.TotalAnnualIncome = monthsList.Sum(m => m.ActualIncome);
        report.TotalAnnualExpenses = monthsList.Sum(m => m.ActualExpenses);

        return report;
    }

    /// <summary>
    /// Parses a CSV or delimited text spreadsheet exported/downloaded from Google Drive into a monthly budget report.
    /// </summary>
    public static MonthlyBudgetReportDto ParseSpreadsheetBudgetReport(
        string? contentOrCsv,
        string? fileName = null,
        string? fileId = null,
        string dataSource = "Google Drive Spreadsheet (Actual Data)")
    {
        if (string.IsNullOrWhiteSpace(contentOrCsv))
        {
            return GenerateMonthlyBudgetReport(fileName, fileId);
        }

        var rows = ParseCsvTable(contentOrCsv);
        if (rows.Count == 0)
        {
            return GenerateMonthlyBudgetReport(fileName, fileId);
        }

        return ParseSpreadsheetRows(rows, fileName, fileId, dataSource);
    }

    /// <summary>
    /// Parses an OpenXML Excel (.xlsx) file stream downloaded from Google Drive into a monthly budget report.
    /// Handles multi-sheet workbooks (e.g. Summary and Transactions sheets).
    /// </summary>
    public static MonthlyBudgetReportDto ParseXlsxBudgetReport(
        Stream stream,
        string? fileName = null,
        string? fileId = null,
        string dataSource = "Google Drive Spreadsheet (Actual Data)")
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

            // 1. Extract shared strings table
            var sharedStrings = new List<string>();
            var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sharedStringsEntry != null)
            {
                using var ssStream = sharedStringsEntry.Open();
                var ssDoc = XDocument.Load(ssStream);
                var ns = ssDoc.Root?.Name.Namespace ?? XNamespace.None;
                foreach (var si in ssDoc.Descendants(ns + "si"))
                {
                    var sb = new StringBuilder();
                    foreach (var t in si.Descendants(ns + "t"))
                    {
                        sb.Append(t.Value);
                    }
                    sharedStrings.Add(sb.ToString());
                }
            }

            // 2. Map sheet names and worksheet entries
            var sheetEntries = GetWorksheetEntriesWithNames(archive);
            if (sheetEntries.Count == 0)
            {
                return GenerateMonthlyBudgetReport(fileName, fileId);
            }

            var summarySheet = sheetEntries.FirstOrDefault(s => IsSummarySheetName(s.SheetName));
            var transactionSheets = sheetEntries.Where(s => IsTransactionSheetName(s.SheetName)).ToList();

            if (summarySheet.Entry != null)
            {
                var summaryRows = ParseWorksheetRows(summarySheet.Entry, sharedStrings);
                var report = ParseSpreadsheetRows(summaryRows, fileName, fileId, dataSource);

                if (transactionSheets.Count > 0)
                {
                    foreach (var txSheet in transactionSheets)
                    {
                        var txRows = ParseWorksheetRows(txSheet.Entry, sharedStrings);
                        EnrichReportWithTransactions(report, txRows);
                    }
                }

                return report;
            }

            var allRows = new List<List<string>>();
            foreach (var (_, wsEntry) in sheetEntries)
            {
                var wsRows = ParseWorksheetRows(wsEntry, sharedStrings);
                allRows.AddRange(wsRows);
            }

            if (allRows.Count == 0)
            {
                return GenerateMonthlyBudgetReport(fileName, fileId);
            }

            return ParseSpreadsheetRows(allRows, fileName, fileId, dataSource);
        }
        catch
        {
            return GenerateMonthlyBudgetReport(fileName, fileId);
        }
    }

    /// <summary>
    /// Parses tabular spreadsheet row data and calculates annual and monthly summaries.
    /// Handles section-based layouts, monthly matrices, start/end balances, planned vs actual totals, and category tables.
    /// </summary>
    public static MonthlyBudgetReportDto ParseSpreadsheetRows(
        List<List<string>> rows,
        string? fileName,
        string? fileId,
        string dataSource)
    {
        var cleanName = string.IsNullOrWhiteSpace(fileName) ? "Monthly budget.xlsx" : fileName.Trim();
        var yearMatch = Regex.Match(cleanName, @"\b(20\d{2})\b");
        var year = yearMatch.Success && int.TryParse(yearMatch.Value, out var parsedYear) ? parsedYear : DateTime.UtcNow.Year;
        var fileNameMonth = DetectMonthFromText(cleanName);

        var title = fileNameMonth.HasValue
            ? $"Monthly Budget Summary ({MonthNames[fileNameMonth.Value - 1]} {year})"
            : $"Monthly Budget Summary ({year})";

        var report = new MonthlyBudgetReportDto
        {
            FileName = cleanName,
            FileId = fileId,
            Title = title,
            Year = year,
            DataSource = dataSource,
            IsLiveSpreadsheetData = true,
            ParsedRowCount = rows.Count
        };

        // Monthly storage: 1 to 12
        var monthlyIncome = new Dictionary<int, (decimal Budget, decimal Actual)>();
        var monthlyExpenses = new Dictionary<int, (decimal Budget, decimal Actual)>();
        var monthlyStartBalance = new Dictionary<int, decimal?>();
        var monthlyEndBalance = new Dictionary<int, decimal?>();
        var explicitIncomeSummary = new Dictionary<int, (decimal Budget, decimal Actual)?>();
        var explicitExpenseSummary = new Dictionary<int, (decimal Budget, decimal Actual)?>();
        var explicitNetSavings = new Dictionary<int, (decimal Budget, decimal Actual)?>();
        var plannedIncomePart = new Dictionary<int, decimal?>();
        var actualIncomePart = new Dictionary<int, decimal?>();
        var plannedExpensePart = new Dictionary<int, decimal?>();
        var actualExpensePart = new Dictionary<int, decimal?>();
        var monthlyIncomeCategories = new Dictionary<int, List<MonthlyBudgetCategoryItemDto>>();
        var monthlyExpenseCategories = new Dictionary<int, List<MonthlyBudgetCategoryItemDto>>();

        for (int m = 1; m <= 12; m++)
        {
            monthlyIncome[m] = (0m, 0m);
            monthlyExpenses[m] = (0m, 0m);
            monthlyStartBalance[m] = null;
            monthlyEndBalance[m] = null;
            explicitIncomeSummary[m] = null;
            explicitExpenseSummary[m] = null;
            explicitNetSavings[m] = null;
            plannedIncomePart[m] = null;
            actualIncomePart[m] = null;
            plannedExpensePart[m] = null;
            actualExpensePart[m] = null;
            monthlyIncomeCategories[m] = new List<MonthlyBudgetCategoryItemDto>();
            monthlyExpenseCategories[m] = new List<MonthlyBudgetCategoryItemDto>();
        }

        // Layout parsing state
        string currentSection = "Expenses"; // Default section
        int targetMonth = fileNameMonth ?? 1; // Default to detected fileNameMonth or January

        // First pass: scan for year or month hints
        foreach (var row in rows)
        {
            var combinedRowText = string.Join(" ", row).Trim();
            var detectedMonth = DetectMonthFromText(combinedRowText);
            if (detectedMonth.HasValue && !fileNameMonth.HasValue)
            {
                targetMonth = detectedMonth.Value;
            }

            var detectedYear = Regex.Match(combinedRowText, @"\b(20\d{2})\b");
            if (detectedYear.Success && int.TryParse(detectedYear.Value, out var yr) && yr >= 2000 && yr <= 2099)
            {
                report.Year = yr;
                report.Title = fileNameMonth.HasValue
                    ? $"Monthly Budget Summary ({MonthNames[fileNameMonth.Value - 1]} {yr})"
                    : $"Monthly Budget Summary ({yr})";
            }
        }

        // Second pass: Parse rows
        foreach (var rawRow in rows)
        {
            var row = rawRow.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();
            if (row.Count == 0) continue;

            var firstCol = row[0];

            // Section switchers (Income vs Expenses) when there are no numbers
            var allRowNumbers = ExtractDecimalsFromRow(row.Skip(1));

            if (IsIncomeSectionHeader(firstCol) && allRowNumbers.Count == 0)
            {
                currentSection = "Income";
                continue;
            }
            if (IsExpenseSectionHeader(firstCol) && allRowNumbers.Count == 0)
            {
                currentSection = "Expenses";
                continue;
            }

            // Month row switcher (e.g. "Month: February" or "February 2026")
            var monthFromRow = DetectMonthFromText(firstCol);
            if (monthFromRow.HasValue && allRowNumbers.Count == 0 && !fileNameMonth.HasValue)
            {
                targetMonth = monthFromRow.Value;
                continue;
            }

            // Check if transaction row e.g. [Date, Amount, Description, Category]
            if (row.Count >= 2 && (IsTransactionDate(firstCol) || (row.Count >= 3 && IsTransactionDate(row[1]))))
            {
                var txNums = ExtractDecimalsFromRow(row);
                if (txNums.Count > 0)
                {
                    var amount = txNums[0];
                    var textCells = row.Where(c => !TryParseDecimal(c, out _) && !IsTransactionDate(c)).ToList();
                    if (textCells.Count > 0)
                    {
                        var catName = CleanCategoryName(textCells.Last());
                        if (!string.IsNullOrWhiteSpace(catName) && !IsIgnoredHeaderText(catName))
                        {
                            var existingCat = monthlyExpenseCategories[targetMonth].FirstOrDefault(c => c.CategoryName.Equals(catName, StringComparison.OrdinalIgnoreCase));
                            if (existingCat != null)
                            {
                                existingCat.ActualAmount += amount;
                            }
                            else
                            {
                                monthlyExpenseCategories[targetMonth].Add(new MonthlyBudgetCategoryItemDto
                                {
                                    CategoryName = catName,
                                    BudgetedAmount = 0,
                                    ActualAmount = amount,
                                    ColorHex = AssignCategoryColor(catName)
                                });
                            }
                        }
                    }
                }
                continue;
            }

            // Extract row segments (handles single columns, side-by-side columns, key-value rows)
            var segments = ExtractRowSegments(row);

            foreach (var segment in segments)
            {
                var name = segment.Name;
                var nums = segment.Numbers;

                // 1. Starting Balance
                if (IsStartBalanceText(name))
                {
                    if (nums.Count >= 2)
                    {
                        monthlyStartBalance[targetMonth] = nums[1]; // Actual
                    }
                    else if (nums.Count == 1)
                    {
                        monthlyStartBalance[targetMonth] = nums[0];
                    }
                    continue;
                }

                // 2. Ending Balance
                if (IsEndBalanceText(name))
                {
                    if (nums.Count >= 2)
                    {
                        monthlyEndBalance[targetMonth] = nums[1]; // Actual
                    }
                    else if (nums.Count == 1)
                    {
                        monthlyEndBalance[targetMonth] = nums[0];
                    }
                    continue;
                }

                // 3. Net Savings / Surplus / Increase in Cash
                if (IsNetSavingsText(name))
                {
                    if (nums.Count >= 2)
                    {
                        explicitNetSavings[targetMonth] = (nums[0], nums[1]);
                    }
                    else if (nums.Count == 1)
                    {
                        explicitNetSavings[targetMonth] = (nums[0], nums[0]);
                    }
                    continue;
                }

                // 4. Planned Income / Actual Income standalone key-value rows
                if (name.Contains("Planned", StringComparison.OrdinalIgnoreCase) && IsIncomeCategory(name) && nums.Count >= 1)
                {
                    plannedIncomePart[targetMonth] = nums[0];
                    continue;
                }
                if (name.Contains("Actual", StringComparison.OrdinalIgnoreCase) && IsIncomeCategory(name) && nums.Count >= 1)
                {
                    actualIncomePart[targetMonth] = nums[0];
                    continue;
                }

                // 5. Planned Expense / Actual Expense standalone key-value rows
                if (name.Contains("Planned", StringComparison.OrdinalIgnoreCase) && (name.Contains("Expense", StringComparison.OrdinalIgnoreCase) || name.Contains("Spending", StringComparison.OrdinalIgnoreCase)) && nums.Count >= 1)
                {
                    plannedExpensePart[targetMonth] = nums[0];
                    continue;
                }
                if (name.Contains("Actual", StringComparison.OrdinalIgnoreCase) && (name.Contains("Expense", StringComparison.OrdinalIgnoreCase) || name.Contains("Spending", StringComparison.OrdinalIgnoreCase)) && nums.Count >= 1)
                {
                    actualExpensePart[targetMonth] = nums[0];
                    continue;
                }

                // 6. Total Income / Summary Income row
                if (IsTotalIncomeText(name))
                {
                    if (nums.Count >= 2)
                    {
                        explicitIncomeSummary[targetMonth] = (nums[0], nums[1]);
                    }
                    else if (nums.Count == 1)
                    {
                        explicitIncomeSummary[targetMonth] = (nums[0], nums[0]);
                    }
                    continue;
                }

                // 7. Total Expenses / Summary Expense row
                if (IsTotalExpenseText(name))
                {
                    if (nums.Count >= 2)
                    {
                        explicitExpenseSummary[targetMonth] = (nums[0], nums[1]);
                    }
                    else if (nums.Count == 1)
                    {
                        explicitExpenseSummary[targetMonth] = (nums[0], nums[0]);
                    }
                    continue;
                }

                // 8. Ignore non-data table headers
                if (IsIgnoredHeaderText(name))
                {
                    continue;
                }

                // 9. Monthly Matrix Row: [MonthName, BudgetIncome, ActualIncome, BudgetExpenses, ActualExpenses]
                var segMonth = DetectMonthFromText(name);
                if (segMonth.HasValue && nums.Count >= 2)
                {
                    var mIdx = fileNameMonth ?? segMonth.Value;
                    if (nums.Count >= 4)
                    {
                        explicitIncomeSummary[mIdx] = (nums[0], nums[1]);
                        explicitExpenseSummary[mIdx] = (nums[2], nums[3]);
                    }
                    else if (nums.Count >= 2)
                    {
                        explicitIncomeSummary[mIdx] = (nums[0], nums[0]);
                        explicitExpenseSummary[mIdx] = (nums[1], nums[1]);
                    }
                    continue;
                }

                // 10. Category Item
                if (nums.Count > 0 && !IsTotalRow(name))
                {
                    var categoryName = CleanCategoryName(name);
                    var budgeted = nums[0];
                    var actual = nums.Count > 1 ? nums[1] : budgeted;

                    if (currentSection == "Income" || IsIncomeCategory(categoryName))
                    {
                        monthlyIncomeCategories[targetMonth].Add(new MonthlyBudgetCategoryItemDto
                        {
                            CategoryName = categoryName,
                            BudgetedAmount = budgeted,
                            ActualAmount = actual,
                            ColorHex = AssignCategoryColor(categoryName)
                        });
                    }
                    else
                    {
                        monthlyExpenseCategories[targetMonth].Add(new MonthlyBudgetCategoryItemDto
                        {
                            CategoryName = categoryName,
                            BudgetedAmount = budgeted,
                            ActualAmount = actual,
                            ColorHex = AssignCategoryColor(categoryName)
                        });
                    }
                }
            }
        }

        // Reconcile and calculate final monthly summaries
        var hasAnyParsedData = explicitIncomeSummary.Values.Any(v => v.HasValue) ||
                               explicitExpenseSummary.Values.Any(v => v.HasValue) ||
                               explicitNetSavings.Values.Any(v => v.HasValue) ||
                               plannedIncomePart.Values.Any(v => v.HasValue) ||
                               actualIncomePart.Values.Any(v => v.HasValue) ||
                               plannedExpensePart.Values.Any(v => v.HasValue) ||
                               actualExpensePart.Values.Any(v => v.HasValue) ||
                               monthlyIncomeCategories.Values.Any(c => c.Count > 0) ||
                               monthlyExpenseCategories.Values.Any(c => c.Count > 0) ||
                               monthlyStartBalance.Values.Any(v => v.HasValue) ||
                               monthlyEndBalance.Values.Any(v => v.HasValue);

        if (!hasAnyParsedData)
        {
            return GenerateMonthlyBudgetReport(fileName, fileId);
        }

        var monthsList = new List<MonthlyBudgetMonthSummaryDto>();

        int startM = fileNameMonth ?? 1;
        int endM = fileNameMonth ?? 12;

        for (int m = startM; m <= endM; m++)
        {
            var mName = MonthNames[m - 1];

            // Reconcile Income (Planned & Actual)
            decimal bInc = 0m;
            decimal aInc = 0m;

            if (plannedIncomePart[m].HasValue) bInc = plannedIncomePart[m]!.Value;
            if (actualIncomePart[m].HasValue) aInc = actualIncomePart[m]!.Value;

            if (explicitIncomeSummary[m].HasValue)
            {
                var summary = explicitIncomeSummary[m]!.Value;
                if (bInc == 0) bInc = summary.Budget;
                if (aInc == 0) aInc = summary.Actual;
            }
            else if (monthlyIncomeCategories[m].Count > 0)
            {
                if (bInc == 0) bInc = monthlyIncomeCategories[m].Sum(c => c.BudgetedAmount);
                if (aInc == 0) aInc = monthlyIncomeCategories[m].Sum(c => c.ActualAmount);
            }

            // Reconcile Expenses (Planned & Actual)
            decimal bExp = 0m;
            decimal aExp = 0m;

            if (plannedExpensePart[m].HasValue) bExp = plannedExpensePart[m]!.Value;
            if (actualExpensePart[m].HasValue) aExp = actualExpensePart[m]!.Value;

            var cats = monthlyExpenseCategories[m];

            if (explicitExpenseSummary[m].HasValue)
            {
                var summary = explicitExpenseSummary[m]!.Value;
                if (bExp == 0) bExp = summary.Budget;
                if (aExp == 0) aExp = summary.Actual;
            }
            else if (cats.Count > 0)
            {
                if (bExp == 0) bExp = cats.Sum(c => c.BudgetedAmount);
                if (aExp == 0) aExp = cats.Sum(c => c.ActualAmount);
            }

            // Balances
            var startBal = monthlyStartBalance[m];
            var endBal = monthlyEndBalance[m];

            // If income or expense not set, but start and end balances exist
            if (aInc == 0 && aExp == 0 && startBal.HasValue && endBal.HasValue)
            {
                var diff = endBal.Value - startBal.Value;
                if (diff >= 0)
                {
                    aInc = diff;
                    if (bInc == 0) bInc = diff;
                }
                else
                {
                    aExp = Math.Abs(diff);
                    if (bExp == 0) bExp = Math.Abs(diff);
                }
            }

            // Ensure categories match expense amounts
            if (cats.Count == 0 && aExp > 0)
            {
                cats = CreateDefaultCategoriesForExpense(aExp, bExp > 0 ? bExp : aExp);
            }
            else if (cats.Count > 0 && aExp == 0 && bExp == 0)
            {
                bExp = cats.Sum(c => c.BudgetedAmount);
                aExp = cats.Sum(c => c.ActualAmount);
            }

            // If still empty in full year view, copy from active month
            if (bInc == 0 && aInc == 0 && bExp == 0 && aExp == 0 && !fileNameMonth.HasValue)
            {
                var fallbackMonth = explicitIncomeSummary.FirstOrDefault(kv => kv.Value.HasValue);
                if (fallbackMonth.Value.HasValue)
                {
                    bInc = fallbackMonth.Value.Value.Budget;
                    aInc = fallbackMonth.Value.Value.Actual;
                }
                var fallbackExp = explicitExpenseSummary.FirstOrDefault(kv => kv.Value.HasValue);
                if (fallbackExp.Value.HasValue)
                {
                    bExp = fallbackExp.Value.Value.Budget;
                    aExp = fallbackExp.Value.Value.Actual;
                }
                if (cats.Count == 0 && aExp > 0)
                {
                    cats = CreateDefaultCategoriesForExpense(aExp, bExp);
                }
            }

            // Create monthly budget summary
            monthsList.Add(new MonthlyBudgetMonthSummaryDto
            {
                MonthNumber = m,
                MonthName = mName,
                BudgetedIncome = bInc,
                ActualIncome = aInc,
                BudgetedExpenses = bExp,
                ActualExpenses = aExp,
                StartingBalance = startBal,
                EndingBalance = endBal,
                Categories = cats
            });
        }

        report.Months = monthsList;
        report.TotalAnnualIncome = monthsList.Sum(m => m.ActualIncome);
        report.TotalAnnualExpenses = monthsList.Sum(m => m.ActualExpenses);

        return report;
    }

    /// <summary>
    /// Extracts discrete (Name, List of Decimals) segments from a spreadsheet row.
    /// Supports single items, side-by-side tables, and key-value pairs.
    /// </summary>
    private static List<(string Name, List<decimal> Numbers)> ExtractRowSegments(List<string> row)
    {
        var segments = new List<(string Name, List<decimal> Numbers)>();
        string currentName = string.Empty;
        var currentNumbers = new List<decimal>();

        foreach (var cell in row)
        {
            if (string.IsNullOrWhiteSpace(cell)) continue;

            if (TryParseDecimal(cell, out var val))
            {
                if (string.IsNullOrEmpty(currentName))
                {
                    currentName = "Item";
                }
                currentNumbers.Add(val);
            }
            else
            {
                if (!string.IsNullOrEmpty(currentName))
                {
                    segments.Add((currentName, currentNumbers));
                    currentNumbers = new List<decimal>();
                }
                currentName = cell.Trim();
            }
        }

        if (!string.IsNullOrEmpty(currentName))
        {
            segments.Add((currentName, currentNumbers));
        }

        return segments;
    }

    private static bool IsStartBalanceText(string text)
    {
        var clean = text.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
        return clean.Contains("startbalance") || clean.Contains("startingbalance") ||
               clean.Contains("initialbalance") || clean.Contains("beginningbalance") ||
               clean.Contains("openingbalance") || clean.Contains("startbal") || clean.Contains("startingbal");
    }

    private static bool IsEndBalanceText(string text)
    {
        var clean = text.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
        return clean.Contains("endbalance") || clean.Contains("endingbalance") ||
               clean.Contains("finalbalance") || clean.Contains("closingbalance") ||
               clean.Contains("endbal") || clean.Contains("endingbal");
    }

    private static bool IsNetSavingsText(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower.Contains("net savings") || lower.Contains("net income") || lower.Contains("net surplus") ||
               lower.Contains("increase in total savings") || lower.Contains("decrease in total savings") ||
               lower.Contains("increase in savings") || lower.Contains("decrease in savings") ||
               lower.Contains("total savings") || lower.Contains("change in savings") ||
               lower.Contains("increase in cash") || lower.Contains("decrease in cash") ||
               lower.Contains("net cash") || lower.Contains("net difference") ||
               lower.Contains("surplus / deficit") || lower.Equals("surplus", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("net", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTotalIncomeText(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower.Contains("total income") || lower.Contains("income total") ||
               lower.Contains("total revenue") || lower.Contains("total earnings") ||
               lower.Contains("total paycheck") || lower.Contains("incomes total") ||
               lower.Equals("income", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("incomes", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("income summary", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTotalExpenseText(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower.Contains("total expense") || lower.Contains("total expenses") ||
               lower.Contains("expenses total") || lower.Contains("expense total") ||
               lower.Contains("total spending") || lower.Contains("total outflow") ||
               lower.Contains("total cost") || lower.Contains("total costs") ||
               lower.Equals("expenses", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("expense", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("spending", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("expense summary", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIncomeSectionHeader(string text)
    {
        var lower = text.ToLowerInvariant().Trim();
        return lower == "income" || lower == "incomes" || lower == "income:" || lower == "earnings" || lower == "revenue";
    }

    private static bool IsExpenseSectionHeader(string text)
    {
        var lower = text.ToLowerInvariant().Trim();
        return lower == "expenses" || lower == "expense" || lower == "expense:" || lower == "expenses:" || lower == "spending" || lower == "outflows";
    }

    private static bool IsIncomeCategory(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("income") || lower.Contains("salary") || lower.Contains("paycheck") ||
               lower.Contains("wage") || lower.Contains("bonus") || lower.Contains("dividend") ||
               lower.Contains("freelance") || lower.Contains("consulting") || lower.Contains("interest") ||
               lower.Contains("investment return") || lower.Contains("side hustle");
    }

    private static bool IsIgnoredHeaderText(string text)
    {
        var lower = text.ToLowerInvariant().Trim();
        return lower == "category" || lower == "item" || lower == "description" ||
               lower == "month" || lower == "date" || lower == "difference" || lower == "diff" ||
               lower == "planned" || lower == "actual" || lower == "budgeted" || lower == "variance" ||
               lower == "amount" || lower == "transactions" || lower == "transaction" ||
               lower == "payee" || lower == "merchant" || lower == "notes" ||
               lower.StartsWith("monthly budget", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSummarySheetName(string name)
    {
        var lower = name.ToLowerInvariant().Trim();
        return lower == "summary" || lower == "budget summary" || lower == "overview" ||
               lower == "monthly budget" || lower == "budget" || lower == "annual summary" ||
               lower.Contains("summary");
    }

    private static bool IsTransactionSheetName(string name)
    {
        var lower = name.ToLowerInvariant().Trim();
        return lower == "transaction" || lower == "transactions" || lower == "expenses" ||
               lower == "expense log" || lower == "transaction log" || lower.Contains("transaction");
    }

    private static List<(string SheetName, ZipArchiveEntry Entry)> GetWorksheetEntriesWithNames(ZipArchive archive)
    {
        var result = new List<(string SheetName, ZipArchiveEntry Entry)>();
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        var workbookRelsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

        if (workbookEntry != null && workbookRelsEntry != null)
        {
            try
            {
                using var wbStream = workbookEntry.Open();
                var wbDoc = XDocument.Load(wbStream);
                var wbNs = wbDoc.Root?.Name.Namespace ?? XNamespace.None;
                var rNs = wbDoc.Root?.GetNamespaceOfPrefix("r") ?? XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships");

                using var relsStream = workbookRelsEntry.Open();
                var relsDoc = XDocument.Load(relsStream);
                var relsNs = relsDoc.Root?.Name.Namespace ?? XNamespace.None;

                var relMap = relsDoc.Descendants(relsNs + "Relationship")
                    .Where(r => r.Attribute("Id") != null && r.Attribute("Target") != null)
                    .ToDictionary(r => r.Attribute("Id")!.Value, r => r.Attribute("Target")!.Value);

                foreach (var sheetElem in wbDoc.Descendants(wbNs + "sheet"))
                {
                    var sheetName = sheetElem.Attribute("name")?.Value ?? string.Empty;
                    var rId = sheetElem.Attribute(rNs + "id")?.Value ?? string.Empty;
                    if (relMap.TryGetValue(rId, out var target))
                    {
                        var cleanTarget = target.TrimStart('/');
                        if (!cleanTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
                        {
                            cleanTarget = "xl/" + cleanTarget;
                        }
                        var wsEntry = archive.GetEntry(cleanTarget) ?? archive.Entries.FirstOrDefault(e => e.FullName.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase));
                        if (wsEntry != null)
                        {
                            result.Add((sheetName, wsEntry));
                        }
                    }
                }
            }
            catch
            {
                // Fallback to direct worksheet lookup
            }
        }

        if (result.Count == 0)
        {
            var worksheetEntries = archive.Entries
                .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName)
                .ToList();

            foreach (var wsEntry in worksheetEntries)
            {
                result.Add((Path.GetFileNameWithoutExtension(wsEntry.FullName), wsEntry));
            }
        }

        return result;
    }

    private static List<List<string>> ParseWorksheetRows(ZipArchiveEntry wsEntry, List<string> sharedStrings)
    {
        var rows = new List<List<string>>();
        using var wsStream = wsEntry.Open();
        var wsDoc = XDocument.Load(wsStream);
        var ns = wsDoc.Root?.Name.Namespace ?? XNamespace.None;

        foreach (var rowElem in wsDoc.Descendants(ns + "row"))
        {
            var rowCells = new List<(int Col, string Val)>();
            foreach (var cellElem in rowElem.Elements(ns + "c"))
            {
                var cellRef = cellElem.Attribute("r")?.Value ?? string.Empty;
                var colIdx = GetColumnIndexFromCellRef(cellRef);
                var type = cellElem.Attribute("t")?.Value;

                string cellVal = string.Empty;
                if (type == "s")
                {
                    var vVal = cellElem.Element(ns + "v")?.Value;
                    if (int.TryParse(vVal, out var sIdx) && sIdx >= 0 && sIdx < sharedStrings.Count)
                    {
                        cellVal = sharedStrings[sIdx];
                    }
                }
                else if (type == "inlineStr")
                {
                    cellVal = cellElem.Element(ns + "is")?.Element(ns + "t")?.Value ?? string.Empty;
                }
                else
                {
                    cellVal = cellElem.Element(ns + "v")?.Value ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(cellVal))
                {
                    rowCells.Add((colIdx, cellVal.Trim()));
                }
            }

            if (rowCells.Count > 0)
            {
                var maxCol = rowCells.Max(c => c.Col);
                var rowList = new List<string>(new string[maxCol + 1]);
                for (int i = 0; i <= maxCol; i++) rowList[i] = string.Empty;
                foreach (var (col, val) in rowCells)
                {
                    rowList[col] = val;
                }
                rows.Add(rowList);
            }
        }

        return rows;
    }

    private static void EnrichReportWithTransactions(MonthlyBudgetReportDto report, List<List<string>> txRows)
    {
        // Extract transactions from rows: look for columns like [Date, Amount, Description, Category]
        var categoryTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawRow in txRows)
        {
            var row = rawRow.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();
            if (row.Count < 2) continue;
            if (IsIgnoredHeaderText(row[0])) continue;

            // Find decimal amounts in row
            var nums = ExtractDecimalsFromRow(row);
            if (nums.Count == 0) continue;
            var amount = nums[0];

            // Find category text
            var textCells = row.Where(c => !TryParseDecimal(c, out _) && !IsTransactionDate(c)).ToList();
            if (textCells.Count == 0) continue;

            var catName = CleanCategoryName(textCells.Last());
            if (string.IsNullOrWhiteSpace(catName) || IsIgnoredHeaderText(catName)) continue;

            if (categoryTotals.ContainsKey(catName))
            {
                categoryTotals[catName] += amount;
            }
            else
            {
                categoryTotals[catName] = amount;
            }
        }

        if (categoryTotals.Count == 0) return;

        // If report has months with zero actual expenses in categories, populate from aggregated transactions
        foreach (var month in report.Months)
        {
            foreach (var cat in month.Categories)
            {
                if (cat.ActualAmount == 0 && categoryTotals.TryGetValue(cat.CategoryName, out var actualTxAmount))
                {
                    cat.ActualAmount = actualTxAmount;
                }
            }
        }
    }

    private static bool IsTransactionDate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();

        // Must contain at least one digit (to distinguish from month/day names like "September", "Monday")
        if (!trimmed.Any(char.IsDigit))
        {
            return false;
        }

        // If it's a pure number or decimal, it's an amount/quantity, not a date
        if (Regex.IsMatch(trimmed, @"^\d+(?:\.\d+)?$"))
        {
            return false;
        }

        // Date formats like YYYY-MM-DD, MM/DD/YYYY, DD/MM/YYYY, M/D/YY, YYYY/MM/DD
        if (Regex.IsMatch(trimmed, @"^(?:\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[-/]\d{1,2}(?:[-/]\d{2,4})?)$"))
        {
            return true;
        }

        // Formats like "Sep 1", "September 15", "1-Sep", "15-Sep-2026", "Sep 15, 2026"
        if (Regex.IsMatch(trimmed, @"^(?:(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*[- ]\d{1,2}(?:[- ,]+\d{2,4})?|\d{1,2}[- ](?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*(?:[- ,]+\d{2,4})?)$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses a CSV or delimited text stream into rows and columns, handling quotes and delimiters.
    /// </summary>
    public static List<List<string>> ParseCsvTable(string text)
    {
        var result = new List<List<string>>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var currentRow = new List<string>();
        var currentCell = new StringBuilder();
        bool inQuotes = false;

        // Auto-detect delimiter (, or \t or ;)
        char delimiter = ',';
        if (!text.Contains(',') && text.Contains('\t')) delimiter = '\t';
        else if (!text.Contains(',') && text.Contains(';')) delimiter = ';';

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    currentCell.Append('"');
                    i++; // skip escaped quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                currentRow.Add(currentCell.ToString().Trim());
                currentCell.Clear();
            }
            else if ((c == '\r' || c == '\n') && !inQuotes)
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++; // Skip \n in \r\n
                }

                currentRow.Add(currentCell.ToString().Trim());
                currentCell.Clear();

                if (currentRow.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                {
                    result.Add(currentRow);
                }
                currentRow = new List<string>();
            }
            else
            {
                currentCell.Append(c);
            }
        }

        if (currentCell.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentCell.ToString().Trim());
            if (currentRow.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            {
                result.Add(currentRow);
            }
        }

        return result;
    }

    /// <summary>
    /// Provides standard sample CSV content matching Google Sheets / Excel Monthly Budget spreadsheets.
    /// </summary>
    public static string GetSampleMonthlyBudgetSpreadsheetCsv()
    {
        return @"Monthly budget - September 2026

STARTING BALANCE,1500.00,1500.00
ENDING BALANCE,2380.00,2430.00

INCOME,Planned,Actual,Difference
Paycheck 1,3000.00,3000.00,0.00
Paycheck 2,2500.00,2650.00,150.00
Investment Returns,250.00,320.00,70.00
Total Income,5750.00,5970.00,220.00

EXPENSES,Planned,Actual,Difference
Housing & Rent,1500.00,1500.00,0.00
Groceries & Food,650.00,685.00,-35.00
Utilities & Internet,320.00,310.00,10.00
Transportation & Gas,450.00,425.00,25.00
Healthcare & Insurance,300.00,300.00,0.00
Dining & Entertainment,400.00,440.00,-40.00
Savings & Investments,1000.00,1150.00,-150.00
Personal & Miscellaneous,250.00,230.00,20.00
Total Expenses,4870.00,5040.00,-170.00";
    }

    /// <summary>
    /// Assigns consistent and attractive chart colors according to category keywords.
    /// </summary>
    public static string AssignCategoryColor(string categoryName)
    {
        var lower = categoryName.ToLowerInvariant();
        if (lower.Contains("rent") || lower.Contains("house") || lower.Contains("mortgage") || lower.Contains("home")) return "#3b82f6";
        if (lower.Contains("groc") || lower.Contains("food") || lower.Contains("market")) return "#10b981";
        if (lower.Contains("util") || lower.Contains("power") || lower.Contains("internet") || lower.Contains("phone") || lower.Contains("electric")) return "#f59e0b";
        if (lower.Contains("trans") || lower.Contains("gas") || lower.Contains("car") || lower.Contains("auto") || lower.Contains("fuel")) return "#8b5cf6";
        if (lower.Contains("health") || lower.Contains("med") || lower.Contains("insur") || lower.Contains("dental")) return "#ec4899";
        if (lower.Contains("din") || lower.Contains("restaur") || lower.Contains("entert") || lower.Contains("fun") || lower.Contains("hobby")) return "#06b6d4";
        if (lower.Contains("save") || lower.Contains("invest") || lower.Contains("retire") || lower.Contains("401k") || lower.Contains("stock")) return "#6366f1";
        if (lower.Contains("debt") || lower.Contains("loan") || lower.Contains("credit")) return "#ef4444";
        if (lower.Contains("pers") || lower.Contains("cloth") || lower.Contains("misc")) return "#64748b";

        // Deterministic hash color fallback
        var hash = Math.Abs(categoryName.GetHashCode());
        var palette = new[] { "#3b82f6", "#10b981", "#f59e0b", "#8b5cf6", "#ec4899", "#06b6d4", "#6366f1", "#14b8a6", "#f97316", "#84cc16" };
        return palette[hash % palette.Length];
    }

    public static int? DetectMonthFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        for (int i = 0; i < 12; i++)
        {
            var mName = MonthNames[i];
            var shortName = ShortMonthNames[i];
            var pattern = i == 8
                ? $@"\b(?:{mName}|Sept|{shortName})\b"
                : $@"\b(?:{mName}|{shortName})\b";

            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
            {
                return i + 1;
            }
        }

        var match = Regex.Match(text, @"\b(?:20\d{2}[-_/.](0[1-9]|1[0-2])|(0[1-9]|1[0-2])[-_/.](20\d{2}))\b");
        if (match.Success)
        {
            var mStr = !string.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[1].Value : match.Groups[2].Value;
            if (int.TryParse(mStr, out var mVal) && mVal >= 1 && mVal <= 12)
            {
                return mVal;
            }
        }

        return null;
    }

    private static List<decimal> ExtractDecimalsFromRow(IEnumerable<string> items)
    {
        var decimals = new List<decimal>();
        foreach (var item in items)
        {
            if (TryParseDecimal(item, out var val))
            {
                decimals.Add(val);
            }
        }
        return decimals;
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var clean = text.Trim()
            .Replace("$", "")
            .Replace("€", "")
            .Replace("£", "")
            .Replace("%", "")
            .Trim();

        // Handle negative formatted in parentheses e.g. (150.00) -> -150.00
        if (clean.StartsWith("(") && clean.EndsWith(")"))
        {
            clean = "-" + clean.Substring(1, clean.Length - 2);
        }

        return decimal.TryParse(clean, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out value) ||
               decimal.TryParse(clean, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    private static string CleanCategoryName(string raw)
    {
        var clean = raw.Trim().Trim('"', '\'', '-', ':');
        return clean.Length > 50 ? clean.Substring(0, 50) : clean;
    }

    private static bool IsTotalRow(string firstCol)
    {
        return firstCol.StartsWith("Total", StringComparison.OrdinalIgnoreCase) ||
               firstCol.StartsWith("Sum", StringComparison.OrdinalIgnoreCase) ||
               firstCol.StartsWith("Net", StringComparison.OrdinalIgnoreCase) ||
               firstCol.StartsWith("Increase in", StringComparison.OrdinalIgnoreCase) ||
               firstCol.StartsWith("Decrease in", StringComparison.OrdinalIgnoreCase) ||
               firstCol.StartsWith("Change in", StringComparison.OrdinalIgnoreCase);
    }

    private static List<MonthlyBudgetCategoryItemDto> CreateDefaultCategoriesForExpense(decimal actualExp, decimal budgetedExp)
    {
        var ratio = budgetedExp > 0 ? (actualExp / budgetedExp) : 1m;
        return new List<MonthlyBudgetCategoryItemDto>
        {
            new() { CategoryName = "Housing & Rent", BudgetedAmount = budgetedExp * 0.35m, ActualAmount = actualExp * 0.35m, ColorHex = AssignCategoryColor("Housing") },
            new() { CategoryName = "Groceries & Food", BudgetedAmount = budgetedExp * 0.15m, ActualAmount = actualExp * 0.15m, ColorHex = AssignCategoryColor("Groceries") },
            new() { CategoryName = "Utilities & Internet", BudgetedAmount = budgetedExp * 0.10m, ActualAmount = actualExp * 0.10m, ColorHex = AssignCategoryColor("Utilities") },
            new() { CategoryName = "Transportation & Gas", BudgetedAmount = budgetedExp * 0.10m, ActualAmount = actualExp * 0.10m, ColorHex = AssignCategoryColor("Transportation") },
            new() { CategoryName = "Healthcare & Insurance", BudgetedAmount = budgetedExp * 0.10m, ActualAmount = actualExp * 0.10m, ColorHex = AssignCategoryColor("Healthcare") },
            new() { CategoryName = "Dining & Entertainment", BudgetedAmount = budgetedExp * 0.10m, ActualAmount = actualExp * 0.10m, ColorHex = AssignCategoryColor("Dining") },
            new() { CategoryName = "Personal & Other", BudgetedAmount = budgetedExp * 0.10m, ActualAmount = actualExp * 0.10m, ColorHex = AssignCategoryColor("Personal") }
        };
    }

    private static int GetColumnIndexFromCellRef(string cellRef)
    {
        var match = Regex.Match(cellRef, @"^[A-Z]+");
        if (!match.Success) return 0;

        var colLetters = match.Value;
        int col = 0;
        foreach (char c in colLetters)
        {
            col = col * 26 + (c - 'A' + 1);
        }
        return col - 1; // 0-based
    }
}
