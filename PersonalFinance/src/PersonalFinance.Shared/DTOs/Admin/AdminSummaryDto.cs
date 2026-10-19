namespace PersonalFinance.Shared.DTOs.Admin;

/// <summary>
/// System-level account counts shown on the Admin Dashboard. Contains no per-user financial data.
/// </summary>
public class AdminSummaryDto
{
    public int TotalAccounts { get; set; }

    public int ConfirmedAccounts { get; set; }

    public int UnconfirmedAccounts { get; set; }

    /// <summary>
    /// Number of accounts per role name.
    /// </summary>
    public Dictionary<string, int> AccountsPerRole { get; set; } = new();
}
