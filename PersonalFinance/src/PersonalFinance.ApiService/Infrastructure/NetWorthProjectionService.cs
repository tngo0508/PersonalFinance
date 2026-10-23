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
            FiTarget = fiTarget,
            StartingBalance = request.StartingBalance,
            AnnualSavings = request.AnnualSavings,
            RetirementAge = request.RetirementAge,
            SavingsRatePercent = FinanceMetrics.SavingsRatePercent(request.AnnualSavings, request.AnnualIncome),
            ProgressToFiPercent = fiTarget != 0m ? request.StartingBalance / fiTarget * 100m : null
        };

        // Base scenario
        projection.Base = BuildScenario(
            "Base", request.StartingBalance, request.AnnualSavings, request.CurrentAge,
            request.RetirementAge, request.BaseRoi / 100m, currentYear);

        // Conservative scenario
        projection.Conservative = BuildScenario(
            "Conservative", request.StartingBalance, request.AnnualSavings, request.CurrentAge,
            request.RetirementAge, request.ConservativeRoi / 100m, currentYear);

        // Aggressive scenario
        projection.Aggressive = BuildScenario(
            "Aggressive", request.StartingBalance, request.AggressiveSavings, request.CurrentAge,
            request.RetirementAge, request.AggressiveRoi / 100m, currentYear);

        bool hasProjection = request.RetirementAge > request.CurrentAge;
        foreach (NetWorthScenarioDto scenario in new[] { projection.Base, projection.Conservative, projection.Aggressive })
        {
            scenario.FiReachedAge = hasProjection && fiTarget > 0m
                ? scenario.Years.FirstOrDefault(y => y.EndingBalance >= fiTarget)?.Age
                : null;
        }

        return projection;
    }

    /// <summary>
    /// Builds a single scenario by computing year-by-year compound interest growth.
    /// </summary>
    private static NetWorthScenarioDto BuildScenario(
        string name,
        decimal startingBalance,
        decimal annualSavings,
        int startAge,
        int retirementAge,
        decimal roiRate,
        int startYear)
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
        int year = startYear;
        int age = startAge;

        while (age <= retirementAge)
        {
            decimal roi = balance * roiRate;
            decimal endingBalance = balance + annualSavings + roi;

            scenario.Years.Add(new NetWorthYearDto
            {
                Year = year,
                Age = age,
                StartingBalance = balance,
                Savings = annualSavings,
                Roi = roi,
                EndingBalance = endingBalance
            });

            totalContributions += annualSavings;
            balance = endingBalance;
            year++;
            age++;
        }

        scenario.FinalNetWorth = balance;
        scenario.TotalContributions = totalContributions;
        scenario.TotalInterestEarned = balance - totalContributions;

        return scenario;
    }
}
