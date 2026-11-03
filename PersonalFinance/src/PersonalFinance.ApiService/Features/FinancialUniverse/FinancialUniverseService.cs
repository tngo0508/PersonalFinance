using PersonalFinance.ApiService.Features.InvestmentScenarios;
using PersonalFinance.ApiService.Features.NetWorth;
using PersonalFinance.ApiService.Features.SavingsGoals;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.Currency;
using PersonalFinance.Shared.DTOs.FinancialUniverse;
using PersonalFinance.Shared.DTOs.InvestmentScenarios;
using PersonalFinance.Shared.DTOs.NetWorth;
using PersonalFinance.Shared.DTOs.SavingsGoals;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.ApiService.Features.FinancialUniverse;

/// <summary>
/// Builds the Financial Universe graph (spec #617) from the other Features' interfaces, left to right:
/// <list type="number">
/// <item>Income sources: the budget month's income categories with an actual, pooling into one Income node whose
/// amount is the month's actual income (the data does not say which source paid for what).</item>
/// <item>Expense categories with an actual, largest first; the ones under 3% of the month's total flow, or past the
/// five colour slots, fold into Other (<see cref="OtherFolding"/>, the Money Flow rule). Spending the categories do
/// not account for joins Other too, so Income always equals the categories, Other and Savings together.</item>
/// <item>Savings (income minus expenses) or Shortfall when the month spent more than it earned, as Money Flow defines
/// them.</item>
/// <item>Each Savings Goal, weighted by the month's Contributions (0 draws a thin edge), and Net Worth as a
/// relationship.</item>
/// <item>Each Investment Scenario, as a relationship from Net Worth.</item>
/// </list>
/// A kind the Account has nothing of becomes one Placeholder node linking to the feature that fills it, joined to its
/// neighbours by relationship edges so the graph stays in one piece. Every amount is in the Reporting Currency
/// (ADR 0016): the budget month arrives converted, goals bring their latest-rate figures, Contributions convert at
/// their own day, balances at the latest rate; a missing rate leaves the amount null, never zero.
/// </summary>
internal sealed class FinancialUniverseService : IFinancialUniverseService
{
    private const string IncomeId = "income";
    private const string OtherId = "other";
    private const string SavingsId = "savings";
    private const string ShortfallId = "shortfall";
    private const string NetWorthId = "net-worth";

    private readonly IBudgetDataProvider _budgetDataProvider;
    private readonly ISavingsGoalService _savingsGoals;
    private readonly INetWorthParametersService _netWorthParameters;
    private readonly IInvestmentScenarioService _investmentScenarios;
    private readonly ICurrencyConverter _currencyConverter;

    public FinancialUniverseService(IBudgetDataProvider budgetDataProvider, ISavingsGoalService savingsGoals,
        INetWorthParametersService netWorthParameters, IInvestmentScenarioService investmentScenarios,
        ICurrencyConverter currencyConverter)
    {
        _budgetDataProvider = budgetDataProvider;
        _savingsGoals = savingsGoals;
        _netWorthParameters = netWorthParameters;
        _investmentScenarios = investmentScenarios;
        _currencyConverter = currencyConverter;
    }

