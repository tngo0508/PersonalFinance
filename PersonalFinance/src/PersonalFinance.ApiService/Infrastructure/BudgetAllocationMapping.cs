using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.BudgetPlanner;

namespace PersonalFinance.ApiService.Infrastructure;

/// <summary>
/// The one hand-written mapping from a saved Budget Planner allocation to its DTO (ADR 0001: no AutoMapper).
/// List categories with <c>BudgetPlannerCategories.AllocationAmounts(budget.ToAllocationsDto())</c>.
/// </summary>
public static class BudgetAllocationMapping
{
    /// <summary>The ten Allocation Amounts of a saved budget.</summary>
    public static BudgetAllocationsDto ToAllocationsDto(this UserBudgetAllocation budget)
    {
        return new BudgetAllocationsDto
        {
            Housing = budget.HousingAmount,
            Food = budget.FoodAmount,
            Transportation = budget.TransportationAmount,
            Utilities = budget.UtilitiesAmount,
            Insurance = budget.InsuranceAmount,
            Healthcare = budget.HealthcareAmount,
            Savings = budget.SavingsAmount,
            DebtRepayment = budget.DebtRepaymentAmount,
            Entertainment = budget.EntertainmentAmount,
            Miscellaneous = budget.MiscellaneousAmount
        };
    }
}
