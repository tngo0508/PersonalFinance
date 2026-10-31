using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.InvestmentScenarios;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.ApiService.Features.InvestmentScenarios;

/// <summary>
/// <see cref="IInvestmentScenarioService"/> over EF Core. Reads are no-tracking and ordered last edited first (the
/// last update, else the creation), newest id breaking a tie. A Grow scenario stores no goal. The 20-scenario limit
/// counts live rows only; two saves racing past it can leave 21, which only means one more scenario to tidy.
/// </summary>
internal sealed class InvestmentScenarioService : IInvestmentScenarioService
{
    private static readonly Expression<Func<InvestmentScenario, InvestmentScenarioDto>> ToDto = s => new InvestmentScenarioDto
    {
        Id = s.Id,
        Name = s.Name,
        Note = s.Note,
        Mode = s.Mode,
        CurrencyCode = s.CurrencyCode,
        InitialAmount = s.InitialAmount,
        MonthlyContribution = s.MonthlyContribution,
        AssumedReturn = s.AssumedReturn,
        Years = s.Years,
        ContributionIncrease = s.ContributionIncrease,
        Inflation = s.Inflation,
        Fee = s.Fee,
        DelayYears = s.DelayYears,
        StopAfterYear = s.StopAfterYear,
        GoalTarget = s.GoalTarget,
        GoalSolveFor = s.GoalSolveFor,
        CreatedAtUtc = s.CreatedAt,
        LastEditedAtUtc = s.UpdatedAt ?? s.CreatedAt
    };

    private static readonly Func<InvestmentScenario, InvestmentScenarioDto> ToDtoCompiled = ToDto.Compile();

    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public InvestmentScenarioService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task<List<InvestmentScenarioDto>> ListAsync(string userId, CancellationToken cancellationToken = default) =>
        ListQuery(userId).ToListAsync(cancellationToken);

    /// <summary>
    /// The Account's scenarios, last edited first. Internal so tests can check it translates to Azure SQL.
    /// </summary>
    internal IQueryable<InvestmentScenarioDto> ListQuery(string userId) =>
        _db.InvestmentScenarios.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt).ThenByDescending(s => s.Id)
            .Select(ToDto);

    /// <inheritdoc />
    public Task<InvestmentScenarioDto?> GetAsync(string userId, int id, CancellationToken cancellationToken = default) =>
        Owned(userId, id).AsNoTracking()
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<InvestmentScenarioDto> CreateAsync(string userId, SaveInvestmentScenarioRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureRoomAsync(userId, cancellationToken);

        var scenario = new InvestmentScenario
        {
            UserId = userId,
            CreatedAt = Now(),
            CurrencyCode = CurrencyCodeRules.Resolve(request.CurrencyCode)
        };
        Apply(scenario, request);
        _db.InvestmentScenarios.Add(scenario);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDtoCompiled(scenario);
    }

    /// <inheritdoc />
    public async Task<InvestmentScenarioDto?> UpdateAsync(string userId, int id,
        SaveInvestmentScenarioRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        InvestmentScenario? scenario = await FindAsync(userId, id, cancellationToken);
        if (scenario is null)
        {
            return null;
        }

        scenario.CurrencyCode = CurrencyCodeRules.Resolve(request.CurrencyCode, scenario.CurrencyCode);
        Apply(scenario, request);
        scenario.UpdatedAt = Now();
        await _db.SaveChangesAsync(cancellationToken);
        return ToDtoCompiled(scenario);
    }

    /// <inheritdoc />
    public async Task<InvestmentScenarioDto?> DuplicateAsync(string userId, int id, string name,
        CancellationToken cancellationToken = default)
    {
        InvestmentScenario? source = await Owned(userId, id).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (source is null)
        {
            return null;
        }

        await EnsureRoomAsync(userId, cancellationToken);
        var copy = new InvestmentScenario
        {
            UserId = userId,
            CreatedAt = Now(),
            Name = name.Trim(),
            Note = source.Note,
            CurrencyCode = source.CurrencyCode
        };
        CopyInputs(source, copy);
        _db.InvestmentScenarios.Add(copy);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDtoCompiled(copy);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string userId, int id, CancellationToken cancellationToken = default)
    {
        InvestmentScenario? scenario = await FindAsync(userId, id, cancellationToken);
        if (scenario is null)
        {
            return false;
        }

        scenario.IsDeleted = true;
        scenario.DeletedAt = Now();
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<InvestmentScenario?> FindAsync(string userId, int id, CancellationToken cancellationToken) =>
        Owned(userId, id).FirstOrDefaultAsync(cancellationToken);

    /// <summary>The Account's scenario with this id (none for another Account's, or a deleted one).</summary>
    private IQueryable<InvestmentScenario> Owned(string userId, int id) =>
        _db.InvestmentScenarios.Where(s => s.UserId == userId && s.Id == id);

    private async Task EnsureRoomAsync(string userId, CancellationToken cancellationToken)
    {
        int kept = await _db.InvestmentScenarios.CountAsync(s => s.UserId == userId, cancellationToken);
        if (kept >= InvestmentCalculatorLimits.MaxScenariosPerUser)
        {
            throw new ApiErrorException(ApiErrorKeys.InvestmentScenarios.LimitReached,
                $"You can keep up to {InvestmentCalculatorLimits.MaxScenariosPerUser} saved scenarios. Delete one to save another.",
                args: ErrorArgs.Number("max", InvestmentCalculatorLimits.MaxScenariosPerUser),
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    /// <summary>Writes the request's name, note and inputs; a Grow scenario keeps no goal.</summary>
    private static void Apply(InvestmentScenario scenario, SaveInvestmentScenarioRequestDto request)
    {
        scenario.Name = request.Name.Trim();
        scenario.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        CopyInputs(request, scenario);
    }

    /// <summary>Copies the twelve calculator inputs; a Grow scenario keeps no goal.</summary>
    private static void CopyInputs(IInvestmentScenarioInputs source, InvestmentScenario target)
    {
        bool goal = source.Mode == InvestmentScenarioMode.Goal;
        target.Mode = source.Mode;
        target.InitialAmount = source.InitialAmount;
        target.MonthlyContribution = source.MonthlyContribution;
        target.AssumedReturn = source.AssumedReturn;
        target.Years = source.Years;
        target.ContributionIncrease = source.ContributionIncrease;
        target.Inflation = source.Inflation;
        target.Fee = source.Fee;
        target.DelayYears = source.DelayYears;
        target.StopAfterYear = source.StopAfterYear;
        target.GoalTarget = goal ? source.GoalTarget : null;
        target.GoalSolveFor = goal ? source.GoalSolveFor : null;
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
