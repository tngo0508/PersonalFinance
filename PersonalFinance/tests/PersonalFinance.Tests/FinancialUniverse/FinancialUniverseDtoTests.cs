using System.Text.Json;
using PersonalFinance.Shared.DTOs.FinancialUniverse;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Tests.FinancialUniverse;

/// <summary>
/// The graph crosses from ApiService to Web as JSON (the ApiService's options, then Refit's): the enums travel as
/// their names, a nullable enum included, and nothing is lost on the way back.
/// </summary>
public class FinancialUniverseDtoTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Graph_RoundTripsThroughJson_WithEnumsAsNames()
    {
        var graph = new FinancialUniverseGraphDto
        {
            Year = 2026,
            Month = 10,
            CurrencyCode = "USD",
            BudgetSource = BudgetDataSource.GoogleSheet,
            Nodes =
            [
                new FinancialUniverseNodeDto
                {
                    Id = "goal-3", Kind = FinancialUniverseNodeKind.SavingsGoal, Name = "Tết", Amount = 100m, SecondaryAmount = 200m,
                    CurrencyCode = "USD", NativeAmount = 2_600_000m, NativeCurrencyCode = "VND", SourceId = 3,
                    LinkTarget = FinancialUniverseLinkTarget.SavingsGoals
                },
                new FinancialUniverseNodeDto
                {
                    Id = "placeholder-networth", Kind = FinancialUniverseNodeKind.Placeholder, CurrencyCode = "USD",
                    LinkTarget = FinancialUniverseLinkTarget.NetWorth, PlaceholderFor = FinancialUniverseNodeKind.NetWorth
                }
            ],
            Edges = [new FinancialUniverseEdgeDto { SourceId = "savings", TargetId = "goal-3", Weight = 0m }],
            AvailableMonths = [new FinancialUniverseMonthDto { Year = 2026, Month = 10 }, new FinancialUniverseMonthDto { Year = 2026, Month = 9 }]
        };

        string json = JsonSerializer.Serialize(graph, Web);
        FinancialUniverseGraphDto back = JsonSerializer.Deserialize<FinancialUniverseGraphDto>(json, Web)!;

        Assert.Contains("\"kind\":\"SavingsGoal\"", json, StringComparison.Ordinal);
        Assert.Contains("\"placeholderFor\":\"NetWorth\"", json, StringComparison.Ordinal);
        Assert.Contains("\"budgetSource\":\"GoogleSheet\"", json, StringComparison.Ordinal);
        Assert.Equal(BudgetDataSource.GoogleSheet, back.BudgetSource);
        Assert.Equal(FinancialUniverseNodeKind.NetWorth, back.Nodes[1].PlaceholderFor);
        Assert.Null(back.Nodes[0].PlaceholderFor);
        Assert.Equal((FinancialUniverseNodeKind.SavingsGoal, "Tết", 100m, 200m, 2_600_000m, "VND", 3, FinancialUniverseLinkTarget.SavingsGoals),
            (back.Nodes[0].Kind, back.Nodes[0].Name, back.Nodes[0].Amount, back.Nodes[0].SecondaryAmount, back.Nodes[0].NativeAmount,
                back.Nodes[0].NativeCurrencyCode, back.Nodes[0].SourceId, back.Nodes[0].LinkTarget));
        Assert.Equal(0m, Assert.Single(back.Edges).Weight);
        Assert.Equal([(2026, 10), (2026, 9)], back.AvailableMonths.Select(m => (m.Year, m.Month)));
    }
}