    /// <inheritdoc />
    public async Task<FinancialUniverseGraphDto> GetGraphAsync(string userId, int year, int month,
        CancellationToken cancellationToken = default)
    {
        // Sequential reads on purpose: the request-scoped DbContext behind these Services is not thread-safe.
        string reportingCurrency = await _currencyConverter.GetReportingCurrencyAsync(userId, cancellationToken);
        BudgetDataSource source = await _budgetDataProvider.GetActiveSourceAsync(userId, cancellationToken);
        BudgetMonthData? budgetMonth = await _budgetDataProvider.GetMonthAsync(userId, year, month, cancellationToken);
        List<SavingsGoalResponseDto> goals = await _savingsGoals.GetSavingsGoalsAsync(userId, cancellationToken);
        NetWorthParametersDto? netWorth = await _netWorthParameters.LoadAsync(userId, cancellationToken);
        List<InvestmentScenarioDto> scenarios = await _investmentScenarios.ListAsync(userId, cancellationToken);

        // One conversion for every amount the goals' own figures do not already cover: each month's Contribution at its
        // day, the Net Worth and scenario balances at the latest rate.
        List<MoneyAt> toConvert = goals
            .SelectMany(g => ContributionsIn(g, year, month).Select(c => new MoneyAt(GoalCurrency(g), c.Date)))
            .Concat(netWorth is null ? [] : [new MoneyAt(SupportedCurrencies.CodeOrDefault(netWorth.CurrencyCode), null)])
            .Concat(scenarios.Select(s => new MoneyAt(SupportedCurrencies.CodeOrDefault(s.CurrencyCode), null)))
            .ToList();
        ReportingConversion conversion = await _currencyConverter.PrepareAsync(reportingCurrency, toConvert, cancellationToken);

        var graph = new FinancialUniverseGraphDto
        {
            Year = year,
            Month = month,
            CurrencyCode = reportingCurrency,
            BudgetSource = source
        };
        var builder = new GraphBuilder(graph, reportingCurrency);
        FinancialUniverseLinkTarget budgetTarget = source == BudgetDataSource.GoogleSheet
            ? FinancialUniverseLinkTarget.Analysis
            : FinancialUniverseLinkTarget.BudgetTracker;

        string? rank3 = AddBudget(builder, budgetMonth, budgetTarget);
        AddGoals(builder, goals, rank3, conversion, year, month);
        string netWorthId = AddNetWorth(builder, netWorth, rank3, conversion);
        AddScenarios(builder, scenarios, netWorthId, conversion);

        graph.Conversion = ConversionDto.Combine(
            goals.Select(g => g.Conversion).Append(budgetMonth?.Conversion).Append(conversion.ToDto()));
        return graph;
    }

    /// <summary>
    /// The income sources, Income, the expense categories, Other and Savings or Shortfall. Returns the id of the rank-3
    /// node (Savings or Shortfall), or null when the month has neither income nor expenses.
    /// </summary>
    private static string? AddBudget(GraphBuilder builder, BudgetMonthData? budgetMonth,
        FinancialUniverseLinkTarget budgetTarget)
    {
        List<BudgetMonthCategoryData> incomeSources = Positive(budgetMonth, BudgetCategoryKind.Income);
        List<BudgetMonthCategoryData> expenseCategories = Positive(budgetMonth, BudgetCategoryKind.Expense);
        decimal income = Math.Max(Math.Max(0m, budgetMonth?.ActualIncome ?? 0m), incomeSources.Sum(c => c.Actual));
        decimal categoryTotal = expenseCategories.Sum(c => c.Actual);
        decimal expenses = Math.Max(Math.Max(0m, budgetMonth?.ActualExpenses ?? 0m), categoryTotal);

        if (incomeSources.Count == 0 && income == 0m)
        {
            builder.Placeholder(FinancialUniverseNodeKind.IncomeSource, budgetTarget);
        }

        for (int i = 0; i < incomeSources.Count; i++)
        {
            builder.Node($"income-source-{i}", FinancialUniverseNodeKind.IncomeSource, budgetTarget, incomeSources[i].Name,
                incomeSources[i].Actual);
        }

        if (income == 0m && expenses == 0m)
        {
            builder.Placeholder(FinancialUniverseNodeKind.ExpenseCategory, budgetTarget);
            return null;
        }

        builder.Node(IncomeId, FinancialUniverseNodeKind.Income, budgetTarget, null, income);
        for (int i = 0; i < incomeSources.Count; i++)
        {
            builder.Edge($"income-source-{i}", IncomeId, incomeSources[i].Actual);
        }

        decimal shortfall = Math.Max(0m, expenses - income);
        decimal savings = Math.Max(0m, income - expenses);
        decimal totalFlow = income + shortfall;
        OtherFoldingResult<BudgetMonthCategoryData> folding =
            OtherFolding.Fold(expenseCategories, c => c.Actual, totalFlow);
        decimal other = folding.OtherTotal + (expenses - categoryTotal);

        // Spending with no categories (a Sheet month with totals only) shows as Other, not as "nothing spent".
        if (expenses == 0m)
        {
            string placeholder = builder.Placeholder(FinancialUniverseNodeKind.ExpenseCategory, budgetTarget);
            builder.Edge(IncomeId, placeholder, null);
        }

        foreach (ColoredCategory<BudgetMonthCategoryData> kept in folding.Kept)
        {
            string id = $"expense-{kept.ColorSlot}";
            builder.Node(id, FinancialUniverseNodeKind.ExpenseCategory, budgetTarget, kept.Category.Name,
                kept.Category.Actual, colorSlot: kept.ColorSlot);
            builder.Edge(IncomeId, id, kept.Category.Actual);
        }

        if (other > 0m)
        {
            builder.Node(OtherId, FinancialUniverseNodeKind.Other, budgetTarget, null, other);
            builder.Edge(IncomeId, OtherId, other);
        }

        if (shortfall > 0m)
        {
            builder.Node(ShortfallId, FinancialUniverseNodeKind.Shortfall, FinancialUniverseLinkTarget.Analysis, null, shortfall);
            builder.Edge(IncomeId, ShortfallId, shortfall);
            return ShortfallId;
        }

        builder.Node(SavingsId, FinancialUniverseNodeKind.Savings, FinancialUniverseLinkTarget.Analysis, null, savings);
        builder.Edge(IncomeId, SavingsId, savings);
        return SavingsId;
    }

