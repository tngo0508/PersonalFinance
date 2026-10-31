using PersonalFinance.Shared.DTOs.InvestmentScenarios;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for the Account's Investment Scenarios (#592). Every call is scoped to the signed-in Account.
/// </summary>
public interface IInvestmentScenariosApi
{
    /// <summary>The Account's scenarios, last edited first.</summary>
    [Get("/api/InvestmentScenarios")]
    Task<List<InvestmentScenarioDto>> GetScenariosAsync(CancellationToken cancellationToken = default);

    /// <summary>One scenario.</summary>
    [Get("/api/InvestmentScenarios/{id}")]
    Task<InvestmentScenarioDto> GetScenarioAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Saves a new scenario; a 409 when the Account already keeps the most allowed.</summary>
    [Post("/api/InvestmentScenarios")]
    Task<InvestmentScenarioDto> CreateScenarioAsync([Body] SaveInvestmentScenarioRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces a scenario's name, note and inputs.</summary>
    [Put("/api/InvestmentScenarios/{id}")]
    Task<InvestmentScenarioDto> UpdateScenarioAsync(int id, [Body] SaveInvestmentScenarioRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Copies a scenario under a new name; a 409 when the Account already keeps the most allowed.</summary>
    [Post("/api/InvestmentScenarios/{id}/duplicate")]
    Task<InvestmentScenarioDto> DuplicateScenarioAsync(int id, [Body] DuplicateInvestmentScenarioRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a scenario (soft delete).</summary>
    [Delete("/api/InvestmentScenarios/{id}")]
    Task DeleteScenarioAsync(int id, CancellationToken cancellationToken = default);
}
