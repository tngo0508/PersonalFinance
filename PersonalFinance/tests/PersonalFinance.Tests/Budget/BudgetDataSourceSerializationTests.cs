using System.Text.Json;
using PersonalFinance.Shared.DTOs.BudgetTracker;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.Budget;

public class BudgetDataSourceSerializationTests
{
    private static readonly JsonSerializerOptions DefaultApiOptions = new() { PropertyNameCaseInsensitive = true };

    [Theory]
    [InlineData("{\"source\":\"budgetTracker\"}", BudgetDataSource.BudgetTracker)]
    [InlineData("{\"source\":\"BudgetTracker\"}", BudgetDataSource.BudgetTracker)]
    [InlineData("{\"source\":\"googleSheet\"}", BudgetDataSource.GoogleSheet)]
    [InlineData("{\"source\":2}", BudgetDataSource.BudgetTracker)]
    [InlineData("{\"source\":1}", BudgetDataSource.GoogleSheet)]
    public void SetBudgetDataSourceRequest_DeserializesStringAndNumericEnumValues(string json,
        BudgetDataSource expected)
    {
        SetBudgetDataSourceRequest? request = JsonSerializer.Deserialize<SetBudgetDataSourceRequest>(json, DefaultApiOptions);

        Assert.NotNull(request);
        Assert.Equal(expected, request.Source);
    }
}
