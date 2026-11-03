using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;

namespace PersonalFinance.ApiService.Features.FinancialUniverse.Validators;

/// <summary>
/// Checks the month a Financial Universe graph is asked for. Year and month are optional together: a missing one takes
/// the current month's value. A failure carries <see cref="ApiErrorKeys.FinancialUniverse.MonthRange"/> (ADR 0017) with
/// the bounds as number arguments.
/// </summary>
internal static class FinancialUniverseQueryValidator
{
    /// <summary>The earliest year a graph can be asked for; no budget data or Contribution is dated before it.</summary>
    public const int MinYear = 2000;

    /// <summary>The latest year a graph can be asked for.</summary>
    public const int MaxYear = 2100;

    /// <summary>
    /// The month to build, or the rejection. A null <paramref name="year"/> or <paramref name="month"/> takes its value
    /// from <paramref name="now"/>.
    /// </summary>
    public static (int Year, int Month)? Resolve(int? year, int? month, DateTimeOffset now, out ApiErrorEntryDto? error)
    {
        int resolvedYear = year ?? now.Year;
        int resolvedMonth = month ?? now.Month;
        if (resolvedYear is < MinYear or > MaxYear || resolvedMonth is < 1 or > 12)
        {
            error = new ApiErrorEntryDto
            {
                Key = ApiErrorKeys.FinancialUniverse.MonthRange,
                Message = $"Choose a month between January {MinYear} and December {MaxYear}.",
                Field = resolvedMonth is < 1 or > 12 ? "month" : "year",
                Args = new Dictionary<string, ApiErrorArgDto>(ErrorArgs.Number("minYear", MinYear)
                    .Concat(ErrorArgs.Number("maxYear", MaxYear)))
            };
            return null;
        }

        error = null;
        return (resolvedYear, resolvedMonth);
    }
}
