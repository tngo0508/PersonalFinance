using PersonalFinance.Shared.DTOs.MoneyPriorities;
using Refit;

namespace PersonalFinance.Shared.Contracts;

/// <summary>
/// Refit API contract for Money Priorities. Every call is scoped to the signed-in Account. A rejected submit fails
/// with 400 and an <see cref="ApiErrorDto"/> body.
/// </summary>
public interface IMoneyPrioritiesApi
{
    /// <summary>Walks the Priority Tree from the answers and saves a Priority Walk (unless identical to the latest).</summary>
    [Post("/api/MoneyPriorities/walks")]
    Task<PriorityWalkResultDto> SubmitWalkAsync([Body] SubmitPriorityWalkRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>The Account's 10 newest Priority Walks, newest first.</summary>
    [Get("/api/MoneyPriorities/walks")]
    Task<List<PriorityWalkSummaryDto>> GetRecentWalksAsync(CancellationToken cancellationToken = default);

    /// <summary>One Priority Walk with its figures recomputed; 404 when the Account has none with that id.</summary>
    [Get("/api/MoneyPriorities/walks/{id}")]
    Task<PriorityWalkResultDto> GetWalkAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes one Priority Walk (204); 404 when the Account has none with that id.</summary>
    [Delete("/api/MoneyPriorities/walks/{id}")]
    Task DeleteWalkAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes (or reuses) a Priority Walk's Purpose Note and saves it onto the Walk; the state says when there is none
    /// (no Purpose, not available, quota spent, refused). 404 when the Account has no Walk with that id.
    /// </summary>
    [Post("/api/MoneyPriorities/walks/{id}/note")]
    Task<PriorityWalkPurposeNoteDto> WritePurposeNoteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The whole Priority Tree with nothing highlighted.</summary>
    [Get("/api/MoneyPriorities/tree")]
    Task<PriorityTreeDiagramDto> GetTreeAsync(CancellationToken cancellationToken = default);
}
