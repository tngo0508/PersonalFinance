using PersonalFinance.Shared.DTOs.MoneyPriorities;

namespace PersonalFinance.ApiService.Features.MoneyPriorities;

/// <summary>
/// Money Priorities: walks the Priority Tree from a user's answers, saves the answers as a Priority Walk and shows a
/// Walk with its figures recomputed. Every method is scoped to <c>userId</c>; another Account's Walk is never visible.
/// </summary>
public interface IMoneyPrioritiesService
{
    /// <summary>
    /// Evaluates a validated request and saves it as a Priority Walk with the answers off its path stored as null. When
    /// the on-path answers, the Purpose and the limits year match the Account's latest Walk exactly, nothing is saved
    /// and that Walk is returned instead.
    /// </summary>
    Task<PriorityWalkResultDto> SubmitAsync(string userId, SubmitPriorityWalkRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>The Account's 10 newest Priority Walks, newest first, for Earlier results.</summary>
    Task<List<PriorityWalkSummaryDto>> ListRecentAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>One of the Account's Priority Walks with its figures recomputed, or null when it has none with that id.</summary>
    Task<PriorityWalkResultDto?> GetAsync(string userId, int id, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes one of the Account's Priority Walks; false when it has none with that id.</summary>
    Task<bool> DeleteAsync(string userId, int id, CancellationToken cancellationToken = default);

    /// <summary>The whole Priority Tree with nothing highlighted, shown before a submit.</summary>
    PriorityTreeDiagramDto GetTree();

    /// <summary>
    /// The Purpose Note of one of the Account's Priority Walks, or null when it has none with that id. A Walk without
    /// a Purpose gets no model call. A note already saved on the Walk, or on another of the Account's Walks with the
    /// same Purpose, answers, tree version and limits year, is reused (and copied onto this Walk) without a model call.
    /// Otherwise the AI Assistant writes it and it is saved onto the Walk. The Walk is kept whatever happens.
    /// </summary>
    Task<PriorityWalkPurposeNoteDto?> WritePurposeNoteAsync(string userId, int id,
        CancellationToken cancellationToken = default);
}
