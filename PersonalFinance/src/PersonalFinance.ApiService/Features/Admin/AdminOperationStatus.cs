namespace PersonalFinance.ApiService.Features.Admin;

/// <summary>
/// Outcome of an admin role-management operation.
/// </summary>
public enum AdminOperationStatus
{
    Success,
    NotFound,
    Rejected
}
