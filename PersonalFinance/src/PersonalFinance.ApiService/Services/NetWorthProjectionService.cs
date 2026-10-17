using PersonalFinance.Shared.DTOs.NetWorth;

namespace PersonalFinance.ApiService.Services;

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
        var currentYear = DateTime.Now.Year;
        var projection = new NetWorthProjectionDto
        {
            CurrentYear = currentYear,
            CurrentAge = request.CurrentAge,
            AnnualIncome = request.AnnualIncome,
            AnnualSpending = request.AnnualSpending,
            FiTarget = request.AnnualSpending * FiRuleMultiplier
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
            var roi = balance * roiRate;
            var endingBalance = balance + annualSavings + roi;

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
