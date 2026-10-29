using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Tests.TestSupport;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure.Localization;
using PersonalFinance.Web.Models.Networth;
using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.NetWorth;

/// <summary>
/// The FI Breakdown (GLOSSARY: FI Breakdown): the "How this is calculated" cards on the Net Worth page, one per
/// calculated figure, with its value, the inputs it comes from and its formula in the user's own numbers. The wording
/// follows the UI language and the amounts the request culture (localization spec §2.1, §2.2).
/// </summary>
public sealed class FiBreakdownBuilderTests : IDisposable
{
    private const char Nbsp = '\u00A0';
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

    public FiBreakdownBuilderTests() => UseCulture("en-US", "en");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
    }

    private static void UseCulture(string culture, string uiCulture)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(uiCulture);
    }

    private static FiBreakdownViewModel Breakdown(NetWorthProjectionDto projection) =>
        FiBreakdownBuilder.Build(projection, TestLocalization.Localizer);

    private static string Vi(string key, params (string Name, object? Value)[] args) =>
        LocalizerExtensions.FormatNamed(TestLocalization.Vietnamese(key), args.ToDictionary(a => a.Name, a => a.Value));

    private static NetWorthProjectionDto Project(decimal balance = 180_000m, decimal savings = 22_000m,
        decimal income = 85_000m, decimal spending = 48_000m, int currentAge = 34, int retirementAge = 65,
        decimal baseRoi = 7m, decimal conservativeRoi = 6m, decimal aggressiveRoi = 10m,
        decimal aggressiveSavings = 45_000m) =>
        NetWorthProjectionService.ComputeProjection(new NetWorthProjectionRequestDto
        {
            StartingBalance = balance,
            AnnualSavings = savings,
            AnnualIncome = income,
            AnnualSpending = spending,
            CurrentAge = currentAge,
            RetirementAge = retirementAge,
            BaseRoi = baseRoi,
            ConservativeRoi = conservativeRoi,
            AggressiveRoi = aggressiveRoi,
            AggressiveSavings = aggressiveSavings
        });

    private static FiBreakdownCardViewModel Card(FiBreakdownViewModel breakdown, string key) =>
        breakdown.Cards.Single(c => c.Key == key);

    // 0% returns keep the FI ages exact: 100,000 today at 35 + 100,000 a year reaches 1,000,000 at 44; Aggressive at 40.
    private static NetWorthProjectionDto ZeroReturn(int retirementAge = 60, decimal balance = 100_000m) =>
        Project(balance: balance, savings: 100_000m, spending: 40_000m, currentAge: 35, retirementAge: retirementAge,
            baseRoi: 0m, conservativeRoi: 0m, aggressiveRoi: 0m, aggressiveSavings: 200_000m);

    [Fact]
    public void Build_HasTheFourCardsInOrder()
    {
        FiBreakdownViewModel breakdown = Breakdown(Project());

        Assert.Equal(["savings-rate", "fi-target", "progress", "fi-age"], breakdown.Cards.Select(c => c.Key));
    }

    [Fact]
    public void SavingsRate_ShowsValueInputsAndFormula()
    {
        FiBreakdownCardViewModel card = Card(Breakdown(Project()), "savings-rate");

        Assert.Equal("Savings Rate", card.Label);
        Assert.Equal("25.9%", card.Value);
        Assert.Equal(["Annual Savings", "Annual Income"], card.Inputs.Select(i => i.Label));
        Assert.Equal("$22,000", card.Inputs[0].Value);
        Assert.Equal("$22,000 ÷ $85,000 = 25.9%", card.Formula);
    }

    [Fact]
    public void FiTarget_IsSpendingTimesTwentyFive()
    {
        FiBreakdownCardViewModel card = Card(Breakdown(Project()), "fi-target");

        Assert.Equal("$1,200,000", card.Value);
        Assert.Equal(["Annual Spending"], card.Inputs.Select(i => i.Label));
        Assert.Equal("$48,000 × 25 = $1,200,000", card.Formula);
    }

    [Fact]
    public void ProgressToFi_IsStartingBalanceOverTarget()
    {
        FiBreakdownCardViewModel card = Card(Breakdown(Project()), "progress");

        Assert.Equal("15.0%", card.Value);
        Assert.Equal(["Starting Balance", "FI Target"], card.Inputs.Select(i => i.Label));
        Assert.Equal("$180,000 ÷ $1,200,000 = 15.0%", card.Formula);
    }

    [Fact]
    public void FiAge_ShowsBaseAgeGrowthSentenceAndEveryScenario()
    {
        FiBreakdownCardViewModel card = Card(Breakdown(ZeroReturn()), "fi-age");

        Assert.Equal("FI age", card.Label);
        Assert.Equal("44 (in 9 years)", card.Value);
        Assert.Equal(
            ["Starting Balance", "Annual Savings", "Base return", "Current Age", "Retirement Age", "FI Target"],
            card.Inputs.Select(i => i.Label));
        Assert.Equal("Grow $100,000 at 0% a year, adding $100,000 a year, until it reaches $1,000,000.", card.Formula);
        Assert.Equal(
            ["Base (0%): age 44", "Conservative (0%): age 44", "Aggressive (0%, $200,000 a year): age 40"],
            card.Scenarios);
    }

    [Fact]
    public void FiAge_WhenNotReachedByRetirement_SaysSo()
    {
        FiBreakdownCardViewModel card = Card(Breakdown(ZeroReturn(retirementAge: 40)), "fi-age");

        Assert.Equal("Not reached by age 40", card.Value);
        Assert.Equal("Base (0%): not reached by age 40", card.Scenarios[0]);
        Assert.Equal("Aggressive (0%, $200,000 a year): age 40", card.Scenarios[2]);
    }

    [Theory]
    [InlineData(2_000_000, "35 (this year)")]
    [InlineData(900_000, "36 (in 1 year)")]
    public void FiAge_UsesTheRightWordingForNowAndOneYear(int balance, string expected)
    {
        FiBreakdownCardViewModel card = Card(Breakdown(ZeroReturn(balance: balance)), "fi-age");

        Assert.Equal(expected, card.Value);
    }

    [Fact]
    public void ReturnRates_KeepTheirDecimals()
    {
        FiBreakdownCardViewModel card = Card(Breakdown(Project(baseRoi: 6.5m)), "fi-age");

        Assert.StartsWith("Base (6.5%)", card.Scenarios[0]);
        Assert.Equal("6.5%", card.Inputs.Single(i => i.Label == "Base return").Value);
    }

    [Fact]
    public void WithNoSpending_ProgressAndFiAgeAskForSpending()
    {
        FiBreakdownViewModel breakdown = Breakdown(Project(spending: 0m));

        foreach (string key in new[] { "progress", "fi-age" })
        {
            FiBreakdownCardViewModel card = Card(breakdown, key);
            Assert.Equal("—", card.Value);
            Assert.Equal("Set your annual spending to see this.", card.Note);
            Assert.Null(card.Formula);
            Assert.Empty(card.Scenarios);
        }
    }

    [Fact]
    public void WithNoIncome_SavingsRateAsksForIncome()
    {
        FiBreakdownCardViewModel card = Card(Breakdown(Project(income: 0m)), "savings-rate");

        Assert.Equal("—", card.Value);
        Assert.Equal("Add your annual income to see this.", card.Note);
        Assert.Null(card.Formula);
    }

    [Fact]
    public async Task Calculate_ReturnsTheProjectionWithTheBreakdownForLiveUpdates()
    {
        (INetWorthApi api, ApiProxy<INetWorthApi> proxy) = ApiProxy.Create<INetWorthApi>();
        proxy.Returns(nameof(INetWorthApi.CalculateProjectionAsync), Project());
        var controller = new NetworthController(api, NullLogger<NetworthController>.Instance, TestLocalization.Localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        IActionResult result = await controller.Calculate(new NetWorthViewModel());

        JsonResult json = Assert.IsType<JsonResult>(result);
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(json.Value, WebJson));
        JsonElement root = document.RootElement;
        // The existing scripts keep reading the projection at the top level...
        Assert.Equal(1_200_000m, root.GetProperty("fiTarget").GetDecimal());
        Assert.True(root.TryGetProperty("base", out _));
        // ...and the breakdown rides alongside it.
        JsonElement cards = root.GetProperty("breakdown").GetProperty("cards");
        Assert.Equal("savings-rate", cards[0].GetProperty("key").GetString());
        Assert.Equal("25.9%", cards[0].GetProperty("value").GetString());
    }

    [Fact]
    public void InVietnamese_LabelsAreTranslatedAndAmountsUseTheVietnameseCulture()
    {
        UseCulture("vi-VN", "vi");

        FiBreakdownCardViewModel card = Card(Breakdown(Project()), "savings-rate");

        Assert.Equal(TestLocalization.Vietnamese("NetWorth.Field.SavingsRate"), card.Label);
        Assert.Equal("25,9%", card.Value);
        Assert.Equal(
            [TestLocalization.Vietnamese("NetWorth.Field.AnnualSavings"), TestLocalization.Vietnamese("NetWorth.Field.AnnualIncome")],
            card.Inputs.Select(i => i.Label));
        Assert.Equal($"22.000{Nbsp}$", card.Inputs[0].Value);
        Assert.Equal($"22.000{Nbsp}$ ÷ 85.000{Nbsp}$ = 25,9%", card.Formula);
    }

    [Fact]
    public void InVietnamese_FiAgeSentencesAreTranslated()
    {
        UseCulture("vi-VN", "vi");

        FiBreakdownCardViewModel card = Card(Breakdown(ZeroReturn()), "fi-age");

        Assert.Equal(TestLocalization.Vietnamese("NetWorth.Breakdown.FiAge"), card.Label);
        Assert.Equal(Vi("NetWorth.Breakdown.AgeWhen", ("age", 44), ("when", Vi("NetWorth.Breakdown.InYears.other", ("count", 9)))),
            card.Value);
        Assert.Equal(Vi("NetWorth.Breakdown.FiAgeFormula", ("balance", $"100.000{Nbsp}$"), ("rate", "0%"),
            ("savings", $"100.000{Nbsp}$"), ("target", $"1.000.000{Nbsp}$")), card.Formula);
        Assert.Equal(Vi("NetWorth.Breakdown.Scenario", ("name", TestLocalization.Vietnamese("NetWorth.Js.Scenario.Aggressive")),
                ("assumptions", Vi("NetWorth.Breakdown.RateAndSavings", ("rate", "0%"), ("savings", $"200.000{Nbsp}$"))),
                ("outcome", Vi("NetWorth.Breakdown.ReachedAge", ("age", 40)))),
            card.Scenarios[2]);
        Assert.DoesNotContain(card.Scenarios, line => line.Contains("age", StringComparison.Ordinal));
    }

    [Fact]
    public void InVietnamese_ReturnRatesUseTheDecimalComma()
    {
        UseCulture("vi-VN", "vi");

        FiBreakdownCardViewModel card = Card(Breakdown(Project(baseRoi: 6.5m)), "fi-age");

        Assert.Equal("6,5%", card.Inputs[2].Value);
    }
}
