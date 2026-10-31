using PersonalFinance.Shared.DTOs.InvestmentScenarios;

namespace PersonalFinance.ApiService.Features.InvestmentScenarios;

/// <summary>
/// An Account's Investment Scenarios (GLOSSARY.md): saved, named sets of Investment Calculator inputs. Every method is
/// scoped to <c>userId</c>: another Account's scenario reads as missing.
/// </summary>
public interface IInvestmentScenarioService
{
    /// <summary>The Account's scenarios, last edited first.</summary>
    Task<List<InvestmentScenarioDto>> ListAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>One scenario, or null when the Account has none with this id.</summary>
    Task<InvestmentScenarioDto?> GetAsync(string userId, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new scenario. Throws a keyed 409 rejection when the Account already keeps
    /// <c>InvestmentCalculatorLimits.MaxScenariosPerUser</c>.
    /// </summary>
    Task<InvestmentScenarioDto> CreateAsync(string userId, SaveInvestmentScenarioRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces a scenario's name, note and inputs; null when the Account has none with this id.</summary>
    Task<InvestmentScenarioDto?> UpdateAsync(string userId, int id, SaveInvestmentScenarioRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a scenario under <paramref name="name"/>; null when the Account has none with this id. The limit
    /// applies as for <see cref="CreateAsync"/>.
    /// </summary>
    Task<InvestmentScenarioDto?> DuplicateAsync(string userId, int id, string name,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a scenario; false when the Account has none with this id.</summary>
    Task<bool> DeleteAsync(string userId, int id, CancellationToken cancellationToken = default);
}
