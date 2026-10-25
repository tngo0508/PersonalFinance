using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.AiAssistant;
using PersonalFinance.ApiService.Features.MoneyPriorities.Domain;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.AiAssistant;
using PersonalFinance.Shared.DTOs.MoneyPriorities;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.MoneyPriorities;

/// <summary>
/// <see cref="IMoneyPrioritiesService"/> over EF Core. The path is computed only by <see cref="PriorityPathEvaluator"/>
/// and the figures only by <see cref="PriorityLeafCalculator"/>; dollar figures are never stored, so every result is
/// rebuilt from a Walk's stored answers.
/// </summary>
internal sealed class MoneyPrioritiesService : IMoneyPrioritiesService
{
    private readonly IAiAssistantService _aiAssistant;
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public MoneyPrioritiesService(AppDbContext db, TimeProvider timeProvider, IAiAssistantService aiAssistant)
    {
        _db = db;
        _timeProvider = timeProvider;
        _aiAssistant = aiAssistant;
    }

    public async Task<PriorityWalkResultDto> SubmitAsync(string userId, SubmitPriorityWalkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        PriorityPath path = PriorityPathEvaluator.Evaluate(PriorityWalkMapping.ToAnswers(request));
        PriorityAnswers answers = path.Answers;
        int? limitsTaxYear = path.NodeIds.Contains(PriorityNodeIds.ContributionLimit)
            ? RetirementContributionLimits.ForYear(CurrentTaxYear()).TaxYear
            : null;

        var candidate = new PriorityWalk
        {
            UserId = userId,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            MonthlyAmount = answers.MonthlyAmount,
            Purpose = string.IsNullOrWhiteSpace(request.Purpose) ? null : request.Purpose.Trim(),
            DebtApr = answers.DebtApr,
            DebtBalance = answers.DebtBalance,
            EmergencyFundMonths = answers.EmergencyFundMonths,
            MonthlyExpenses = answers.MonthlyExpenses,
            HasFullEmployerMatch = answers.HasFullEmployerMatch,
            EmployerMatchPercent = answers.EmployerMatchPercent,
            HorizonYears = answers.HorizonYears,
            AgeBand = answers.AgeBand?.ToString(),
            AtContributionLimit = answers.AtContributionLimit,
            TreeVersion = path.TreeVersion,
            LimitsTaxYear = limitsTaxYear,
            Path = path.StoredPath,
            LeafId = path.LeafId
        };

        PriorityWalk? latest = await LatestWalkQuery(userId).FirstOrDefaultAsync(cancellationToken);
        if (latest is not null && PriorityWalkMapping.SameAnswers(latest, candidate))
        {
            return BuildResult(latest);
        }

        _db.PriorityWalks.Add(candidate);
        await _db.SaveChangesAsync(cancellationToken);
        return BuildResult(candidate);
    }

    public async Task<List<PriorityWalkSummaryDto>> ListRecentAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await RecentWalksQuery(userId)
            .Select(w => new { w.Id, w.CreatedAt, w.MonthlyAmount, w.LeafId, w.TreeVersion })
            .ToListAsync(cancellationToken);

