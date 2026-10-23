namespace PersonalFinance.Web.Models.Analysis;

/// <summary>
///     The Money Flow card on the Analysis page (GLOSSARY: Money Flow): one month of actual income moving into
///     spending categories and savings, as Sankey nodes and links plus the matching table.
/// </summary>
public sealed class MoneyFlowViewModel
{
    /// <summary>The month shown, e.g. "September 2026".</summary>
    public string MonthLabel { get; set; } = string.Empty;

    /// <summary>The short month name used in the Shortfall note, e.g. "September".</summary>
    public string MonthName { get; set; } = string.Empty;

    public decimal Income { get; set; }
    public decimal Expenses { get; set; }

    /// <summary>The amount by which expenses exceed income; 0 when the month has savings.</summary>
    public decimal Shortfall { get; set; }

    /// <summary>Income left after expenses; 0 in a Shortfall month.</summary>
    public decimal Saved { get; set; }

    /// <summary>What shares are a percentage of: income, or total spending in a Shortfall month.</summary>
    public decimal ShareBase { get; set; }

    public bool HasShortfall => Shortfall > 0m;

    public List<MoneyFlowNodeViewModel> Nodes { get; set; } = new();
    public List<MoneyFlowLinkViewModel> Links { get; set; } = new();

    /// <summary>Every category, ungrouped, then Uncategorized and Saved: the accessible version of the chart.</summary>
    public List<MoneyFlowRowViewModel> TableRows { get; set; } = new();
}

public enum MoneyFlowNodeKind
{
    Income,
    Shortfall,
    Spending,
    Category,
    Uncategorized,
    Other,
    Saved
}

public sealed class MoneyFlowNodeViewModel
{
    /// <summary>A stable key, distinct from the label, so a category named "Saved" can't collide with the Saved node.</summary>
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public MoneyFlowNodeKind Kind { get; set; }

    /// <summary>The Sankey column: 0 for Income and Shortfall, then 1 or 2.</summary>
    public int Column { get; set; }

    /// <summary>Share of <see cref="MoneyFlowViewModel.ShareBase"/>; null for the left-hand and Spending nodes.</summary>
    public decimal? SharePercent { get; set; }

    /// <summary>For a category, its colour slot (0 = largest); null for every other kind of node.</summary>
    public int? ColorSlot { get; set; }
}

public sealed class MoneyFlowLinkViewModel
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class MoneyFlowRowViewModel
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal SharePercent { get; set; }
    public MoneyFlowNodeKind Kind { get; set; }

    /// <summary>The colour slot of the category's band, so the table can show the same swatch.</summary>
    public int? ColorSlot { get; set; }

    /// <summary>True for a category drawn inside the chart's Other band rather than on its own.</summary>
    public bool InOther { get; set; }
}
