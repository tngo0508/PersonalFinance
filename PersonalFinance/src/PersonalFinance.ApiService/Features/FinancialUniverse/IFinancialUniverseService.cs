using PersonalFinance.Shared.DTOs.FinancialUniverse;

namespace PersonalFinance.ApiService.Features.FinancialUniverse;

/// <summary>
/// Projects an Account's existing data into the Financial Universe graph (GLOSSARY.md, spec #617). Read-only: the
/// graph is derived on every call from the budget source, the Savings Goals, the Net Worth parameters and the
/// Investment Scenarios, through those Features' interfaces.
/// </summary>
public interface IFinancialUniverseService
{
    /// <summary>The graph for <paramref name="year"/> and <paramref name="month"/>, both already validated.</summary>
    Task<FinancialUniverseGraphDto> GetGraphAsync(string userId, int year, int month,
        CancellationToken cancellationToken = default);
}