    /// <summary>Each goal with its saved and target amounts, weighted from Savings by the month's Contributions.</summary>
    private static void AddGoals(GraphBuilder builder, List<SavingsGoalResponseDto> goals, string? savingsId,
        ReportingConversion conversion, int year, int month)
    {
        if (goals.Count == 0)
        {
            string placeholder = builder.Placeholder(FinancialUniverseNodeKind.SavingsGoal, FinancialUniverseLinkTarget.SavingsGoals);
            builder.RelationshipFrom(savingsId, placeholder);
            return;
        }

        foreach (SavingsGoalResponseDto goal in goals)
        {
            string id = $"goal-{goal.Id}";
            string currency = GoalCurrency(goal);
            builder.Node(id, FinancialUniverseNodeKind.SavingsGoal, FinancialUniverseLinkTarget.SavingsGoals, goal.Name,
                goal.ReportingCurrentAmount, goal.ReportingTargetAmount, goal.CurrentAmount, currency, goal.Id);
            if (savingsId is not null)
            {
                // A month with no Contribution still draws the edge, thin, so the goal is seen to hang off Savings.
                decimal contributed = ContributionsIn(goal, year, month)
                    .Sum(c => conversion.Convert(c.Amount, currency, c.Date) ?? 0m);
                builder.Edge(savingsId, id, contributed);
            }
        }
    }

    /// <summary>Net Worth from its saved parameters, or its Placeholder. Returns the node's id either way.</summary>
    private static string AddNetWorth(GraphBuilder builder, NetWorthParametersDto? netWorth, string? savingsId,
        ReportingConversion conversion)
    {
        string id;
        if (netWorth is null)
        {
            id = builder.Placeholder(FinancialUniverseNodeKind.NetWorth, FinancialUniverseLinkTarget.NetWorth);
        }
        else
        {
            string currency = SupportedCurrencies.CodeOrDefault(netWorth.CurrencyCode);
            id = NetWorthId;
            builder.Node(id, FinancialUniverseNodeKind.NetWorth, FinancialUniverseLinkTarget.NetWorth, null,
                conversion.Convert(netWorth.StartingBalance, currency, null),
                conversion.Convert(netWorth.AnnualSavings, currency, null), netWorth.StartingBalance, currency);
        }

        builder.RelationshipFrom(savingsId, id);
        return id;
    }

