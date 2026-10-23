using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.ApiService.Services;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Admin;

namespace PersonalFinance.ApiService.Controllers;

/// <summary>
/// Admin-only API endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AppPolicies.RequireAdmin)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>
    /// Gets system-level account counts.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken = default)
    {
        return Ok(await _adminService.GetSummaryAsync(cancellationToken));
    }

    /// <summary>
    /// Lists all accounts with their roles.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken = default)
    {
        return Ok(await _adminService.GetUsersAsync(cancellationToken));
    }

    /// <summary>
    /// Lists all roles with member counts.
    /// </summary>
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken = default)
    {
        return Ok(await _adminService.GetRolesAsync(cancellationToken));
    }

    /// <summary>
    /// Creates a role.
    /// </summary>
    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        return ToActionResult(await _adminService.CreateRoleAsync(request.Name));
    }

    /// <summary>
    /// Deletes a non-built-in role without members.
    /// </summary>
    [HttpDelete("roles/{roleName}")]
    public async Task<IActionResult> DeleteRole(string roleName)
    {
        return ToActionResult(await _adminService.DeleteRoleAsync(roleName));
    }

    /// <summary>
    /// Assigns a role to an account.
    /// </summary>
    [HttpPut("users/{userId}/roles/{roleName}")]
    public async Task<IActionResult> AssignRole(string userId, string roleName)
    {
        return ToActionResult(await _adminService.AssignRoleAsync(userId, roleName));
    }

    /// <summary>
    /// Removes a role from an account.
    /// </summary>
    [HttpDelete("users/{userId}/roles/{roleName}")]
    public async Task<IActionResult> RemoveRole(string userId, string roleName)
    {
        string actingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        return ToActionResult(await _adminService.RemoveRoleAsync(actingUserId, userId, roleName));
    }

    private IActionResult ToActionResult(AdminOperationResult result)
    {
        return result.Status switch
        {
            AdminOperationStatus.Success => NoContent(),
            AdminOperationStatus.NotFound => NotFound(new AdminErrorDto { Error = result.Error ?? "Not found." }),
            _ => BadRequest(new AdminErrorDto { Error = result.Error ?? "The operation was rejected." })
        };
    }
}
