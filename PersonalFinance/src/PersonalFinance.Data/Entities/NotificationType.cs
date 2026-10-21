namespace PersonalFinance.Data.Entities;

/// <summary>
/// Enum representing the type of notification.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Budget-related alert (e.g., spending exceeds 80% or 100% of allocation).
    /// </summary>
    BudgetAlert,

    /// <summary>
    /// Savings goal milestone (e.g., 50%, 75%, or 100% of target reached).
    /// </summary>
    SavingsMilestone,

    /// <summary>
    /// Google Drive sync status (e.g., sync succeeded or failed).
    /// </summary>
    SyncStatus
}
