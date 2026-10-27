using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.SavingsGoals;

public class SavingsGoalDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public string? Description { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public double ProgressPercent { get; set; }

    /// <summary>The goal's Contributions, newest first (date descending, then id).</summary>
    public List<ContributionDto> Contributions { get; set; } = [];

    /// <summary>The net of the goal's Regular Contributions dated this calendar month.</summary>
    public decimal SavedThisMonth { get; set; }

    /// <summary>The goal's Pace, worked out from its Contribution log at today's date; null from an older ApiService.</summary>
    public SavingsGoalPaceDto? Pace { get; set; }
}

/// <summary>
/// What the Contribution log says about a goal (GLOSSARY.md: Pace): its status against the target date, what is
/// still needed per month, and when the recent rate would finish it. Descriptive only, never advice.
/// </summary>
public class SavingsGoalPaceDto
{
    public PaceStatus Status { get; set; }

    /// <summary>Remaining amount per month until the target date; only for dated goals not reached or past due.</summary>
    public decimal? NeededPerMonth { get; set; }

    public PaceRateState RateState { get; set; }

    /// <summary>The net of Regular Contributions per month over the last 90 days; set when the state is Positive.</summary>
    public decimal? RatePerMonth { get; set; }

    /// <summary>Today plus the remaining amount at the recent rate; null unless the rate is positive and the goal not reached.</summary>
    public DateOnly? ProjectedFinish { get; set; }
}

public class CreateSavingsGoalRequestDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The ISO 4217 currency of the target, the starting amount and every later Contribution, one of
    /// <c>SupportedCurrencies</c>; null means US dollars. It is set once: an update keeps it.
    /// </summary>
    public string? CurrencyCode { get; set; }

    public decimal TargetAmount { get; set; }
    public decimal? CurrentAmount { get; set; }
    public string? Description { get; set; }
    public DateTime? TargetDate { get; set; }
}

/// <summary>
/// Changes a goal's name, target, description or date. The saved amount only changes through Contributions, and
/// Reached is derived from it.
/// </summary>
public class UpdateSavingsGoalRequestDto
{
    public string? Name { get; set; }
    public decimal? TargetAmount { get; set; }
    public string? Description { get; set; }
    public DateTime? TargetDate { get; set; }
}

public class CreateSavingsGoalResponseDto
{
    public bool Success { get; set; }
    public int Id { get; set; }
}

public class UpdateSavingsGoalResponseDto
{
    public bool Success { get; set; }
}

public class DeleteSavingsGoalResponseDto
{
    public bool Success { get; set; }
}

/// <summary>
/// One dated movement of money into (positive) or out of (negative) a Savings Goal.
/// </summary>
public class ContributionDto
{
    public int Id { get; set; }
    public int SavingsGoalId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
    public ContributionKind Kind { get; set; }
}

/// <summary>
/// Adds or edits a Contribution. A null <see cref="Date"/> means today.
/// </summary>
public class SaveContributionRequestDto
{
    /// <summary>Signed, never zero: positive is a deposit, negative a withdrawal.</summary>
    public decimal Amount { get; set; }

    public DateOnly? Date { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// Sets a goal's saved amount to the figure the user's bank shows, recorded as an Adjustment of the difference.
/// </summary>
public class CorrectBalanceRequestDto
{
    /// <summary>The new saved amount, zero or more.</summary>
    public decimal Balance { get; set; }
}

public class SaveContributionResponseDto
{
    public bool Success { get; set; }

    /// <summary>The Contribution saved; null when a balance correction found no difference to record.</summary>
    public int? Id { get; set; }
}

public class DeleteContributionResponseDto
{
    public bool Success { get; set; }
}
