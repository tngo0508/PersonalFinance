using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.InvestmentScenarios;
using PersonalFinance.Web.Infrastructure;

namespace PersonalFinance.Web.Controllers;

/// <summary>
/// JSON endpoints behind the Investment Calculator's Saved panel (#592), forwarded to ApiService through
/// <see cref="IInvestmentScenariosApi"/>. Writes require the antiforgery token (sent by <c>investment-scenarios.js</c>);
/// a failure answers <c>{ "error": "..." }</c> in the reader's language (<see cref="JsonApiErrorsAttribute"/>).
/// </summary>
[Authorize]
[JsonApiErrors]
[Route("api/[controller]")]
[Produces("application/json")]
public class InvestmentScenariosController : Controller
{
    private readonly IInvestmentScenariosApi _scenariosApi;

    public InvestmentScenariosController(IInvestmentScenariosApi scenariosApi)
    {
        _scenariosApi = scenariosApi;
    }

    /// <summary>The signed-in Account's scenarios, last edited first.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default) =>
        Ok(await _scenariosApi.GetScenariosAsync(cancellationToken));

    /// <summary>Saves the calculator's inputs as a new scenario.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] SaveInvestmentScenarioRequestDto request,
        CancellationToken cancellationToken = default) =>
        Ok(await _scenariosApi.CreateScenarioAsync(request, cancellationToken));

    /// <summary>Replaces a scenario's name, note and inputs (Update, and Rename).</summary>
    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromBody] SaveInvestmentScenarioRequestDto request,
        CancellationToken cancellationToken = default) =>
        Ok(await _scenariosApi.UpdateScenarioAsync(id, request, cancellationToken));

    /// <summary>Copies a scenario under a new name.</summary>
    [HttpPost("{id:int}/duplicate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id, [FromBody] DuplicateInvestmentScenarioRequestDto request,
        CancellationToken cancellationToken = default) =>
        Ok(await _scenariosApi.DuplicateScenarioAsync(id, request, cancellationToken));

    /// <summary>Deletes a scenario.</summary>
    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        await _scenariosApi.DeleteScenarioAsync(id, cancellationToken);
        return Ok(new { success = true });
    }
}
