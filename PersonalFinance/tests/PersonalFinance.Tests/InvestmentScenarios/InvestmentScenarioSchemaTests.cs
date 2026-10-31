using System.Text.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.InvestmentScenarios;
using PersonalFinance.Shared.Enums;
using Refit;

namespace PersonalFinance.Tests.InvestmentScenarios;

/// <summary>
/// The Investment Scenarios table as the Azure SQL migrations create it (#592): money as decimal(18,2), rates as
/// decimal(5,2), indexed by Account; and the scenario's mode and goal question travelling as their names between the
/// page, the Web app and the ApiService.
/// </summary>
public class InvestmentScenarioSchemaTests
{
    private static readonly Lazy<string> Script = new(() =>
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();
        return db.GetService<IMigrator>().GenerateScript();
    });

    [Theory]
    [InlineData("[Name] nvarchar(100) NOT NULL")]
    [InlineData("[Note] nvarchar(500) NULL")]
    [InlineData("[Mode] int NOT NULL")]
    [InlineData("[CurrencyCode] char(3) NOT NULL DEFAULT 'USD'")]
    [InlineData("[InitialAmount] decimal(18,2) NOT NULL")]
    [InlineData("[MonthlyContribution] decimal(18,2) NOT NULL")]
    [InlineData("[AssumedReturn] decimal(5,2) NOT NULL")]
    [InlineData("[Years] int NOT NULL")]
    [InlineData("[ContributionIncrease] decimal(5,2) NOT NULL")]
    [InlineData("[Inflation] decimal(5,2) NOT NULL")]
    [InlineData("[Fee] decimal(5,2) NOT NULL")]
    [InlineData("[DelayYears] int NOT NULL")]
    [InlineData("[StopAfterYear] int NULL")]
    [InlineData("[GoalTarget] decimal(18,2) NULL")]
    [InlineData("[GoalSolveFor] int NULL")]
    [InlineData("[UserId] nvarchar(450) NOT NULL")]
    [InlineData("[IsDeleted] bit NOT NULL")]
    public void Migration_CreatesEveryColumn(string column)
    {
        string script = Script.Value;
        int start = script.IndexOf("CREATE TABLE [InvestmentScenarios]", StringComparison.Ordinal);
        Assert.True(start >= 0, "No migration creates the InvestmentScenarios table.");
        int end = script.IndexOf(");", start, StringComparison.Ordinal);

        Assert.Contains(column, script[start..end], StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_IndexesScenariosByAccount()
    {
        Assert.Contains("CREATE INDEX [IX_InvestmentScenarios_UserId] ON [InvestmentScenarios] ([UserId]);",
            Script.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWebAppsRefitSerializer_SendsTheModeAndQuestionAsNames_AndTheApiServiceReadsThemBack()
    {
        JsonSerializerOptions refitOptions = SystemTextJsonContentSerializer.GetDefaultJsonSerializerOptions();
        var request = new SaveInvestmentScenarioRequestDto
        { Mode = InvestmentScenarioMode.Goal, GoalSolveFor = InvestmentGoalSolveFor.Years };

        string json = JsonSerializer.Serialize(request, refitOptions);
        SaveInvestmentScenarioRequestDto? read = JsonSerializer.Deserialize<SaveInvestmentScenarioRequestDto>(json,
            JsonSerializerOptions.Web);

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("Goal", document.RootElement.GetProperty("mode").GetString());
        Assert.Equal("Years", document.RootElement.GetProperty("goalSolveFor").GetString());
        Assert.NotNull(read);
        Assert.Equal((InvestmentScenarioMode.Goal, (InvestmentGoalSolveFor?)InvestmentGoalSolveFor.Years),
            (read.Mode, read.GoalSolveFor));
        Assert.Null(JsonSerializer.Deserialize<SaveInvestmentScenarioRequestDto>("""{"goalSolveFor":null}""",
            JsonSerializerOptions.Web)?.GoalSolveFor);
    }
}
