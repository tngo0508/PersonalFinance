namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Finds the Fed's hiking cycles in the monthly effective federal funds rate: runs of month-on-month rises, the way the
/// Performance chart shades them. The rate is a monthly average, so a hike late in a month shows partly in that month
/// and partly in the next.
/// </summary>
internal static class RateCycles
{
    /// <summary>The smallest month-on-month rise, in percentage points, that counts as a hike.</summary>
    public const decimal HikeStep = 0.10m;

    /// <summary>How many months without a hike a cycle may pause and still go on.</summary>
    public const int MaxPauseMonths = 2;

    /// <summary>The smallest total rise, in percentage points, for a run of hikes to count as a cycle.</summary>
    public const decimal MinTotalRise = 0.50m;

    /// <summary>
    /// The hiking cycles in <paramref name="rates"/> (monthly, by the first day of each month): each from its first
    /// month with a rise of at least <see cref="HikeStep"/> to its last, pausing at most <see cref="MaxPauseMonths"/>
    /// months, ended early by a fall of that size, and kept only when the rate rose by <see cref="MinTotalRise"/> or more
    /// in all. A cycle still under way at the end runs to the last month with a rise.
    /// </summary>
    public static IReadOnlyList<(DateOnly Start, DateOnly End)> Hikes(IReadOnlyList<(DateOnly Month, decimal Rate)> rates)
    {
        List<(DateOnly Month, decimal Rate)> ordered = rates.OrderBy(r => r.Month).ToList();
        var cycles = new List<(DateOnly Start, DateOnly End)>();
        int? start = null;
        int lastRise = 0;
        decimal startRate = 0m;

        void Close()
        {
            if (start is { } first && ordered[lastRise].Rate - startRate >= MinTotalRise)
            {
                cycles.Add((ordered[first].Month, ordered[lastRise].Month));
            }

            start = null;
        }

        for (int i = 1; i < ordered.Count; i++)
        {
            bool consecutive = ordered[i - 1].Month.AddMonths(1) == ordered[i].Month;
            decimal change = ordered[i].Rate - ordered[i - 1].Rate;
            if (!consecutive)
            {
                // A month with no rate breaks the run.
                Close();
            }

            if (change >= HikeStep && consecutive)
            {
                if (start is null)
                {
                    start = i;
                    startRate = ordered[i - 1].Rate;
                }

                lastRise = i;
            }
            else if (start is not null && (change <= -HikeStep || i - lastRise > MaxPauseMonths))
            {
                Close();
            }
        }

        Close();
        return cycles;
    }
}
