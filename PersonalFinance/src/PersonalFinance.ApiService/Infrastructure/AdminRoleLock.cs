namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// Serializes every "count the Admins, then act" check (removing the Admin role, making a Deletion Request) so
/// concurrent requests cannot leave zero Admins. In-process only, which is acceptable at current scale (ADR 0004).
/// </summary>
internal static class AdminRoleLock
{
    public static readonly SemaphoreSlim Instance = new(1, 1);
}
