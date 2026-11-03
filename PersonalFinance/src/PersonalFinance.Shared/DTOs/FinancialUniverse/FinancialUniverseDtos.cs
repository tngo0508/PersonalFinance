using System.Text.Json.Serialization;
using PersonalFinance.Shared.DTOs.Currency;
using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Shared.DTOs.FinancialUniverse;

/// <summary>
/// The Financial Universe for one month (spec #617): the nodes and edges derived from the Account's budget months,
/// Savings Goals, Net Worth parameters and Investment Scenarios. Every amount is in <see cref="CurrencyCode"/>, the
/// Reporting Currency (ADR 0016); a node keeps its native amount beside it. Nothing here is authored.
/// </summary>
public sealed class FinancialUniverseGraphDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    /// <summary>The ISO 4217 code every <see cref="FinancialUniverseNodeDto.Amount"/> is in: the Reporting Currency.</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>The rates that converted amounts in other currencies, for the page's caption; null when none was needed.</summary>
    public ConversionDto? Conversion { get; set; }

    /// <summary>The budget source the income and expense nodes came from.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BudgetDataSource BudgetSource { get; set; }

    /// <summary>Nodes in rank order, then display order within a rank.</summary>
    public List<FinancialUniverseNodeDto> Nodes { get; set; } = [];

    /// <summary>Edges in the order of their source nodes, then their target nodes.</summary>
    public List<FinancialUniverseEdgeDto> Edges { get; set; } = [];
}

/// <summary>One node of the Financial Universe.</summary>
public sealed class FinancialUniverseNodeDto
{
    /// <summary>Stable within the graph (<c>income</c>, <c>expense-2</c>, <c>goal-14</c>); edges refer to it.</summary>
    public string Id { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FinancialUniverseNodeKind Kind { get; set; }

    /// <summary>
    /// The user's own name for a category, goal or scenario, never translated; null for a node the app names (Income,
    /// Other, Savings, Shortfall, Net Worth, a Placeholder), whose label the Web layer localizes.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The node's amount in the Reporting Currency: a category's actual, a goal's saved amount, the Net Worth starting
    /// balance, a scenario's starting amount; null for a Placeholder, or when no rate could convert it.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// A second figure in the Reporting Currency: a goal's target, the Net Worth annual savings, a scenario's monthly
    /// contribution; null otherwise.
    /// </summary>
    public decimal? SecondaryAmount { get; set; }

    /// <summary>The ISO 4217 code of <see cref="Amount"/> and <see cref="SecondaryAmount"/>: the Reporting Currency.</summary>
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary><see cref="Amount"/> as stored, in <see cref="NativeCurrencyCode"/>; null when the source keeps no native figure.</summary>
    public decimal? NativeAmount { get; set; }

    /// <summary>The ISO 4217 code of <see cref="NativeAmount"/>.</summary>
    public string? NativeCurrencyCode { get; set; }

    /// <summary>The id of the owning entity (a goal, a scenario); null for derived nodes and budget categories.</summary>
    public int? SourceId { get; set; }

    /// <summary>The feature page this node opens.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FinancialUniverseLinkTarget LinkTarget { get; set; }

    /// <summary>For a Placeholder, the kind it stands in for; null otherwise.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FinancialUniverseNodeKind? PlaceholderFor { get; set; }

    /// <summary>
    /// An expense category's colour slot, in display order and capped as Money Flow caps it; null for every other node.
    /// </summary>
    public int? ColorSlot { get; set; }
}

/// <summary>One edge of the Financial Universe, from <see cref="SourceId"/> to <see cref="TargetId"/>.</summary>
public sealed class FinancialUniverseEdgeDto
{
    public string SourceId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// The month's amount along the edge in the Reporting Currency (an actual, a contribution total, which may be 0);
    /// null for a relationship with no amount (Savings to Net Worth, Net Worth to a scenario, edges to a Placeholder).
    /// </summary>
    public decimal? Weight { get; set; }
}
