using System.Reflection;
using PersonalFinance.ApiService.Infrastructure;
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
        PropertyInfo[] properties = typeof(BudgetAllocationsDto).GetProperties();
        for (int i = 0; i < properties.Length; i++)
        {
            properties[i].SetValue(allocations, 100m + i);
        }

        IReadOnlyList<(string Name, decimal Amount)> categories = BudgetPlannerCategories.AllocationAmounts(allocations);

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
        for (int i = 0; i < amountProperties.Count; i++)
        {
            amountProperties[i].SetValue(budget, 100m + i);
        }

        BudgetAllocationsDto dto = budget.ToAllocationsDto();

        foreach (PropertyInfo? property in amountProperties)
        {
            string dtoName = property.Name[..^"Amount".Length];
            Assert.Equal(property.GetValue(budget), typeof(BudgetAllocationsDto).GetProperty(dtoName)!.GetValue(dto));
        }
    }
}