        return rows
            .Select(w => new PriorityWalkSummaryDto
            {
                WalkId = w.Id,
                CreatedAtUtc = w.CreatedAt,
                MonthlyAmount = w.MonthlyAmount,
                LeafId = w.LeafId,
                LeafLabel = PriorityWalkMapping.TreeFor(w.TreeVersion).Node(w.LeafId).Label
            })
            .ToList();
    }

    public async Task<PriorityWalkResultDto?> GetAsync(string userId, int id,
        CancellationToken cancellationToken = default)
    {
        PriorityWalk? walk = await _db.PriorityWalks
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, cancellationToken);
        return walk is null ? null : BuildResult(walk);
    }

    public async Task<bool> DeleteAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        PriorityWalk? walk = await _db.PriorityWalks
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, cancellationToken);
        if (walk is null)
        {
            return false;
        }

        walk.SoftDelete();
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public PriorityTreeDiagramDto GetTree()
    {
        PriorityTree tree = PriorityTreeV1.Tree;
        return new PriorityTreeDiagramDto
        {
            TreeVersion = tree.Version,
            WholeTreeDiagram = PriorityDiagramText.WholeTree(tree, PriorityDiagramPalette.Light)
        };
    }

    public async Task<PriorityWalkPurposeNoteDto?> WritePurposeNoteAsync(string userId, int id,
        CancellationToken cancellationToken = default)
    {
        PriorityWalk? walk = await _db.PriorityWalks
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, cancellationToken);
        if (walk is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(walk.Purpose))
        {
            return PurposeNote(walk, PurposeNoteState.NoPurpose, note: null, MoneyPrioritiesMessages.PurposeNoteInvite);
        }

        if (walk.PurposeNote is not null)
        {
            return PurposeNote(walk, PurposeNoteState.Written, walk.PurposeNote, message: null);
        }

        List<PriorityWalk> candidates = await NoteCandidatesQuery(userId, walk).ToListAsync(cancellationToken);
        string? reused = candidates.FirstOrDefault(c => PriorityWalkMapping.SameAnswers(c, walk))?.PurposeNote;
        if (reused is not null)
        {
            await SaveNoteAsync(walk, reused, cancellationToken);
            return PurposeNote(walk, PurposeNoteState.Written, reused, message: null);
        }

        WalkView view = View(walk);
        PurposeNoteResponseDto written = await _aiAssistant.WritePurposeNoteAsync(userId,
            PurposeNoteFacts.For(walk.Purpose, view.Path.Answers, view.PathSentences, view.LeafLabel, view.Figures),
            cancellationToken);
        if (written.State == PurposeNoteState.Written && !string.IsNullOrWhiteSpace(written.Note))
        {
            await SaveNoteAsync(walk, written.Note, cancellationToken);
            return PurposeNote(walk, PurposeNoteState.Written, written.Note, message: null);
        }

        return written.State == PurposeNoteState.NoPurpose
            ? PurposeNote(walk, PurposeNoteState.NoPurpose, note: null, MoneyPrioritiesMessages.PurposeNoteInvite)
            : PurposeNote(walk, written.State, note: null, written.Message);
    }

    /// <summary>
    /// The Account's other Walks with a saved Purpose Note that could share this Walk's: same Purpose, tree version,
    /// leaf and amount, newest first. The rest of the answers are compared in memory with
    /// <see cref="PriorityWalkMapping.SameAnswers"/>.
    /// </summary>
    internal IQueryable<PriorityWalk> NoteCandidatesQuery(string userId, PriorityWalk walk) =>
        _db.PriorityWalks
            .AsNoTracking()
            .Where(w => w.UserId == userId
                        && w.Id != walk.Id
                        && w.PurposeNote != null
                        && w.Purpose == walk.Purpose
                        && w.TreeVersion == walk.TreeVersion
                        && w.LeafId == walk.LeafId
                        && w.MonthlyAmount == walk.MonthlyAmount)
            .OrderByDescending(w => w.CreatedAt)
            .ThenByDescending(w => w.Id);

    private async Task SaveNoteAsync(PriorityWalk walk, string note, CancellationToken cancellationToken)
    {
        walk.PurposeNote = note;
        walk.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static PriorityWalkPurposeNoteDto PurposeNote(PriorityWalk walk, PurposeNoteState state, string? note,
        string? message) => new()
    {
        WalkId = walk.Id,
        State = state,
        Note = note,
        Message = message,
        Disclaimer = AiAssistantMessages.Disclaimer
    };

    /// <summary>The Account's Priority Walks for Earlier results: the 10 newest, newest first, <c>Id</c> breaking ties.</summary>
    internal IQueryable<PriorityWalk> RecentWalksQuery(string userId) =>
        NewestFirst(userId).Take(MoneyPrioritiesLimits.RecentWalkCount);

    /// <summary>The Account's latest Priority Walk, which a new submit is compared against.</summary>
    internal IQueryable<PriorityWalk> LatestWalkQuery(string userId) => NewestFirst(userId).Take(1);

    private IQueryable<PriorityWalk> NewestFirst(string userId) =>
        _db.PriorityWalks
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ThenByDescending(w => w.Id);

    private int CurrentTaxYear() => _timeProvider.GetUtcNow().Year;

    /// <summary>
    /// The result card for a stored Walk: the path and answers as stored, walked against the Walk's own tree version,
    /// with the figures recomputed using the limits of its stored tax year (the current year when none is stored).
    /// </summary>
    private PriorityWalkResultDto BuildResult(PriorityWalk walk)
    {
        (PriorityTree tree, PriorityPath path, RetirementContributionLimits limits, PriorityLeafFigures figures,
            string leafLabel, IReadOnlyList<string> pathSentences) = View(walk);
        PriorityAnswers answers = path.Answers;

        return new PriorityWalkResultDto
        {
            WalkId = walk.Id,
            CreatedAtUtc = walk.CreatedAt,
            TreeVersion = walk.TreeVersion,
            LeafId = path.LeafId,
            LeafLabel = leafLabel,
            HeadlineLeafLabel = $"{leafLabel}.",
            MonthlyAmount = walk.MonthlyAmount,
            Purpose = walk.Purpose,
            Answers = PriorityWalkMapping.ToAnswersDto(walk.Purpose, answers),
            Figures = PriorityWalkMapping.ToDto(figures, limits),
            PathSentences = pathSentences.ToList(),
            WholeTreeDiagram = PriorityDiagramText.WholeTree(tree, path.NodeIds, PriorityDiagramPalette.Light),
            PathViewDiagram = PriorityDiagramText.PathView(tree, path.NodeIds, PriorityDiagramPalette.Light),
            LimitsTaxYear = walk.LimitsTaxYear,
            Assumptions = new PriorityAssumptionsDto
            {
                BaseReturnPercent = PriorityAssumptions.BaseReturnPercent,
                ConservativeReturnPercent = PriorityAssumptions.ConservativeReturnPercent,
                AggressiveReturnPercent = PriorityAssumptions.AggressiveReturnPercent,
                CashYieldPercent = PriorityAssumptions.CashYieldPercent
            },
            Disclaimer = MoneyPrioritiesMessages.EstimateDisclaimer,
            PurposeNote = walk.PurposeNote
        };
    }

    /// <summary>
    /// A stored Walk walked against its own tree version, with the figures recomputed using the limits of its stored
    /// tax year (the current year when none is stored).
    /// </summary>
    private WalkView View(PriorityWalk walk)
    {
        PriorityTree tree = PriorityWalkMapping.TreeFor(walk.TreeVersion);
        var path = new PriorityPath(walk.TreeVersion, walk.Path.Split(','), PriorityWalkMapping.ToAnswers(walk));
        RetirementContributionLimits limits = RetirementContributionLimits.ForYear(walk.LimitsTaxYear ?? CurrentTaxYear());
        return new WalkView(tree, path, limits, PriorityLeafCalculator.Calculate(path, limits),
            tree.Node(path.LeafId).Label, PriorityPathSentences.For(tree, path.NodeIds));
    }

    private sealed record WalkView(PriorityTree Tree, PriorityPath Path, RetirementContributionLimits Limits,
        PriorityLeafFigures Figures, string LeafLabel, IReadOnlyList<string> PathSentences);
}
