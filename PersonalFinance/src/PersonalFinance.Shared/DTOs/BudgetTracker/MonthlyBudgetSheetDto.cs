using PersonalFinance.Shared.DTOs.Currency;

namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// Complete sheet view for a month in the Budget Tracker, reproducing the Google monthly budget sheet layout. Every
/// row amount and total is in <see cref="ReportingCurrency"/> (ADR 0016); each transaction keeps its own currency.
/// </summary>
public class MonthlyBudgetSheetDto
{
    /// <summary>The ISO 4217 code the rows and totals are in: the Account's Reporting Currency.</summary>
    public string ReportingCurrency { get; set; } = string.Empty;

    /// <summary>The rates that brought lines and transactions in other currencies into the Reporting Currency.</summary>
    public ConversionDto? Conversion { get; set; }

    /// <summary>Calendar year.</summary>
    public int Year { get; set; }

    /// <summary>Calendar month (1-12).</summary>
    public int Month { get; set; }

    /// <summary>Formatted month name, e.g., "October 2026".</summary>
    public string MonthName { get; set; } = string.Empty;

    /// <summary>Whether a budget plan has been initialized for this month.</summary>
    public bool IsInitialized { get; set; }

    /// <summary>Whether an earlier month exists to copy planned amounts from.</summary>
    public bool CanCopyFromPrevious { get; set; }

    /// <summary>Year of the most recent earlier month with a plan, so the page can name it in the user's Language.</summary>
    public int? PreviousPlanYear { get; set; }

    /// <summary>Month (1-12) of the most recent earlier month with a plan.</summary>
    public int? PreviousPlanMonth { get; set; }

    /// <summary>Expenses section with rows and totals.</summary>
    public SheetSectionDto Expenses { get; set; } = new();

    /// <summary>Income section with rows and totals.</summary>
    public SheetSectionDto Income { get; set; } = new();

    /// <summary>Itemized transactions dated within this month.</summary>
    public List<BudgetTransactionDto> Transactions { get; set; } = new();
}
