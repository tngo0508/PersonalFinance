using System.Globalization;
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

    private const string IncomeKey = "income";
    private const string ShortfallKey = "shortfall";
    private const string SpendingKey = "spending";
    private const string UncategorizedKey = "uncategorized";
    private const string OtherKey = "other";
    private const string SavedKey = "saved";

    /// <summary>
    ///     Builds the model, or returns <c>null</c> when there are no insights or the month has neither income nor
    ///     expenses.
    /// </summary>
    public static MoneyFlowViewModel? Build(CategoryInsightsDto? insights)
    {
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
            MonthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(insights.MonthNumber),
            Income = income,
            Expenses = expenses,
            Shortfall = shortfall,
            Saved = saved,
            ShareBase = shareBase
        };

        if (income > 0m)
        {
            model.Nodes.Add(Node(IncomeKey, "Income", income, MoneyFlowNodeKind.Income, 0, null));
        }

        if (shortfall > 0m)
        {
            model.Nodes.Add(Node(ShortfallKey, "Shortfall", shortfall, MoneyFlowNodeKind.Shortfall, 0, null));
            model.Nodes.Add(Node(SpendingKey, "Spending", expenses, MoneyFlowNodeKind.Spending, 1, null));
        }

        var rightHand = new List<MoneyFlowNodeViewModel>();
        decimal other = 0m;
        for (int i = 0; i < categories.Count; i++)
        {
            (string name, decimal amount) = categories[i];
            if (amount < totalFlow * OtherThreshold)
            {
                other += amount;
                continue;
            }

            rightHand.Add(Node($"category-{i}", name, amount, MoneyFlowNodeKind.Category, rightColumn,
                Share(amount, shareBase)));
        }

        if (uncategorized > 0m)
        {
            rightHand.Add(Node(UncategorizedKey, "Uncategorized", uncategorized, MoneyFlowNodeKind.Uncategorized,
                rightColumn, Share(uncategorized, shareBase)));
        }

        if (other > 0m)
        {
            rightHand.Add(Node(OtherKey, "Other", other, MoneyFlowNodeKind.Other, rightColumn, Share(other, shareBase)));
        }

        if (saved > 0m)
        {
            rightHand.Add(Node(SavedKey, "Saved", saved, MoneyFlowNodeKind.Saved, rightColumn, Share(saved, shareBase)));
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

        model.TableRows.AddRange(categories.Select(c => Row(c.Name, c.Amount, MoneyFlowNodeKind.Category, shareBase)));
        if (uncategorized > 0m)
        {
            model.TableRows.Add(Row("Uncategorized", uncategorized, MoneyFlowNodeKind.Uncategorized, shareBase));
        }

        if (saved > 0m)
        {
            model.TableRows.Add(Row("Saved", saved, MoneyFlowNodeKind.Saved, shareBase));
        }

        return model;
    }

    private static string MonthLabel(CategoryInsightsDto insights)
    {
        return insights.MonthNumber is >= 1 and <= 12 && insights.Year > 0
            ? new DateTime(insights.Year, insights.MonthNumber, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture)
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
