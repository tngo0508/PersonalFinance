namespace PersonalFinance.ApiService.Features.Admin;

/// <summary>
/// Result of an admin role-management operation; <see cref="Error"/> is set when it was not successful.
/// </summary>
public record AdminOperationResult(AdminOperationStatus Status, string? Error = null)
{
    public bool Succeeded => Status == AdminOperationStatus.Success;

    public static AdminOperationResult Ok() => new(AdminOperationStatus.Success);

    public static AdminOperationResult NotFound(string error) => new(AdminOperationStatus.NotFound, error);

    public static AdminOperationResult Rejected(string error) => new(AdminOperationStatus.Rejected, error);
}
