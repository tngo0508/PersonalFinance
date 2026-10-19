namespace PersonalFinance.Shared.DTOs.Admin;

/// <summary>
/// An account and the roles it holds, shown in the Admin user list. Contains no financial data.
/// </summary>
public class AdminUserDto
{
    public string Id { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool EmailConfirmed { get; set; }

    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// A role and how many accounts hold it.
/// </summary>
public class AdminRoleDto
{
    public string Name { get; set; } = string.Empty;

    public int UserCount { get; set; }

    /// <summary>
    /// True for roles defined in code (cannot be deleted).
    /// </summary>
    public bool IsBuiltIn { get; set; }
}

/// <summary>
/// Request body for creating a role.
/// </summary>
public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Error payload returned by admin endpoints when an operation is refused.
/// </summary>
public class AdminErrorDto
{
    public string Error { get; set; } = string.Empty;
}