    /// <summary>Each scenario by name, as a relationship from Net Worth.</summary>
    private static void AddScenarios(GraphBuilder builder, List<InvestmentScenarioDto> scenarios, string netWorthId,
        ReportingConversion conversion)
    {
        if (scenarios.Count == 0)
        {
            string placeholder = builder.Placeholder(FinancialUniverseNodeKind.InvestmentScenario,
                FinancialUniverseLinkTarget.InvestmentCalculator);
            builder.Edge(netWorthId, placeholder, null);
            return;
        }

        foreach (InvestmentScenarioDto scenario in scenarios)
        {
            string id = $"scenario-{scenario.Id}";
            string currency = SupportedCurrencies.CodeOrDefault(scenario.CurrencyCode);
            builder.Node(id, FinancialUniverseNodeKind.InvestmentScenario, FinancialUniverseLinkTarget.InvestmentCalculator,
                scenario.Name, conversion.Convert(scenario.InitialAmount, currency, null),
                conversion.Convert(scenario.MonthlyContribution, currency, null), scenario.InitialAmount, currency,
                scenario.Id);
            builder.Edge(netWorthId, id, null);
        }
    }

    /// <summary>The month's categories of <paramref name="kind"/> with a positive actual, largest first, then by name.</summary>
    private static List<BudgetMonthCategoryData> Positive(BudgetMonthData? month, BudgetCategoryKind kind) =>
        (month?.Categories ?? [])
        .Where(c => c.Kind == kind && c.Actual > 0m)
        .OrderByDescending(c => c.Actual)
        .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static IEnumerable<ContributionDto> ContributionsIn(SavingsGoalResponseDto goal, int year, int month) =>
        goal.Contributions.Where(c => c.Date.Year == year && c.Date.Month == month);

    private static string GoalCurrency(SavingsGoalResponseDto goal) => SupportedCurrencies.CodeOrDefault(goal.CurrencyCode);

    /// <summary>Appends nodes and edges in call order, which is the graph's rank and display order.</summary>
    private sealed class GraphBuilder(FinancialUniverseGraphDto graph, string reportingCurrency)
    {
        public void Node(string id, FinancialUniverseNodeKind kind, FinancialUniverseLinkTarget target, string? name,
            decimal? amount, decimal? secondaryAmount = null, decimal? nativeAmount = null, string? nativeCurrency = null,
            int? sourceId = null, int? colorSlot = null)
        {
            graph.Nodes.Add(new FinancialUniverseNodeDto
            {
                Id = id,
                Kind = kind,
                Name = name,
                Amount = amount,
                SecondaryAmount = secondaryAmount,
                CurrencyCode = reportingCurrency,
                NativeAmount = nativeAmount,
                NativeCurrencyCode = nativeAmount is null ? null : nativeCurrency,
                SourceId = sourceId,
                LinkTarget = target,
                ColorSlot = colorSlot
            });
        }

        /// <summary>One Placeholder for <paramref name="standsFor"/>; returns its id.</summary>
        public string Placeholder(FinancialUniverseNodeKind standsFor, FinancialUniverseLinkTarget target)
        {
            string id = $"placeholder-{standsFor.ToString().ToLowerInvariant()}";
            graph.Nodes.Add(new FinancialUniverseNodeDto
            {
                Id = id,
                Kind = FinancialUniverseNodeKind.Placeholder,
                CurrencyCode = reportingCurrency,
                LinkTarget = target,
                PlaceholderFor = standsFor
            });
            return id;
        }

        public void Edge(string sourceId, string targetId, decimal? weight) =>
            graph.Edges.Add(new FinancialUniverseEdgeDto { SourceId = sourceId, TargetId = targetId, Weight = weight });

        /// <summary>An unweighted edge from <paramref name="sourceId"/>, when there is such a node.</summary>
        public void RelationshipFrom(string? sourceId, string targetId)
        {
            if (sourceId is not null)
            {
                Edge(sourceId, targetId, null);
            }
        }
    }
}
