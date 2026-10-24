using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Builds the fund returns snapshot: each fund's monthly series from the filings read, and per window each fund's
/// total and annualized return, volatility, Sharpe ratio and worst fall, and each pair's correlation. Every figure is
/// computed here, in the refresh; the browser only selects them.
/// </summary>
internal static class FundReturnsBuilder
{
    /// <summary>The window lengths, in years.</summary>
    public static readonly IReadOnlyList<int> WindowYears = [1, 3, 5];

    // Percentages, ratios and correlations are kept to two decimals.
    private const int RatioDecimals = 2;

    /// <summary>
    /// The snapshot over <paramref name="records"/>, with Sharpe ratios against <paramref name="treasuryBills"/>
    /// (monthly 3-month bill rates, percent a year); null when no fund has a return.
    /// </summary>
    public static FundReturnsSnapshotDto? Build(IReadOnlyList<TrackedFund> funds,
        IEnumerable<FundReturnFilingRecord> records, IReadOnlyList<(DateOnly Month, decimal Rate)> treasuryBills)
    {
        ILookup<string, FundReturnFilingRecord> byTicker = records
            .Where(r => r.ReportDate is not null && r.Returns is { Count: 3 })
            .ToLookup(r => r.Ticker, StringComparer.OrdinalIgnoreCase);
        List<Series> series = funds
            .Select(f => SeriesOf(f.Ticker, byTicker[f.Ticker]))
            .OfType<Series>()
            .ToList();
        if (series.Count == 0)
        {
            return null;
        }

        Dictionary<DateOnly, decimal> bills = treasuryBills
            .GroupBy(b => b.Month)
            .ToDictionary(g => g.Key, g => g.Last().Rate);
        DateOnly latest = series.Max(s => s.Last);
        return new FundReturnsSnapshotDto
        {
            AsOf = latest.AddMonths(1).AddDays(-1),
            Funds = series.Select(s => new FundReturnSeriesDto
            {
                Ticker = s.Ticker,
                FirstMonth = s.First,
                LastMonth = s.Last,
                Returns = s.Values,
            }).ToList(),
            Windows = WindowYears.Select(years => Window(years, series, bills)).ToList(),
        };
    }

    /// <summary>The first day of the month <paramref name="date"/> falls in.</summary>
    public static DateOnly MonthOf(DateOnly date) => new(date.Year, date.Month, 1);

    /// <summary>
    /// A fund's monthly series: each filing gives the report month and the two before it; of two filings for one
    /// month, the later-filed one's return is kept. Null when no filing gives a return.
    /// </summary>
    private static Series? SeriesOf(string ticker, IEnumerable<FundReturnFilingRecord> records)
    {
        var months = new SortedDictionary<DateOnly, decimal>();
        foreach (FundReturnFilingRecord record in records
                     .OrderBy(r => r.FiledOn)
                     .ThenBy(r => r.AccessionNumber, StringComparer.Ordinal))
        {
            DateOnly reportMonth = MonthOf(record.ReportDate!.Value);
            for (int k = 0; k < 3; k++)
            {
                if (record.Returns![k] is { } value)
                {
                    months[reportMonth.AddMonths(k - 2)] = value;
                }
            }
        }

        if (months.Count == 0)
        {
            return null;
        }

        DateOnly first = months.Keys.First();
        DateOnly last = months.Keys.Last();
        var values = new List<decimal?>();
        for (DateOnly month = first; month <= last; month = month.AddMonths(1))
        {
            values.Add(months.TryGetValue(month, out decimal value) ? value : null);
        }

        return new Series(ticker, first, last, values);
    }

    /// <summary>The figures for one window.</summary>
    private static FundReturnsWindowDto Window(int years, List<Series> series, Dictionary<DateOnly, decimal> bills)
    {
        int months = years * 12;
        var window = new FundReturnsWindowDto { Years = years };
        foreach (Series fund in series)
        {
            if (fund.Window(fund.Last, months) is { } returns)
            {
                window.Funds.Add(Metrics(fund.Ticker, fund.Last.AddMonths(1 - months), fund.Last, returns, bills));
            }
        }

        for (int a = 0; a < series.Count; a++)
        {
            for (int b = a + 1; b < series.Count; b++)
            {
                DateOnly end = series[a].Last < series[b].Last ? series[a].Last : series[b].Last;
                if (series[a].Window(end, months) is { } first && series[b].Window(end, months) is { } second
                    && Correlation(first, second) is { } correlation)
                {
                    window.Correlations.Add(new FundCorrelationDto
                    {
                        A = a,
                        B = b,
                        Correlation = RoundRatio(correlation),
                        End = end,
                    });
                }
            }
        }

        return window;
    }

