using Microsoft.Extensions.Localization;
using PersonalFinance.Shared.DTOs.SpreadsheetAnalysis;
using PersonalFinance.Web.Models.Analysis;

namespace PersonalFinance.Web.Services;

/// <summary>
///     Builds the Money Flow Sankey model for one month from the Analysis page's category insights.
///     Normal months flow Income into each category, Uncategorized, Other and Saved. In a Shortfall month, Income and
///     the Shortfall each flow as one band into a middle Spending node, which fans out to the categories.
/// </summary>
public static class MoneyFlowBuilder
{
    /// <summary>Categories below this share of the month's total flow are grouped into Other in the chart.</summary>
    private const decimal OtherThreshold = 0.03m;

    /// <summary>
    ///     How many categories get their own colour. Colours are assigned in display order from a palette validated
    ///     for neighbouring bands (see money-flow.js); they are never cycled, so further categories join Other.
    /// </summary>
    public const int MaxColoredCategories = 5;

    private const string IncomeKey = "income";
    private const string ShortfallKey = "shortfall";
    private const string SpendingKey = "spending";
    private const string UncategorizedKey = "uncategorized";
    private const string OtherKey = "other";
    private const string SavedKey = "saved";

    /// <summary>
    ///     Builds the model, or returns <c>null</c> when there are no insights or the month has neither income nor
    ///     expenses. The chart's own labels (Income, Saved, ...) and the month come in the request's Language and
    ///     culture; category names are the user's and stay as written.
    /// </summary>
    public static MoneyFlowViewModel? Build(CategoryInsightsDto? insights, IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        if (insights is null)
        {
            return null;
        }

        List<(string Name, decimal Amount)> categories = insights.Categories
            .Where(c => c.ActualAmount > 0m)
            .Select(c => (c.CategoryName, c.ActualAmount))
            .OrderByDescending(c => c.ActualAmount)
            .ThenBy(c => c.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        decimal income = Math.Max(0m, insights.ActualIncome);
        decimal categoryTotal = categories.Sum(c => c.Amount);
        decimal expenses = Math.Max(Math.Max(0m, insights.ActualExpenses), categoryTotal);
        if (income == 0m && expenses == 0m)
        {
            return null;
        }

        decimal uncategorized = expenses - categoryTotal;
        decimal shortfall = Math.Max(0m, expenses - income);
        decimal saved = Math.Max(0m, income - expenses);
        decimal totalFlow = income + shortfall;
        decimal shareBase = shortfall > 0m ? expenses : income;
        int rightColumn = shortfall > 0m ? 2 : 1;

        var model = new MoneyFlowViewModel
        {
            MonthLabel = MonthLabel(insights),
            MonthName = AnalysisText.MonthName(insights.MonthNumber),
            Income = income,
            Expenses = expenses,
            Shortfall = shortfall,
            Saved = saved,
            ShareBase = shareBase
        };

        if (income > 0m)
        {
            model.Nodes.Add(Node(IncomeKey, localizer["Analysis.MoneyFlow.Income"], income, MoneyFlowNodeKind.Income, 0, null));
        }

        if (shortfall > 0m)
        {
            model.Nodes.Add(Node(ShortfallKey, localizer["Analysis.MoneyFlow.Shortfall"], shortfall, MoneyFlowNodeKind.Shortfall, 0, null));
            model.Nodes.Add(Node(SpendingKey, localizer["Analysis.MoneyFlow.Spending"], expenses, MoneyFlowNodeKind.Spending, 1, null));
        }

        var rightHand = new List<MoneyFlowNodeViewModel>();
        decimal other = 0m;
        for (int i = 0; i < categories.Count; i++)
        {
            (string name, decimal amount) = categories[i];
            int colored = rightHand.Count;
            if (amount < totalFlow * OtherThreshold || colored >= MaxColoredCategories)
            {
                other += amount;
                continue;
            }

            MoneyFlowNodeViewModel node = Node($"category-{i}", name, amount, MoneyFlowNodeKind.Category, rightColumn,
                Share(amount, shareBase));
            node.ColorSlot = colored;
            rightHand.Add(node);
        }

        if (uncategorized > 0m)
        {
            rightHand.Add(Node(UncategorizedKey, localizer["Analysis.MoneyFlow.Uncategorized"], uncategorized, MoneyFlowNodeKind.Uncategorized,
                rightColumn, Share(uncategorized, shareBase)));
        }

        if (other > 0m)
        {
            rightHand.Add(Node(OtherKey, localizer["Analysis.MoneyFlow.Other"], other, MoneyFlowNodeKind.Other, rightColumn, Share(other, shareBase)));
        }

        if (saved > 0m)
        {
            rightHand.Add(Node(SavedKey, localizer["Analysis.MoneyFlow.Saved"], saved, MoneyFlowNodeKind.Saved, rightColumn, Share(saved, shareBase)));
        }

        model.Nodes.AddRange(rightHand);

        if (shortfall > 0m)
        {
            if (income > 0m)
            {
                model.Links.Add(Link(IncomeKey, SpendingKey, income));
            }

            model.Links.Add(Link(ShortfallKey, SpendingKey, shortfall));
            model.Links.AddRange(rightHand.Select(n => Link(SpendingKey, n.Key, n.Amount)));
        }
        else
        {
            model.Links.AddRange(rightHand.Select(n => Link(IncomeKey, n.Key, n.Amount)));
        }

        Dictionary<string, int?> slotsByKey = rightHand
            .Where(n => n.Kind == MoneyFlowNodeKind.Category)
            .ToDictionary(n => n.Key, n => n.ColorSlot);
        model.TableRows.AddRange(categories.Select((c, i) =>
        {
            MoneyFlowRowViewModel row = Row(c.Name, c.Amount, MoneyFlowNodeKind.Category, shareBase);
            row.ColorSlot = slotsByKey.GetValueOrDefault($"category-{i}");
            row.InOther = row.ColorSlot is null;
            return row;
        }));
        if (uncategorized > 0m)
        {
            model.TableRows.Add(Row(localizer["Analysis.MoneyFlow.Uncategorized"], uncategorized, MoneyFlowNodeKind.Uncategorized, shareBase));
        }

        if (saved > 0m)
        {
            model.TableRows.Add(Row(localizer["Analysis.MoneyFlow.Saved"], saved, MoneyFlowNodeKind.Saved, shareBase));
        }

        return model;
    }

    private static string MonthLabel(CategoryInsightsDto insights)
    {
        return insights.MonthNumber is >= 1 and <= 12 && insights.Year > 0
            ? AnalysisText.MonthYear(insights.Year, insights.MonthNumber)
            : insights.MonthName;
    }

    private static decimal Share(decimal amount, decimal shareBase)
    {
        return shareBase > 0m ? Math.Round(amount / shareBase * 100m, 1) : 0m;
    }

    private static MoneyFlowNodeViewModel Node(string key, string label, decimal amount, MoneyFlowNodeKind kind,
        int column, decimal? sharePercent)
    {
        return new MoneyFlowNodeViewModel
        {
            Key = key,
            Label = label,
            Amount = amount,
            Kind = kind,
            Column = column,
            SharePercent = sharePercent
        };
    }

    private static MoneyFlowLinkViewModel Link(string from, string to, decimal amount)
    {
        return new MoneyFlowLinkViewModel { From = from, To = to, Amount = amount };
    }

    private static MoneyFlowRowViewModel Row(string name, decimal amount, MoneyFlowNodeKind kind, decimal shareBase)
    {
        return new MoneyFlowRowViewModel
        {
            Name = name,
            Amount = amount,
            SharePercent = Share(amount, shareBase),
            Kind = kind
        };
    }
}
