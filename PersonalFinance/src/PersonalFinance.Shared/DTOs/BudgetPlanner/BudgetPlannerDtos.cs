namespace PersonalFinance.Shared.DTOs.BudgetPlanner;

public class BudgetResponseDto
{
    public BudgetAllocationsDto? Allocations { get; set; }
    public decimal MonthlyIncome { get; set; }
}

public class SaveBudgetRequestDto
{
    public string? Period { get; set; }

    /// <summary>
    /// The ISO 4217 currency of the income and allocations, one of <c>SupportedCurrencies</c>; null keeps the saved
    /// allocation's currency (US dollars for a new one).
    /// </summary>
    public string? CurrencyCode { get; set; }

    public decimal MonthlyIncome { get; set; }
    public BudgetAllocationsDto Allocations { get; set; } = new();
}

/// <summary>
/// Dollar amounts allocated to each budget category.
/// Each value represents the exact amount in dollars and cents, not a percentage.
/// </summary>
public class BudgetAllocationsDto
{
    /// <summary>Dollar amount allocated to Housing.</summary>
    public decimal Housing { get; set; }
    /// <summary>Dollar amount allocated to Food.</summary>
    public decimal Food { get; set; }
    /// <summary>Dollar amount allocated to Transportation.</summary>
    public decimal Transportation { get; set; }
    /// <summary>Dollar amount allocated to Utilities.</summary>
    public decimal Utilities { get; set; }
    /// <summary>Dollar amount allocated to Insurance.</summary>
    public decimal Insurance { get; set; }
    /// <summary>Dollar amount allocated to Healthcare.</summary>
    public decimal Healthcare { get; set; }
    /// <summary>Dollar amount allocated to Savings.</summary>
    public decimal Savings { get; set; }
    /// <summary>Dollar amount allocated to Debt Repayment.</summary>
    public decimal DebtRepayment { get; set; }
    /// <summary>Dollar amount allocated to Entertainment.</summary>
    public decimal Entertainment { get; set; }
    /// <summary>Dollar amount allocated to Miscellaneous.</summary>
    public decimal Miscellaneous { get; set; }
}

public class SaveBudgetResponseDto
{
    public bool Success { get; set; }
}
