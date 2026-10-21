using PersonalFinance.ApiService.Services;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.BudgetPlanner;

namespace PersonalFinance.Tests.Budget;

/// <summary>
/// The ten Budget Planner categories are listed once; these tests fail when a category is added to the DTO or entity
/// but not to the list or mapping.
/// </summary>
public class BudgetPlannerCategoriesTests
{
    [Fact]
    public void AllocationAmounts_ListsEveryAllocationOnceInBudgetPageOrder()
    {
        var allocations = new BudgetAllocationsDto();
        var properties = typeof(BudgetAllocationsDto).GetProperties();
        for (var i = 0; i < properties.Length; i++)
        {
            properties[i].SetValue(allocations, 100m + i);
        }

        var categories = BudgetPlannerCategories.AllocationAmounts(allocations);

        Assert.Equal(properties.Length, categories.Count);
        Assert.Equal(properties.Length, categories.Select(c => c.Amount).Distinct().Count());
        Assert.Equal(
            ["Housing", "Food", "Transportation", "Utilities", "Insurance", "Healthcare", "Savings", "Debt Repayment",
                "Entertainment", "Miscellaneous"],
            categories.Select(c => c.Name));
    }

    [Fact]
    public void ToAllocationsDto_MapsEveryAllocationAmount()
    {
        var budget = new UserBudgetAllocation();
        var amountProperties = typeof(UserBudgetAllocation).GetProperties().Where(p => p.Name.EndsWith("Amount", StringComparison.Ordinal)).ToList();
        for (var i = 0; i < amountProperties.Count; i++)
        {
            amountProperties[i].SetValue(budget, 100m + i);
        }

        var dto = budget.ToAllocationsDto();

        foreach (var property in amountProperties)
        {
            var dtoName = property.Name[..^"Amount".Length];
            Assert.Equal(property.GetValue(budget), typeof(BudgetAllocationsDto).GetProperty(dtoName)!.GetValue(dto));
        }
    }
}
