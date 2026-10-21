namespace PersonalFinance.Shared.DTOs.BudgetTracker;

/// <summary>
/// Request to initialize a budget plan for a specified month.
/// </summary>
public class InitializeMonthRequest
{
    /// <summary>Calendar year.</summary>
    public int Year { get; set; }

    /// <summary>Calendar month (1-12).</summary>
    public int Month { get; set; }

    /// <summary>If true, copy planned amounts from the nearest earlier month with a plan.</summary>
    public bool CopyFromPrevious { get; set; }
}
