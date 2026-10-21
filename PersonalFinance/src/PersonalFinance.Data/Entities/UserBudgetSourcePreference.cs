using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Data.Entities;

/// <summary>
/// Database entity storing the budget data source a user explicitly selected for Analysis and the Dashboard.
/// No row means the default source rule applies.
/// </summary>
public class UserBudgetSourcePreference : AuditableEntity
{
    /// <summary>The data source the user chose.</summary>
    public BudgetDataSource Source { get; set; }
}
