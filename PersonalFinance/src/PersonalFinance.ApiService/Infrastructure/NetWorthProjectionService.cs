using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// Service responsible for computing net worth projection scenarios.
/// Lives in the ApiService layer so the projection logic is shared via the API.
/// </summary>
public static class NetWorthProjectionService
{
    /// <summary>
    /// The multiplier for the 4% rule of financial independence.
    /// </summary>
    private const decimal FiRuleMultiplier = 25m;

    /// <summary>The money levers' step in US dollars, scaled to the projection's currency (<see cref="MoneyBounds"/>).</summary>
    private const long MoneyLeverStepDollars = 5_000;

    /// <summary>The return levers' step in percentage points.</summary>
    private const decimal ReturnLeverStep = 1m;

    /// <summary>
    /// Computes the complete net worth projection for all three scenarios.
    /// </summary>
    /// <param name="request">The request containing user-configurable parameters.</param>
    /// <returns>A projection DTO with Base, Conservative, and Aggressive scenarios.</returns>
    public static NetWorthProjectionDto ComputeProjection(NetWorthProjectionRequestDto request)
    {
        int currentYear = DateTime.Now.Year;
        decimal fiTarget = request.AnnualSpending * FiRuleMultiplier;
        var projection = new NetWorthProjectionDto
        {
            CurrentYear = currentYear,
            CurrentAge = request.CurrentAge,
            AnnualIncome = request.AnnualIncome,
            AnnualSpending = request.AnnualSpending,
            InflationPercent = request.InflationPercent,
            FiTarget = fiTarget,
            StartingBalance = request.StartingBalance,
            AnnualSavings = request.AnnualSavings,
            RetirementAge = request.RetirementAge,
            CurrencyCode = SupportedCurrencies.CodeOrDefault(request.CurrencyCode),
            SavingsRatePercent = FinanceMetrics.SavingsRatePercent(request.AnnualSavings, request.AnnualIncome),
            ProgressToFiPercent = fiTarget != 0m ? request.StartingBalance / fiTarget * 100m : null
        };

        bool hasProjection = request.RetirementAge > request.CurrentAge;
        decimal inflationRate = request.InflationPercent / 100m;
        NetWorthScenarioDto Scenario(string name, decimal savings, decimal roiPercent, decimal target) =>
            BuildScenario(name, request.StartingBalance, savings, request.CurrentAge, request.RetirementAge,
                roiPercent / 100m, inflationRate, currentYear, hasProjection ? target : 0m);

        projection.Base = Scenario("Base", request.AnnualSavings, request.BaseRoi, fiTarget);
        projection.Conservative = Scenario("Conservative", request.AnnualSavings, request.ConservativeRoi, fiTarget);
        projection.Aggressive = Scenario("Aggressive", request.AggressiveSavings, request.AggressiveRoi, fiTarget);

        decimal moneyStep = MoneyBounds.Max(MoneyLeverStepDollars, projection.CurrencyCode);
        decimal spendingCut = Math.Min(moneyStep, request.AnnualSpending);
        decimal returnCut = Math.Min(ReturnLeverStep, request.BaseRoi);
        projection.Levers =
        [
            Lever(NetWorthLevers.SaveMore, moneyStep,
                Scenario("Base", request.AnnualSavings + moneyStep, request.BaseRoi, fiTarget)),
            Lever(NetWorthLevers.SpendLess, spendingCut,
                Scenario("Base", request.AnnualSavings, request.BaseRoi,
                    (request.AnnualSpending - spendingCut) * FiRuleMultiplier)),
            Lever(NetWorthLevers.ReturnUp, ReturnLeverStep,
                Scenario("Base", request.AnnualSavings, request.BaseRoi + ReturnLeverStep, fiTarget)),
            Lever(NetWorthLevers.ReturnDown, returnCut,
                Scenario("Base", request.AnnualSavings, request.BaseRoi - returnCut, fiTarget))
        ];

        return projection;
    }

    /// <summary>
    /// The projection in <paramref name="conversion"/>'s Reporting Currency (ADR 0016): the five money inputs are
    /// converted once at the latest rate, then projected. Inputs already in the Reporting Currency are projected as
    /// they are, with no caption; when there is no rate at all the projection stays in the inputs' own currency and
    /// its caption says the conversion is unavailable.
    /// </summary>
    public static NetWorthProjectionDto ComputeProjection(NetWorthProjectionRequestDto request, ReportingConversion conversion)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(conversion);
        string inputCurrency = SupportedCurrencies.CodeOrDefault(request.CurrencyCode);
        if (conversion.IsReporting(inputCurrency))
        {
            return ComputeProjection(request);
        }