    /// <summary>One fund's figures over the months from <paramref name="start"/> to <paramref name="end"/>.</summary>
    private static FundWindowMetricsDto Metrics(string ticker, DateOnly start, DateOnly end, double[] returns,
        Dictionary<DateOnly, decimal> bills)
    {
        double growth = 1;
        double peak = 1;
        double worst = 0;
        foreach (double r in returns)
        {
            growth *= 1 + r / 100;
            peak = Math.Max(peak, growth);
            worst = Math.Min(worst, growth / peak - 1);
        }

        double annualized = (Math.Pow(growth, 12.0 / returns.Length) - 1) * 100;
        double volatility = StandardDeviation(returns) * Math.Sqrt(12);
        List<decimal> windowBills = [];
        for (DateOnly month = start; month <= end; month = month.AddMonths(1))
        {
            if (bills.TryGetValue(month, out decimal rate))
            {
                windowBills.Add(rate);
            }
        }

        decimal? bill = windowBills.Count == 0 ? null : windowBills.Average();
        return new FundWindowMetricsDto
        {
            Ticker = ticker,
            Start = start,
            End = end,
            TotalReturn = RoundPercent((decimal)((growth - 1) * 100)),
            AnnualizedReturn = RoundPercent((decimal)annualized),
            Volatility = RoundPercent((decimal)volatility),
            Sharpe = bill is null || volatility <= 0 ? null : RoundRatio((annualized - (double)bill.Value) / volatility),
            TreasuryBill = bill is null ? null : RoundPercent(bill.Value),
            WorstFall = RoundPercent((decimal)(worst * 100)),
        };
    }

    /// <summary>The sample standard deviation (n - 1).</summary>
    private static double StandardDeviation(double[] values)
    {
        double mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1));
    }

    /// <summary>The Pearson correlation of two equally long series; null when either does not vary.</summary>
    private static double? Correlation(double[] x, double[] y)
    {
        double meanX = x.Average();
        double meanY = y.Average();
        double covariance = 0;
        double varianceX = 0;
        double varianceY = 0;
        for (int i = 0; i < x.Length; i++)
        {
            covariance += (x[i] - meanX) * (y[i] - meanY);
            varianceX += (x[i] - meanX) * (x[i] - meanX);
            varianceY += (y[i] - meanY) * (y[i] - meanY);
        }

        return varianceX <= 0 || varianceY <= 0 ? null : covariance / Math.Sqrt(varianceX * varianceY);
    }

    private static decimal RoundPercent(decimal value) =>
        Math.Round(value, RatioDecimals, MidpointRounding.AwayFromZero);

    private static decimal RoundRatio(double value) =>
        Math.Round((decimal)value, RatioDecimals, MidpointRounding.AwayFromZero);

    /// <summary>A fund's monthly returns, one per month from <see cref="First"/> to <see cref="Last"/> (null for a gap).</summary>
    private sealed record Series(string Ticker, DateOnly First, DateOnly Last, List<decimal?> Values)
    {
        /// <summary>
        /// The <paramref name="months"/> returns ending with <paramref name="end"/>; null when the series does not
        /// cover them all.
        /// </summary>
        public double[]? Window(DateOnly end, int months)
        {
            DateOnly start = end.AddMonths(1 - months);
            if (start < First || end > Last)
            {
                return null;
            }

            int offset = (start.Year - First.Year) * 12 + start.Month - First.Month;
            double[] returns = new double[months];
            for (int i = 0; i < months; i++)
            {
                if (Values[offset + i] is not { } value)
                {
                    return null;
                }

                returns[i] = (double)value;
            }

            return returns;
        }
    }
}
