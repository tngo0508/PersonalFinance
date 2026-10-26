using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.Analysis;

/// <summary>
///     The confirmation page of a Data Source Removal. Only <see cref="Source"/> and <see cref="Confirmation"/> are
///     posted back; the counts are read from ApiService again on every request.
/// </summary>
public class RemoveSourceViewModel
{
    /// <summary>The word a user types to confirm removing the Budget Tracker, whose entries exist nowhere else.</summary>
    public const string ConfirmationWord = "REMOVE";

    public BudgetDataSource Source { get; set; }

    /// <summary>What the user typed to confirm; checked only for the Budget Tracker.</summary>
    public string? Confirmation { get; set; }

    public int MonthCount { get; set; }
    public int TransactionCount { get; set; }

    public bool RequiresTypedConfirmation => Source == BudgetDataSource.BudgetTracker;
}
