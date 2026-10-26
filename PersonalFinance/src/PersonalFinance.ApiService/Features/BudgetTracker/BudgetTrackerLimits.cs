using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Features.BudgetTracker;

/// <summary>
/// The bounds the Budget Tracker's validators and rejections enforce, and the ready-made API error arguments that
/// name them (ADR 0017), so each limit is written once and reaches the user formatted for their culture.
/// </summary>
internal static class BudgetTrackerLimits
{
    /// <summary>The earliest year a month plan or sheet may name.</summary>
    public const int MinYear = 1900;

    /// <summary>The latest year a month plan or sheet may name.</summary>
    public const int MaxYear = 2100;

    /// <summary>The largest amount a planned amount, Actual Override or transaction may hold (decimal(18,2)).</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    /// <summary>The longest category name.</summary>
    public const int CategoryNameMaxLength = 100;

    /// <summary>The longest transaction description.</summary>
    public const int DescriptionMaxLength = 200;

    /// <summary>The <c>min</c> (0) and <c>max</c> amount arguments of every amount range message.</summary>
    public static readonly IReadOnlyDictionary<string, ApiErrorArgDto> AmountRangeArgs =
        ErrorArgs.MoneyRange(0m, MaxAmount, SupportedCurrencies.Default.Code);

    /// <summary>The <c>min</c> and <c>max</c> arguments of the year range message.</summary>
    public static readonly IReadOnlyDictionary<string, ApiErrorArgDto> YearRangeArgs = NumberRange(MinYear, MaxYear);

    /// <summary>The <c>min</c> (1) and <c>max</c> (12) arguments of the month range message.</summary>
    public static readonly IReadOnlyDictionary<string, ApiErrorArgDto> MonthRangeArgs = NumberRange(1, 12);

    /// <summary>Whether <paramref name="year"/> and <paramref name="month"/> name a month a sheet may be addressed by.</summary>
    public static bool IsValidMonth(int year, int month) =>
        year is >= MinYear and <= MaxYear && month is >= 1 and <= 12;

    private static Dictionary<string, ApiErrorArgDto> NumberRange(int min, int max) => new()
    {
        ["min"] = ErrorArgs.Number("min", min)["min"],
        ["max"] = ErrorArgs.Number("max", max)["max"]
    };
}
