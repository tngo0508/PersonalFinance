using System.Globalization;
using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.DTOs.FinancialUniverse;
using PersonalFinance.Shared.Enums;
using PersonalFinance.Shared.Helpers;
using PersonalFinance.Web.Infrastructure.Localization;

namespace PersonalFinance.Web.Models.FinancialUniverse;

/// <summary>
/// Turns the ApiService's graph into the page's view model: every label in the request's Language, every amount
/// formatted in the Reporting Currency, the nodes grouped into the list view's sections in rank order, the detail
/// panel's lines per node (kind, period, native amount, owning feature) and the month picker's options. A category,
/// goal or scenario keeps the user's own name; the app's nodes (Income, Other, Savings, Shortfall, Net Worth) and the
/// Placeholders are localized here. The JavaScript composes no strings (spec #617).
/// </summary>
public static class FinancialUniverseText
{
    /// <summary>The list view's sections in rank order, each with the kinds it lists.</summary>
    private static readonly (FinancialUniverseNodeKind Kind, string Key, string TitleKey, FinancialUniverseNodeKind[] Lists)[] Sections =
    [
        (FinancialUniverseNodeKind.IncomeSource, "income-sources", "FinancialUniverse.Section.IncomeSources",
            [FinancialUniverseNodeKind.IncomeSource]),
        (FinancialUniverseNodeKind.Income, "income", "FinancialUniverse.Section.Income", [FinancialUniverseNodeKind.Income]),
        (FinancialUniverseNodeKind.ExpenseCategory, "expense-categories", "FinancialUniverse.Section.ExpenseCategories",
            [FinancialUniverseNodeKind.ExpenseCategory, FinancialUniverseNodeKind.Other]),
        (FinancialUniverseNodeKind.Savings, "savings", "FinancialUniverse.Section.Savings",
            [FinancialUniverseNodeKind.Savings, FinancialUniverseNodeKind.Shortfall]),
        (FinancialUniverseNodeKind.SavingsGoal, "savings-goals", "FinancialUniverse.Section.SavingsGoals",
            [FinancialUniverseNodeKind.SavingsGoal]),
        (FinancialUniverseNodeKind.NetWorth, "net-worth", "FinancialUniverse.Section.NetWorth", [FinancialUniverseNodeKind.NetWorth]),
        (FinancialUniverseNodeKind.InvestmentScenario, "investment-scenarios", "FinancialUniverse.Section.InvestmentScenarios",
            [FinancialUniverseNodeKind.InvestmentScenario])
    ];

    /// <summary>Every key this builder reads, so a test can check en.json has them all.</summary>
    public static IReadOnlyList<string> Keys { get; } =
    [
        .. Sections.Select(s => s.TitleKey),
        "FinancialUniverse.Node.Income", "FinancialUniverse.Node.Other", "FinancialUniverse.Node.Savings",
        "FinancialUniverse.Node.Shortfall", "FinancialUniverse.Node.NetWorth",
        "FinancialUniverse.Placeholder.IncomeSource", "FinancialUniverse.Placeholder.ExpenseCategory",
        "FinancialUniverse.Placeholder.SavingsGoal", "FinancialUniverse.Placeholder.NetWorth",
        "FinancialUniverse.Placeholder.InvestmentScenario",
        "FinancialUniverse.Row.GoalTarget", "FinancialUniverse.Row.AnnualSavings", "FinancialUniverse.Row.MonthlyContribution",
        "FinancialUniverse.Link.BudgetTracker", "FinancialUniverse.Link.Analysis", "FinancialUniverse.Link.SavingsGoals",
        "FinancialUniverse.Link.NetWorth", "FinancialUniverse.Link.InvestmentCalculator",
        .. Enum.GetValues<FinancialUniverseNodeKind>().Select(kind => "FinancialUniverse.Kind." + kind),
        .. Enum.GetValues<FinancialUniverseLinkTarget>().Select(target => "Shell.Nav." + target),
        "FinancialUniverse.Panel.Period.Month", "FinancialUniverse.Panel.Period.Latest"
    ];

