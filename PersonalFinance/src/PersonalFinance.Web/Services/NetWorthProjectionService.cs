using PersonalFinance.Shared.DTOs;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Web.Services;

/// <summary>
/// Service responsible for computing net worth projection scenarios.
/// Separates business logic from the controller for testability and single responsibility.
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
    /// <param name="model">The view model containing user-configurable parameters.</param>
    /// <returns>A projection DTO with Base, Conservative, and Aggressive scenarios.</returns>
    public static NetWorthProjectionDto ComputeProjection(NetWorthViewModel model)
    {
        var currentYear = DateTime.Now.Year;
        var projection = new NetWorthProjectionDto
        {
            CurrentYear = currentYear,
            CurrentAge = model.CurrentAge,
            AnnualIncome = model.AnnualIncome,
            AnnualSpending = model.AnnualSpending,
            FiTarget = model.AnnualSpending * FiRuleMultiplier
        };

        // Base scenario
        projection.Base = BuildScenario(
            "Base", model.StartingBalance, model.AnnualSavings, model.CurrentAge,
            model.RetirementAge, model.BaseRoi / 100m, currentYear);

        // Conservative scenario
        projection.Conservative = BuildScenario(
            "Conservative", model.StartingBalance, model.AnnualSavings, model.CurrentAge,
            model.RetirementAge, model.ConservativeRoi / 100m, currentYear);

        // Aggressive scenario
        projection.Aggressive = BuildScenario(
            "Aggressive", model.StartingBalance, model.AggressiveSavings, model.CurrentAge,
            model.RetirementAge, model.AggressiveRoi / 100m, currentYear);

        return projection;
    }

    /// <summary>
    /// Builds a single scenario by computing year-by-year compound interest growth.
    /// </summary>
    /// <param name="name">Scenario name (e.g., "Base").</param>
    /// <param name="startingBalance">Initial net worth balance.</param>
    /// <param name="annualSavings">Amount saved and invested each year.</param>
    /// <param name="startAge">Age at the start of the projection.</param>
    /// <param name="retirementAge">Target retirement age.</param>
    /// <param name="roiRate">Annual return on investment as a decimal (e.g., 0.07 for 7%).</param>
    /// <param name="startYear">Calendar year the projection starts.</param>
    /// <returns>A scenario DTO containing year-by-year data and summary metrics.</returns>
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