        decimal? startingBalance = conversion.Convert(request.StartingBalance, inputCurrency, null);
        decimal? annualSavings = conversion.Convert(request.AnnualSavings, inputCurrency, null);
        decimal? annualIncome = conversion.Convert(request.AnnualIncome, inputCurrency, null);
        decimal? annualSpending = conversion.Convert(request.AnnualSpending, inputCurrency, null);
        decimal? aggressiveSavings = conversion.Convert(request.AggressiveSavings, inputCurrency, null);
        bool converted = startingBalance is not null && annualSavings is not null && annualIncome is not null
                         && annualSpending is not null && aggressiveSavings is not null;

        NetWorthProjectionDto projection = converted
            ? ComputeProjection(new NetWorthProjectionRequestDto
            {
                CurrencyCode = conversion.ReportingCurrency,
                StartingBalance = startingBalance!.Value,
                AnnualSavings = annualSavings!.Value,
                AnnualIncome = annualIncome!.Value,
                AnnualSpending = annualSpending!.Value,
                CurrentAge = request.CurrentAge,
                RetirementAge = request.RetirementAge,
                BaseRoi = request.BaseRoi,
                ConservativeRoi = request.ConservativeRoi,
                AggressiveRoi = request.AggressiveRoi,
                AggressiveSavings = aggressiveSavings!.Value,
                InflationPercent = request.InflationPercent
            })
            : ComputeProjection(request);
        projection.Conversion = conversion.ToDto();
        return projection;
    }

    /// <summary>A lever: the outcome of the Base scenario re-run with one input changed by <paramref name="change"/>.</summary>
    private static NetWorthLeverDto Lever(string key, decimal change, NetWorthScenarioDto scenario) => new()
    {
        Key = key,
        Change = change,
        FiReachedAge = scenario.FiReachedAge,
        RealFiReachedAge = scenario.RealFiReachedAge,
        FinalNetWorth = scenario.FinalNetWorth,
        RealFinalNetWorth = scenario.RealFinalNetWorth
    };

    /// <summary>
    /// Builds a single scenario by compounding year by year from today's balance, in decimal throughout: each year
    /// earns <paramref name="roiRate"/> on the balance it starts with and adds the year's savings at its end, so the
    /// balance a row carries is the one held at that row's age. The rows run from the age after
    /// <paramref name="startAge"/> to <paramref name="retirementAge"/> (none when that is not later), each also in
    /// today's money at <paramref name="inflationRate"/>. The FI ages are the first ages at which the balance reaches
    /// <paramref name="fiTarget"/>: the start age when today's balance already does; a target of 0 leaves them unset.
    /// </summary>
    private static NetWorthScenarioDto BuildScenario(
        string name,
        decimal startingBalance,
        decimal annualSavings,
        int startAge,
        int retirementAge,
        decimal roiRate,
        decimal inflationRate,
        int startYear,
        decimal fiTarget)
    {
        var scenario = new NetWorthScenarioDto
        {
            Name = name,
            RoiPercent = roiRate * 100m,
            AnnualSavings = annualSavings,
            StartingBalance = startingBalance,
            StartingAge = startAge
        };

        decimal balance = startingBalance;
        decimal totalContributions = startingBalance;
        decimal inflationFactor = 1m;
        int year = startYear;

        // One year of saving and growth per row: the balance at startAge is today's, so the first row is the age after.
        for (int age = startAge + 1; age <= retirementAge; age++)
        {
            year++;
            decimal roi = balance * roiRate;
            decimal endingBalance = balance + annualSavings + roi;
            totalContributions += annualSavings;
            inflationFactor *= 1m + inflationRate;

            scenario.Years.Add(new NetWorthYearDto
            {
                Year = year,
                Age = age,
                StartingBalance = balance,
                Savings = annualSavings,
                Roi = roi,
                EndingBalance = endingBalance,
                TotalContributions = totalContributions,
                InflationFactor = inflationFactor,
                RealEndingBalance = endingBalance / inflationFactor
            });

            balance = endingBalance;
        }

        scenario.FinalNetWorth = balance;
        scenario.TotalContributions = totalContributions;
        scenario.TotalInterestEarned = balance - totalContributions;
        scenario.RealFinalNetWorth = balance / inflationFactor;
        if (fiTarget > 0m)
        {
            // Today's balance is today's money too, so a balance already at the target is at it in both views.
            bool alreadyThere = startingBalance >= fiTarget;
            scenario.FiReachedAge = alreadyThere
                ? startAge
                : scenario.Years.FirstOrDefault(y => y.EndingBalance >= fiTarget)?.Age;
            scenario.RealFiReachedAge = alreadyThere
                ? startAge
                : scenario.Years.FirstOrDefault(y => y.RealEndingBalance >= fiTarget)?.Age;
        }

        return scenario;
    }
}
