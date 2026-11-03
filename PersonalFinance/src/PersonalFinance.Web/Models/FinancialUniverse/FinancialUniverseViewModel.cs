using PersonalFinance.Shared.DTOs.Currency;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.FinancialUniverse;

/// <summary>
/// The Financial Universe page (spec #617): the graph's nodes with their labels fully formed in the request's Language
/// and the Reporting Currency, grouped into the list view's sections, plus the edges. The page's JavaScript reads the
/// same nodes from a JSON island and composes no text of its own.
/// </summary>
public sealed class FinancialUniverseViewModel
{
    public int Year { get; set; }

    public int Month { get; set; }

    /// <summary>The month as the reader writes it ("October 2026").</summary>
    public string MonthLabel { get; set; } = string.Empty;

    /// <summary>The ISO 4217 code every amount is in: the Reporting Currency (ADR 0016).</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The rates behind the converted amounts, for the caption; null when none was needed.</summary>
    public ConversionDto? Conversion { get; set; }

    /// <summary>The budget source the income and expense nodes came from.</summary>
    public BudgetDataSource BudgetSource { get; set; }

    /// <summary>The list view: one section per kind in rank order, only the sections that have a row.</summary>
    public List<FinancialUniverseSectionViewModel> Sections { get; set; } = [];

    /// <summary>Every node, labelled, in the graph's order.</summary>
    public List<FinancialUniverseNodeViewModel> Nodes { get; set; } = [];

    /// <summary>Every edge, with its weight written out where it has one.</summary>
    public List<FinancialUniverseEdgeViewModel> Edges { get; set; } = [];

    /// <summary>The month picker's options, newest first; exactly one is selected.</summary>
    public List<FinancialUniverseMonthOptionViewModel> AvailableMonths { get; set; } = [];
}

/// <summary>One month the picker offers.</summary>
public sealed class FinancialUniverseMonthOptionViewModel
{
    public int Year { get; set; }

    public int Month { get; set; }

    /// <summary>The option's value as the query carries it: <c>yyyy-MM</c>.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>The month as the reader writes it ("October 2026").</summary>
    public string Label { get; set; } = string.Empty;

    public bool IsSelected { get; set; }
}

/// <summary>One section of the list view.</summary>
public sealed class FinancialUniverseSectionViewModel
{
    /// <summary>The kind the section lists (Savings also lists a Shortfall; ExpenseCategory also lists Other).</summary>
    public FinancialUniverseNodeKind Kind { get; set; }

    /// <summary>A CSS-friendly key of <see cref="Kind"/> ("expense-category") for test ids and anchors.</summary>
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public List<FinancialUniverseNodeViewModel> Rows { get; set; } = [];
}

/// <summary>One node, as a row of the list view and as a node of the graph.</summary>
public sealed class FinancialUniverseNodeViewModel
{
    public string Id { get; set; } = string.Empty;

    public FinancialUniverseNodeKind Kind { get; set; }

    /// <summary>The user's own name, or the localized name of a derived node, or a Placeholder's call to action.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The amount in the Reporting Currency, formatted; empty for a Placeholder.</summary>
    public string AmountText { get; set; } = string.Empty;

    /// <summary>A goal's target, the Net Worth annual savings, a scenario's monthly contribution, written out; else empty.</summary>
    public string DetailText { get; set; } = string.Empty;

    /// <summary>The amount in the Reporting Currency, for the graph; null for a Placeholder or an unconvertible amount.</summary>
    public decimal? Amount { get; set; }

    /// <summary>The feature page the node opens.</summary>
    public FinancialUniverseLinkTarget LinkTarget { get; set; }

    /// <summary>"Open Budget Tracker" and the like, in the request's Language.</summary>
    public string LinkText { get; set; } = string.Empty;

    /// <summary>The node's kind as the detail panel names it ("Expense category"), in the request's Language.</summary>
    public string KindText { get; set; } = string.Empty;

    /// <summary>
    /// The amount as stored, formatted in its own currency, when that differs from the Reporting Currency; else empty.
    /// </summary>
    public string NativeAmountText { get; set; } = string.Empty;

    /// <summary>
    /// What the amount covers: the month for the budget nodes, or that it is the latest saved figure (a goal's balance,
    /// Net Worth, a scenario); empty for a Placeholder.
    /// </summary>
    public string PeriodText { get; set; } = string.Empty;

    /// <summary>The name of the feature that owns the node ("Budget Tracker").</summary>
    public string FeatureText { get; set; } = string.Empty;

    public bool IsPlaceholder { get; set; }

    /// <summary>An expense category's colour slot; null for every other node.</summary>
    public int? ColorSlot { get; set; }
}

/// <summary>One edge, as the graph draws it.</summary>
public sealed class FinancialUniverseEdgeViewModel
{
    public string Source { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    /// <summary>The month's amount along the edge; null for a relationship.</summary>
    public decimal? Weight { get; set; }

    /// <summary><see cref="Weight"/> formatted in the Reporting Currency; empty for a relationship.</summary>
    public string WeightText { get; set; } = string.Empty;
}