    /// <summary>The kinds whose amount is the selected month's figure; the rest show their latest saved figure.</summary>
    private static readonly HashSet<FinancialUniverseNodeKind> MonthBound =
    [
        FinancialUniverseNodeKind.IncomeSource, FinancialUniverseNodeKind.Income, FinancialUniverseNodeKind.ExpenseCategory,
        FinancialUniverseNodeKind.Other, FinancialUniverseNodeKind.Savings, FinancialUniverseNodeKind.Shortfall
    ];

    /// <summary>The month picker's option value and the query parameter the page reads back: <c>yyyy-MM</c>.</summary>
    public const string PeriodFormat = "yyyy-MM";

    /// <summary>Parses a picker value (<c>2026-10</c>) into its year and month; false for anything else.</summary>
    public static bool TryParsePeriod(string? period, out int year, out int month)
    {
        if (DateTime.TryParseExact(period, PeriodFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
        {
            (year, month) = (parsed.Year, parsed.Month);
            return true;
        }

        (year, month) = (0, 0);
        return false;
    }

    /// <summary>The page's view model for <paramref name="graph"/>, in <paramref name="culture"/> and the localizer's Language.</summary>
    public static FinancialUniverseViewModel Build(FinancialUniverseGraphDto graph, IStringLocalizer localizer, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(culture);

        string currency = SupportedCurrencies.CodeOrDefault(graph.CurrencyCode);
        string monthLabel = MonthLabel(graph.Year, graph.Month, culture);
        var model = new FinancialUniverseViewModel
        {
            Year = graph.Year,
            Month = graph.Month,
            MonthLabel = monthLabel,
            CurrencyCode = currency,
            Conversion = graph.Conversion,
            BudgetSource = graph.BudgetSource,
            Nodes = graph.Nodes.Select(n => Node(n, currency, monthLabel, localizer, culture)).ToList(),
            AvailableMonths = MonthOptions(graph, culture),
            Edges = graph.Edges.Select(e => new FinancialUniverseEdgeViewModel
            {
                Source = e.SourceId,
                Target = e.TargetId,
                Weight = e.Weight,
                WeightText = e.Weight is { } weight ? MoneyFormatter.FormatWhole(weight, currency, culture) : string.Empty
            }).ToList()
        };

        Dictionary<string, FinancialUniverseNodeKind> standsFor = graph.Nodes
            .Where(n => n.PlaceholderFor is not null)
            .ToDictionary(n => n.Id, n => n.PlaceholderFor!.Value, StringComparer.Ordinal);
        foreach ((FinancialUniverseNodeKind kind, string key, string titleKey, FinancialUniverseNodeKind[] lists) in Sections)
        {
            List<FinancialUniverseNodeViewModel> rows = model.Nodes
                .Where(n => lists.Contains(n.IsPlaceholder ? standsFor[n.Id] : n.Kind))
                .ToList();
            if (rows.Count > 0)
            {
                model.Sections.Add(new FinancialUniverseSectionViewModel
                {
                    Kind = kind,
                    Key = key,
                    Title = localizer[titleKey],
                    Rows = rows
                });
            }
        }

        return model;
    }

    private static string MonthLabel(int year, int month, CultureInfo culture) =>
        new DateTime(year, month, 1).ToString("Y", culture);

    /// <summary>
    /// The picker's options as the ApiService listed them (newest first), with the graph's own month added when the
    /// list somehow lacks it, so the picker always shows the month on the page as selected.
    /// </summary>
    private static List<FinancialUniverseMonthOptionViewModel> MonthOptions(FinancialUniverseGraphDto graph, CultureInfo culture)
    {
        IEnumerable<(int Year, int Month)> months = graph.AvailableMonths.Select(m => (m.Year, m.Month));
        if (!months.Contains((graph.Year, graph.Month)))
        {
            months = months.Append((graph.Year, graph.Month)).OrderByDescending(m => m.Year).ThenByDescending(m => m.Month);
        }

        return months
            .Where(m => m.Month is >= 1 and <= 12)
            .Select(m => new FinancialUniverseMonthOptionViewModel
            {
                Year = m.Year,
                Month = m.Month,
                Value = new DateTime(m.Year, m.Month, 1).ToString(PeriodFormat, CultureInfo.InvariantCulture),
                Label = MonthLabel(m.Year, m.Month, culture),
                IsSelected = m == (graph.Year, graph.Month)
            })
            .ToList();
    }

    private static FinancialUniverseNodeViewModel Node(FinancialUniverseNodeDto node, string currency, string monthLabel,
        IStringLocalizer localizer, CultureInfo culture)
    {
        string Money(decimal amount) => MoneyFormatter.FormatWhole(amount, currency, culture);

        // An amount no rate could convert shows in its own currency rather than vanishing (ADR 0016).
        string amountText = node.Amount is { } amount
            ? Money(amount)
            : node.NativeAmount is { } native && SupportedCurrencies.IsSupported(node.NativeCurrencyCode)
                ? MoneyFormatter.FormatWhole(native, node.NativeCurrencyCode!, culture)
                : string.Empty;

        string detailText = (node.Kind, node.SecondaryAmount) switch
        {
            (FinancialUniverseNodeKind.SavingsGoal, { } target) => localizer.Named("FinancialUniverse.Row.GoalTarget", ("target", Money(target))),
            (FinancialUniverseNodeKind.NetWorth, { } yearly) => localizer.Named("FinancialUniverse.Row.AnnualSavings", ("amount", Money(yearly))),
            (FinancialUniverseNodeKind.InvestmentScenario, { } monthly) =>
                localizer.Named("FinancialUniverse.Row.MonthlyContribution", ("amount", Money(monthly))),
            _ => string.Empty
        };

        // The stored figure beside the converted one, only when they are in different currencies and there is a
        // converted one (with no rate, the amount line already shows the stored figure).
        bool nativeDiffers = node.Amount is not null && node.NativeAmount is not null
            && SupportedCurrencies.IsSupported(node.NativeCurrencyCode)
            && !string.Equals(node.NativeCurrencyCode, currency, StringComparison.OrdinalIgnoreCase);
        string nativeAmountText = nativeDiffers
            ? MoneyFormatter.FormatWhole(node.NativeAmount!.Value, node.NativeCurrencyCode!, culture)
            : string.Empty;

        string periodText = node.Kind == FinancialUniverseNodeKind.Placeholder
            ? string.Empty
            : MonthBound.Contains(node.Kind)
                ? localizer.Named("FinancialUniverse.Panel.Period.Month", ("month", monthLabel))
                : localizer["FinancialUniverse.Panel.Period.Latest"];

        return new FinancialUniverseNodeViewModel
        {
            Id = node.Id,
            Kind = node.Kind,
            Label = Label(node, localizer),
            AmountText = amountText,
            DetailText = detailText,
            Amount = node.Amount,
            LinkTarget = node.LinkTarget,
            LinkText = localizer["FinancialUniverse.Link." + node.LinkTarget],
            KindText = localizer["FinancialUniverse.Kind." + node.Kind],
            NativeAmountText = nativeAmountText,
            PeriodText = periodText,
            FeatureText = localizer["Shell.Nav." + node.LinkTarget],
            IsPlaceholder = node.Kind == FinancialUniverseNodeKind.Placeholder,
            ColorSlot = node.ColorSlot
        };
    }

    /// <summary>The user's own name where there is one; the app's name for its derived nodes and Placeholders.</summary>
    private static string Label(FinancialUniverseNodeDto node, IStringLocalizer localizer)
    {
        if (!string.IsNullOrWhiteSpace(node.Name))
        {
            return node.Name;
        }

        return node.Kind switch
        {
            FinancialUniverseNodeKind.Placeholder when node.PlaceholderFor is { } standsFor =>
                localizer["FinancialUniverse.Placeholder." + standsFor],
            FinancialUniverseNodeKind.Income => localizer["FinancialUniverse.Node.Income"],
            FinancialUniverseNodeKind.Other => localizer["FinancialUniverse.Node.Other"],
            FinancialUniverseNodeKind.Savings => localizer["FinancialUniverse.Node.Savings"],
            FinancialUniverseNodeKind.Shortfall => localizer["FinancialUniverse.Node.Shortfall"],
            FinancialUniverseNodeKind.NetWorth => localizer["FinancialUniverse.Node.NetWorth"],
            _ => node.Id
        };
    }
}
