using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;

namespace PersonalFinance.ApiService.Features.GoogleDrive;

/// <summary>
/// Reads monthly budget reports and transaction logs from spreadsheet content (CSV text or XLSX workbooks).
/// Pure, in-process parsing: downloading is <see cref="IGoogleDriveFileSource"/>'s job.
/// When nothing usable is found it returns an unavailable result (no months or transactions, an
/// <c>ErrorMessage</c>, and <c>IsLiveSpreadsheetData = false</c>); it never invents figures.
/// </summary>
internal static class BudgetSpreadsheetParser
{
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
    /// A report for a spreadsheet that could not be read: no months, not live data, and the reason.
    /// </summary>
    public static MonthlyBudgetReportDto Unavailable(string? fileName, string? fileId, string reason)
    {
        return new MonthlyBudgetReportDto
        {
            FileName = fileName ?? string.Empty,
            FileId = fileId,
            Title = "Budget report unavailable",
            DataSource = "Unavailable",
            IsLiveSpreadsheetData = false,
            ErrorMessage = reason
        };
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
            return Unavailable(fileName, fileId, "The spreadsheet is empty.");
        }

        List<List<string>> rows = ParseCsvTable(contentOrCsv);
        if (rows.Count == 0)
        {
            return Unavailable(fileName, fileId, "The spreadsheet has no rows.");
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
            List<string> sharedStrings = ReadSharedStrings(archive);

            // 2. Map sheet names and worksheet entries
            List<(string SheetName, ZipArchiveEntry Entry)> sheetEntries = GetWorksheetEntriesWithNames(archive);
            if (sheetEntries.Count == 0)
            {
                return Unavailable(fileName, fileId, "The workbook has no worksheets.");
            }

            (string SheetName, ZipArchiveEntry Entry) summarySheet = sheetEntries.FirstOrDefault(s => IsSummarySheetName(s.SheetName));
            var transactionSheets = sheetEntries.Where(s => IsTransactionSheetName(s.SheetName)).ToList();

            if (summarySheet.Entry != null)
            {
                List<List<string>> summaryRows = ParseWorksheetRows(summarySheet.Entry, sharedStrings);
                MonthlyBudgetReportDto report = ParseSpreadsheetRows(summaryRows, fileName, fileId, dataSource);

                if (transactionSheets.Count > 0)
                {
                    foreach ((string SheetName, ZipArchiveEntry Entry) txSheet in transactionSheets)
                    {
                        List<List<string>> txRows = ParseWorksheetRows(txSheet.Entry, sharedStrings);
                        EnrichReportWithTransactions(report, txRows);
                    }
                }

                return report;
            }

            var allRows = new List<List<string>>();
            foreach ((string _, ZipArchiveEntry? wsEntry) in sheetEntries)
            {
                List<List<string>> wsRows = ParseWorksheetRows(wsEntry, sharedStrings);
                allRows.AddRange(wsRows);
            }

            if (allRows.Count == 0)
            {
                return Unavailable(fileName, fileId, "The workbook's sheets are empty.");
            }

            return ParseSpreadsheetRows(allRows, fileName, fileId, dataSource);
        }
        catch (Exception ex) when (IsUnreadableWorkbook(ex))
        {
            return Unavailable(fileName, fileId, "The file is not a readable Excel workbook.");
        }
    }

    /// <summary>
    /// Reads the transaction log from an OpenXML Excel (.xlsx) workbook.
    /// Sheets named like "Transactions" are read first; other sheets are only scanned when none of those yield rows.
    /// </summary>
    public static SpreadsheetTransactionsDto ParseXlsxTransactions(
        Stream stream,
        string? fileName = null,
        string? fileId = null,
        string dataSource = "Google Drive Spreadsheet (Actual Data)")
    {
        var result = new SpreadsheetTransactionsDto
        {
            FileName = fileName ?? string.Empty,
            FileId = fileId,
            DataSource = dataSource
        };

        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            List<string> sharedStrings = ReadSharedStrings(archive);
            List<(string SheetName, ZipArchiveEntry Entry)> sheetEntries = GetWorksheetEntriesWithNames(archive);

            var preferredSheets = sheetEntries.Where(s => IsTransactionSheetName(s.SheetName)).ToList();
            var otherSheets = sheetEntries.Except(preferredSheets).ToList();

            foreach (List<(string SheetName, ZipArchiveEntry Entry)>? candidates in new[] { preferredSheets, otherSheets })
            {
                var sheetNames = new List<string>();
                foreach ((string? sheetName, ZipArchiveEntry? entry) in candidates)
                {
                    List<SpreadsheetTransactionDto> transactions = ParseTransactionRows(ParseWorksheetRows(entry, sharedStrings));
                    if (transactions.Count == 0) continue;

                    result.Transactions.AddRange(transactions);
                    sheetNames.Add(sheetName);
                }

                if (result.Transactions.Count > 0)
                {
                    result.SheetName = string.Join(", ", sheetNames);
                    return result;
                }
            }

            result.ErrorMessage = "No transactions table was found. Add a sheet with Date and Amount column headers.";
        }
        catch (Exception ex) when (IsUnreadableWorkbook(ex))
        {
            result.ErrorMessage = "The spreadsheet could not be read as an Excel workbook.";
        }

        return result;
    }

    /// <summary>
    /// Reads the transaction log from CSV / delimited text content.
    /// </summary>
    public static SpreadsheetTransactionsDto ParseCsvTransactions(
        string? csvContent,
        string? fileName = null,
        string? fileId = null,
        string dataSource = "Google Drive Spreadsheet (Actual Data)")
    {
        var result = new SpreadsheetTransactionsDto
        {
            FileName = fileName ?? string.Empty,
            FileId = fileId,
            DataSource = dataSource
        };

        if (!string.IsNullOrWhiteSpace(csvContent))
        {
            result.Transactions = ParseTransactionRows(ParseCsvTable(csvContent));
        }

        if (result.Transactions.Count == 0)
        {
            result.ErrorMessage = "No transactions table was found. Add a sheet with Date and Amount column headers.";
        }

        return result;
    }

    /// <summary>
    /// Extracts transactions from tabular rows. A header row containing "Date" and "Amount" starts a table;
    /// several tables can sit side by side (e.g. Google's Monthly budget template puts Expenses and Income next to each other).
    /// The section label above each table ("Expenses" / "Income") decides the transaction type.
    /// </summary>
    public static List<SpreadsheetTransactionDto> ParseTransactionRows(List<List<string>> rows)
    {
        var transactions = new List<SpreadsheetTransactionDto>();
        var tables = new List<TransactionTableLayout>();

        for (int r = 0; r < rows.Count; r++)
        {
            List<TransactionTableLayout> headerTables = DetectTransactionTables(rows, r);
            if (headerTables.Count > 0)
            {
                ApplySignBasedTypes(tables);
                tables = headerTables;
                continue;
            }

            foreach (TransactionTableLayout table in tables)
            {
                SpreadsheetTransactionDto? tx = ReadTransaction(rows[r], table);
                if (tx != null)
                {
                    table.Transactions.Add(tx);
                    transactions.Add(tx);
                }
            }
        }

        ApplySignBasedTypes(tables);
        return transactions;
    }

    private static List<TransactionTableLayout> DetectTransactionTables(List<List<string>> rows, int headerRow)
    {
        var tables = new List<TransactionTableLayout>();
        List<string> row = rows[headerRow];
        var dateCols = Enumerable.Range(0, row.Count).Where(i => IsDateHeader(row[i])).ToList();

        for (int t = 0; t < dateCols.Count; t++)
        {
            int start = dateCols[t];
            int end = t + 1 < dateCols.Count ? dateCols[t + 1] - 1 : row.Count - 1;

            int Find(Func<string, bool> predicate) =>
                Enumerable.Range(start, end - start + 1)
                    .FirstOrDefault(i => i != start && predicate(row[i].Trim().ToLowerInvariant()), -1);

            int amountCol = Find(h => h.Contains("amount") || h is "amt" or "cost" or "price" or "value");
            if (amountCol < 0) continue;

            tables.Add(new TransactionTableLayout
            {
                DateCol = start,
                AmountCol = amountCol,
                DescriptionCol = Find(h =>
                    h is "description" or "payee" or "merchant" or "memo" or "details" or "item" or "name" or "notes"
                        or "note"),
                CategoryCol = Find(h => h.Contains("category")),
                TypeCol = Find(h => h == "type"),
                SectionType = FindSectionType(rows, headerRow, start, end)
            });
        }

        return tables;
    }

    private static bool IsDateHeader(string text)
    {
        string lower = text.Trim().ToLowerInvariant();
        return lower == "date" || lower.EndsWith(" date", StringComparison.Ordinal);
    }

    // Looks a few rows above the header, within the table's columns, for an "Expenses" / "Income" section label

    private static string? FindSectionType(List<List<string>> rows, int headerRow, int startCol, int endCol)
    {
        for (int r = headerRow - 1; r >= Math.Max(0, headerRow - 3); r--)
        {
            List<string> row = rows[r];
            for (int c = startCol; c <= Math.Min(endCol, row.Count - 1); c++)
            {
                string? type = ClassifyTypeText(row[c]);
                if (type != null) return type;
            }
        }

        return null;
    }

    private static string? ClassifyTypeText(string text)
    {
        string lower = text.Trim().ToLowerInvariant();
        if (lower.Length == 0) return null;
        if (lower.Contains("income") || lower.Contains("deposit") || lower.Contains("credit"))
            return SpreadsheetTransactionDto.IncomeType;
        if (lower.Contains("expense") || lower.Contains("spending") || lower.Contains("debit") ||
            lower.Contains("withdrawal")) return SpreadsheetTransactionDto.ExpenseType;
        return null;
    }

    private static SpreadsheetTransactionDto? ReadTransaction(List<string> row, TransactionTableLayout table)
    {
        string Cell(int col) => col >= 0 && col < row.Count ? row[col].Trim() : string.Empty;

        if (!TryParseDecimal(Cell(table.AmountCol), out decimal amount)) return null;

        string dateText = Cell(table.DateCol);
        string description = Cell(table.DescriptionCol);
        string category = Cell(table.CategoryCol);
        if (dateText.Length == 0 && description.Length == 0 && category.Length == 0) return null;
        // Only "Total..." for descriptions: IsTotalRow's "Net"/"Sum" prefixes would drop payees like "Netflix"
        if (IsTotalRow(dateText) || description.StartsWith("Total", StringComparison.OrdinalIgnoreCase)) return null;

        return new SpreadsheetTransactionDto
        {
            Date = ParseSheetDate(dateText),
            DateText = dateText,
            Amount = Math.Round(amount, 2),
            Description = description,
            Category = category,
            Type = ClassifyTypeText(Cell(table.TypeCol)) ?? table.SectionType ?? SpreadsheetTransactionDto.ExpenseType
        };
    }

    // Unlabelled tables with signed amounts (bank-export style): negatives are spending, positives are income

    private static void ApplySignBasedTypes(List<TransactionTableLayout> tables)
    {
        foreach (TransactionTableLayout? table in tables.Where(t => t.SectionType == null && t.TypeCol < 0))
        {
            if (!table.Transactions.Any(t => t.Amount < 0)) continue;

            foreach (SpreadsheetTransactionDto tx in table.Transactions)
            {
                tx.Type = tx.Amount < 0 ? SpreadsheetTransactionDto.ExpenseType : SpreadsheetTransactionDto.IncomeType;
                tx.Amount = Math.Abs(tx.Amount);
            }
        }
    }

    // XLSX stores dates as OLE Automation serial numbers unless the cell holds text

    private static DateTime? ParseSheetDate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial))
        {
            return serial is >= 1 and < 2958466 ? DateTime.FromOADate(serial).Date : null;
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date) ||
            DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
        {
            return date.Date;
        }

        return null;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var sharedStrings = new List<string>();
        ZipArchiveEntry? sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStringsEntry == null) return sharedStrings;

        using Stream ssStream = sharedStringsEntry.Open();
        var ssDoc = XDocument.Load(ssStream);
        XNamespace ns = ssDoc.Root?.Name.Namespace ?? XNamespace.None;
        foreach (XElement si in ssDoc.Descendants(ns + "si"))
        {
            var sb = new StringBuilder();
            foreach (XElement t in si.Descendants(ns + "t"))
            {
                sb.Append(t.Value);
            }

            sharedStrings.Add(sb.ToString());
        }

        return sharedStrings;
    }

    /// <summary>
    /// Detects a plausible calendar year from a spreadsheet row without mistaking monetary amounts
    /// (e.g. "2072", "2,072.50", "$2072") for years.
    /// </summary>
    private static bool TryDetectYearFromRow(List<string> row, out int year)
    {
        year = 0;
        int currentYear = DateTime.UtcNow.Year;

        foreach (string cell in row)
        {
            if (string.IsNullOrWhiteSpace(cell))
            {
                continue;
            }

            string text = cell.Trim();
            bool isPlainYear = Regex.IsMatch(text, @"^20\d{2}$");
            bool isTextOrDate = !Regex.IsMatch(text, @"^[\p{Sc}\s\d,.\-+()%]+$");
            if (!isPlainYear && !isTextOrDate)
            {
                continue;
            }

            Match match = Regex.Match(text, @"(?<![\d.,$])(20\d{2})(?![\d,]|\.\d)");
            if (match.Success
                && int.TryParse(match.Groups[1].Value, out int parsed)
                && parsed >= currentYear - 15
                && parsed <= currentYear + 5)
            {
                year = parsed;
                return true;
            }
        }

        return false;
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
        string cleanName = string.IsNullOrWhiteSpace(fileName) ? "Monthly budget.xlsx" : fileName.Trim();
        Match yearMatch = Regex.Match(cleanName, @"\b(20\d{2})\b");
        int year = yearMatch.Success && int.TryParse(yearMatch.Value, out int parsedYear)
            ? parsedYear
            : DateTime.UtcNow.Year;
        int? fileNameMonth = DetectMonthFromText(cleanName);

        string title = fileNameMonth.HasValue
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
        foreach (List<string> row in rows)
        {
            string combinedRowText = string.Join(" ", row).Trim();
            int? detectedMonth = DetectMonthFromText(combinedRowText);
            if (detectedMonth.HasValue && !fileNameMonth.HasValue)
            {
                targetMonth = detectedMonth.Value;
            }

            // A year in the file name is authoritative; sheet content is only a fallback hint.
            if (!yearMatch.Success && TryDetectYearFromRow(row, out int yr))
            {
                report.Year = yr;
                report.Title = fileNameMonth.HasValue
                    ? $"Monthly Budget Summary ({MonthNames[fileNameMonth.Value - 1]} {yr})"
                    : $"Monthly Budget Summary ({yr})";
            }
        }

        // Second pass: Parse rows
        foreach (List<string> rawRow in rows)
        {
            var row = rawRow.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();
            if (row.Count == 0) continue;

            string firstCol = row[0];

            // Section switchers (Income vs Expenses) when there are no numbers
            List<decimal> allRowNumbers = ExtractDecimalsFromRow(row.Skip(1));

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
            int? monthFromRow = DetectMonthFromText(firstCol);
            if (monthFromRow.HasValue && allRowNumbers.Count == 0 && !fileNameMonth.HasValue)
            {
                targetMonth = monthFromRow.Value;
                continue;
            }

            // Check if transaction row e.g. [Date, Amount, Description, Category]
            if (row.Count >= 2 && (IsTransactionDate(firstCol) || (row.Count >= 3 && IsTransactionDate(row[1]))))
            {
                List<decimal> txNums = ExtractDecimalsFromRow(row);
                if (txNums.Count > 0)
                {
                    decimal amount = txNums[0];
                    var textCells = row.Where(c => !TryParseDecimal(c, out _) && !IsTransactionDate(c)).ToList();
                    if (textCells.Count > 0)
                    {
                        string catName = CleanCategoryName(textCells.Last());
                        if (!string.IsNullOrWhiteSpace(catName) && !IsIgnoredHeaderText(catName))
                        {
                            MonthlyBudgetCategoryItemDto? existingCat = monthlyExpenseCategories[targetMonth].FirstOrDefault(c =>
                                c.CategoryName.Equals(catName, StringComparison.OrdinalIgnoreCase));
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
            List<(string Name, List<decimal> Numbers)> segments = ExtractRowSegments(row);

            foreach ((string Name, List<decimal> Numbers) segment in segments)
            {
                string name = segment.Name;
                List<decimal> nums = segment.Numbers;

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
                if (name.Contains("Planned", StringComparison.OrdinalIgnoreCase) && IsIncomeCategory(name) &&
                    nums.Count >= 1)
                {
                    plannedIncomePart[targetMonth] = nums[0];
                    continue;
                }

                if (name.Contains("Actual", StringComparison.OrdinalIgnoreCase) && IsIncomeCategory(name) &&
                    nums.Count >= 1)
                {
                    actualIncomePart[targetMonth] = nums[0];
                    continue;
                }

                // 5. Planned Expense / Actual Expense standalone key-value rows
                if (name.Contains("Planned", StringComparison.OrdinalIgnoreCase) &&
                    (name.Contains("Expense", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("Spending", StringComparison.OrdinalIgnoreCase)) && nums.Count >= 1)
                {
                    plannedExpensePart[targetMonth] = nums[0];
                    continue;
                }

                if (name.Contains("Actual", StringComparison.OrdinalIgnoreCase) &&
                    (name.Contains("Expense", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("Spending", StringComparison.OrdinalIgnoreCase)) && nums.Count >= 1)
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
                int? segMonth = DetectMonthFromText(name);
                if (segMonth.HasValue && nums.Count >= 2)
                {
                    int mIdx = fileNameMonth ?? segMonth.Value;
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
                    string categoryName = CleanCategoryName(name);
                    decimal budgeted = nums[0];
                    decimal actual = nums.Count > 1 ? nums[1] : budgeted;

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
        bool hasAnyParsedData = explicitIncomeSummary.Values.Any(v => v.HasValue) ||
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
            return Unavailable(fileName, fileId, "No monthly budget table was found. The sheet needs income and expense rows with month columns, or a Summary sheet.");
        }

        var monthsList = new List<MonthlyBudgetMonthSummaryDto>();

        int startM = fileNameMonth ?? 1;
        int endM = fileNameMonth ?? 12;

        for (int m = startM; m <= endM; m++)
        {
            string mName = MonthNames[m - 1];

            // Reconcile Income (Planned & Actual)
            decimal bInc = 0m;
            decimal aInc = 0m;

            if (plannedIncomePart[m].HasValue) bInc = plannedIncomePart[m]!.Value;
            if (actualIncomePart[m].HasValue) aInc = actualIncomePart[m]!.Value;

            if (explicitIncomeSummary[m].HasValue)
            {
                (decimal Budget, decimal Actual) summary = explicitIncomeSummary[m]!.Value;
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

            List<MonthlyBudgetCategoryItemDto> cats = monthlyExpenseCategories[m];

            if (explicitExpenseSummary[m].HasValue)
            {
                (decimal Budget, decimal Actual) summary = explicitExpenseSummary[m]!.Value;
                if (bExp == 0) bExp = summary.Budget;
                if (aExp == 0) aExp = summary.Actual;
            }
            else if (cats.Count > 0)
            {
                if (bExp == 0) bExp = cats.Sum(c => c.BudgetedAmount);
                if (aExp == 0) aExp = cats.Sum(c => c.ActualAmount);
            }

            // Balances
            decimal? startBal = monthlyStartBalance[m];
            decimal? endBal = monthlyEndBalance[m];

            // If income or expense not set, but start and end balances exist
            if (aInc == 0 && aExp == 0 && startBal.HasValue && endBal.HasValue)
            {
                decimal diff = endBal.Value - startBal.Value;
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

            // Category rows without totals: the totals are the category sums.
            // (Totals without category rows keep an empty breakdown; categories are never estimated.)
            if (cats.Count > 0 && aExp == 0 && bExp == 0)
            {
                bExp = cats.Sum(c => c.BudgetedAmount);
                aExp = cats.Sum(c => c.ActualAmount);
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

        foreach (string cell in row)
        {
            if (string.IsNullOrWhiteSpace(cell)) continue;

            if (TryParseDecimal(cell, out decimal val))
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
        string clean = text.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
        return clean.Contains("startbalance") || clean.Contains("startingbalance") ||
               clean.Contains("initialbalance") || clean.Contains("beginningbalance") ||
               clean.Contains("openingbalance") || clean.Contains("startbal") || clean.Contains("startingbal");
    }

    private static bool IsEndBalanceText(string text)
    {
        string clean = text.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
        return clean.Contains("endbalance") || clean.Contains("endingbalance") ||
               clean.Contains("finalbalance") || clean.Contains("closingbalance") ||
               clean.Contains("endbal") || clean.Contains("endingbal");
    }

    private static bool IsNetSavingsText(string text)
    {
        string lower = text.ToLowerInvariant();
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
        string lower = text.ToLowerInvariant();
        return lower.Contains("total income") || lower.Contains("income total") ||
               lower.Contains("total revenue") || lower.Contains("total earnings") ||
               lower.Contains("total paycheck") || lower.Contains("incomes total") ||
               lower.Equals("income", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("incomes", StringComparison.OrdinalIgnoreCase) ||
               lower.Equals("income summary", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTotalExpenseText(string text)
    {
        string lower = text.ToLowerInvariant();
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
        string lower = text.ToLowerInvariant().Trim();
        return lower == "income" || lower == "incomes" || lower == "income:" || lower == "earnings" ||
               lower == "revenue";
    }

    private static bool IsExpenseSectionHeader(string text)
    {
        string lower = text.ToLowerInvariant().Trim();
        return lower == "expenses" || lower == "expense" || lower == "expense:" || lower == "expenses:" ||
               lower == "spending" || lower == "outflows";
    }

    private static bool IsIncomeCategory(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("income") || lower.Contains("salary") || lower.Contains("paycheck") ||
               lower.Contains("wage") || lower.Contains("bonus") || lower.Contains("dividend") ||
               lower.Contains("freelance") || lower.Contains("consulting") || lower.Contains("interest") ||
               lower.Contains("investment return") || lower.Contains("side hustle");
    }

    private static bool IsIgnoredHeaderText(string text)
    {
        string lower = text.ToLowerInvariant().Trim();
        return lower == "category" || lower == "item" || lower == "description" ||
               lower == "month" || lower == "date" || lower == "difference" || lower == "diff" ||
               lower == "planned" || lower == "actual" || lower == "budgeted" || lower == "variance" ||
               lower == "amount" || lower == "transactions" || lower == "transaction" ||
               lower == "payee" || lower == "merchant" || lower == "notes" ||
               lower.StartsWith("monthly budget", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSummarySheetName(string name)
    {
        string lower = name.ToLowerInvariant().Trim();
        return lower == "summary" || lower == "budget summary" || lower == "overview" ||
               lower == "monthly budget" || lower == "budget" || lower == "annual summary" ||
               lower.Contains("summary");
    }

    private static bool IsTransactionSheetName(string name)
    {
        string lower = name.ToLowerInvariant().Trim();
        return lower == "transaction" || lower == "transactions" || lower == "expenses" ||
               lower == "expense log" || lower == "transaction log" || lower.Contains("transaction");
    }

    private static List<(string SheetName, ZipArchiveEntry Entry)> GetWorksheetEntriesWithNames(ZipArchive archive)
    {
        var result = new List<(string SheetName, ZipArchiveEntry Entry)>();
        ZipArchiveEntry? workbookEntry = archive.GetEntry("xl/workbook.xml");
        ZipArchiveEntry? workbookRelsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

        if (workbookEntry != null && workbookRelsEntry != null)
        {
            try
            {
                using Stream wbStream = workbookEntry.Open();
                var wbDoc = XDocument.Load(wbStream);
                XNamespace wbNs = wbDoc.Root?.Name.Namespace ?? XNamespace.None;
                XNamespace rNs = wbDoc.Root?.GetNamespaceOfPrefix("r") ??
                          XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships");

                using Stream relsStream = workbookRelsEntry.Open();
                var relsDoc = XDocument.Load(relsStream);
                XNamespace relsNs = relsDoc.Root?.Name.Namespace ?? XNamespace.None;

                var relMap = relsDoc.Descendants(relsNs + "Relationship")
                    .Where(r => r.Attribute("Id") != null && r.Attribute("Target") != null)
                    .ToDictionary(r => r.Attribute("Id")!.Value, r => r.Attribute("Target")!.Value);

                foreach (XElement sheetElem in wbDoc.Descendants(wbNs + "sheet"))
                {
                    string sheetName = sheetElem.Attribute("name")?.Value ?? string.Empty;
                    string rId = sheetElem.Attribute(rNs + "id")?.Value ?? string.Empty;
                    if (relMap.TryGetValue(rId, out string? target))
                    {
                        string cleanTarget = target.TrimStart('/');
                        if (!cleanTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
                        {
                            cleanTarget = "xl/" + cleanTarget;
                        }

                        ZipArchiveEntry? wsEntry = archive.GetEntry(cleanTarget) ?? archive.Entries.FirstOrDefault(e =>
                            e.FullName.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase));
                        if (wsEntry != null)
                        {
                            result.Add((sheetName, wsEntry));
                        }
                    }
                }
            }
            catch (Exception ex) when (IsUnreadableWorkbook(ex))
            {
                // Malformed workbook.xml or relationships: fall back to the direct worksheet lookup below.
            }
        }

        if (result.Count == 0)
        {
            var worksheetEntries = archive.Entries
                .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                            e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName)
                .ToList();

            foreach (ZipArchiveEntry? wsEntry in worksheetEntries)
            {
                result.Add((Path.GetFileNameWithoutExtension(wsEntry.FullName), wsEntry));
            }
        }

        return result;
    }

    private static List<List<string>> ParseWorksheetRows(ZipArchiveEntry wsEntry, List<string> sharedStrings)
    {
        var rows = new List<List<string>>();
        using Stream wsStream = wsEntry.Open();
        var wsDoc = XDocument.Load(wsStream);
        XNamespace ns = wsDoc.Root?.Name.Namespace ?? XNamespace.None;

        foreach (XElement rowElem in wsDoc.Descendants(ns + "row"))
        {
            var rowCells = new List<(int Col, string Val)>();
            foreach (XElement cellElem in rowElem.Elements(ns + "c"))
            {
                string cellRef = cellElem.Attribute("r")?.Value ?? string.Empty;
                int colIdx = GetColumnIndexFromCellRef(cellRef);
                string? type = cellElem.Attribute("t")?.Value;

                string cellVal = string.Empty;
                if (type == "s")
                {
                    string? vVal = cellElem.Element(ns + "v")?.Value;
                    if (int.TryParse(vVal, out int sIdx) && sIdx >= 0 && sIdx < sharedStrings.Count)
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
                int maxCol = rowCells.Max(c => c.Col);
                var rowList = new List<string>(new string[maxCol + 1]);
                for (int i = 0; i <= maxCol; i++) rowList[i] = string.Empty;
                foreach ((int col, string? val) in rowCells)
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

        foreach (List<string> rawRow in txRows)
        {
            var row = rawRow.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();
            if (row.Count < 2) continue;
            if (IsIgnoredHeaderText(row[0])) continue;

            // Find decimal amounts in row
            List<decimal> nums = ExtractDecimalsFromRow(row);
            if (nums.Count == 0) continue;
            decimal amount = nums[0];

            // Find category text
            var textCells = row.Where(c => !TryParseDecimal(c, out _) && !IsTransactionDate(c)).ToList();
            if (textCells.Count == 0) continue;

            string catName = CleanCategoryName(textCells.Last());
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
        foreach (MonthlyBudgetMonthSummaryDto month in report.Months)
        {
            foreach (MonthlyBudgetCategoryItemDto cat in month.Categories)
            {
                if (cat.ActualAmount == 0 && categoryTotals.TryGetValue(cat.CategoryName, out decimal actualTxAmount))
                {
                    cat.ActualAmount = actualTxAmount;
                }
            }
        }
    }

    private static bool IsTransactionDate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string trimmed = text.Trim();

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
        if (Regex.IsMatch(trimmed,
                @"^(?:(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*[- ]\d{1,2}(?:[- ,]+\d{2,4})?|\d{1,2}[- ](?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*(?:[- ,]+\d{2,4})?)$",
                RegexOptions.IgnoreCase))
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
    /// Assigns consistent and attractive chart colors according to category keywords.
    /// </summary>
    public static string AssignCategoryColor(string categoryName)
    {
        string lower = categoryName.ToLowerInvariant();
        if (lower.Contains("rent") || lower.Contains("house") || lower.Contains("mortgage") || lower.Contains("home"))
            return "#3b82f6";
        if (lower.Contains("groc") || lower.Contains("food") || lower.Contains("market")) return "#10b981";
        if (lower.Contains("util") || lower.Contains("power") || lower.Contains("internet") ||
            lower.Contains("phone") || lower.Contains("electric")) return "#f59e0b";
        if (lower.Contains("trans") || lower.Contains("gas") || lower.Contains("car") || lower.Contains("auto") ||
            lower.Contains("fuel")) return "#8b5cf6";
        if (lower.Contains("health") || lower.Contains("med") || lower.Contains("insur") || lower.Contains("dental"))
            return "#ec4899";
        if (lower.Contains("din") || lower.Contains("restaur") || lower.Contains("entert") || lower.Contains("fun") ||
            lower.Contains("hobby")) return "#06b6d4";
        if (lower.Contains("save") || lower.Contains("invest") || lower.Contains("retire") || lower.Contains("401k") ||
            lower.Contains("stock")) return "#6366f1";
        if (lower.Contains("debt") || lower.Contains("loan") || lower.Contains("credit")) return "#ef4444";
        if (lower.Contains("pers") || lower.Contains("cloth") || lower.Contains("misc")) return "#64748b";

        // Deterministic hash color fallback
        int hash = Math.Abs(categoryName.GetHashCode());
        string[] palette = new[]
        {
            "#3b82f6", "#10b981", "#f59e0b", "#8b5cf6", "#ec4899", "#06b6d4", "#6366f1", "#14b8a6", "#f97316", "#84cc16"
        };
        return palette[hash % palette.Length];
    }

    public static int? DetectMonthFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        for (int i = 0; i < 12; i++)
        {
            string mName = MonthNames[i];
            string shortName = ShortMonthNames[i];
            string pattern = i == 8
                ? $@"\b(?:{mName}|Sept|{shortName})\b"
                : $@"\b(?:{mName}|{shortName})\b";

            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
            {
                return i + 1;
            }
        }

        Match match = Regex.Match(text, @"\b(?:20\d{2}[-_/.](0[1-9]|1[0-2])|(0[1-9]|1[0-2])[-_/.](20\d{2}))\b");
        if (match.Success)
        {
            string mStr = !string.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[1].Value : match.Groups[2].Value;
            if (int.TryParse(mStr, out int mVal) && mVal >= 1 && mVal <= 12)
            {
                return mVal;
            }
        }

        return null;
    }

    private static List<decimal> ExtractDecimalsFromRow(IEnumerable<string> items)
    {
        var decimals = new List<decimal>();
        foreach (string item in items)
        {
            if (TryParseDecimal(item, out decimal val))
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

        string clean = text.Trim()
            .Replace("$", "")
            .Replace("€", "")
            .Replace("£", "")
            .Replace("%", "")
            .Trim();

        // Handle negative formatted in parentheses e.g. (150.00) -> -150.00
        if (clean.StartsWith('(') && clean.EndsWith(')'))
        {
            clean = string.Concat("-", clean.AsSpan(1, clean.Length - 2));
        }

        return decimal.TryParse(clean, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                   CultureInfo.InvariantCulture, out value) ||
               decimal.TryParse(clean, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }

    private static string CleanCategoryName(string raw)
    {
        string clean = raw.Trim().Trim('"', '\'', '-', ':');
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

    /// <summary>
    /// True for errors that mean "this file is not a readable XLSX workbook". Anything else is a bug and propagates.
    /// </summary>
    private static bool IsUnreadableWorkbook(Exception ex)
    {
        return ex is InvalidDataException or System.Xml.XmlException;
    }

    private static int GetColumnIndexFromCellRef(string cellRef)
    {
        Match match = Regex.Match(cellRef, @"^[A-Z]+");
        if (!match.Success) return 0;

        string colLetters = match.Value;
        int col = 0;
        foreach (char c in colLetters)
        {
            col = col * 26 + (c - 'A' + 1);
        }

        return col - 1; // 0-based
    }

    private sealed class TransactionTableLayout
    {
        public int DateCol { get; init; }
        public int AmountCol { get; init; }
        public int DescriptionCol { get; init; } = -1;
        public int CategoryCol { get; init; } = -1;
        public int TypeCol { get; init; } = -1;
        public string? SectionType { get; init; }
        public List<SpreadsheetTransactionDto> Transactions { get; } = new();
    }
}
