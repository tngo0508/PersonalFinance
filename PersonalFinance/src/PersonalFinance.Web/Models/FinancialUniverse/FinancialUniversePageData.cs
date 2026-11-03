using PersonalFinance.Shared.Enums;

namespace PersonalFinance.Web.Models.FinancialUniverse;

/// <summary>
/// What the page's JavaScript reads from the <c>fuPageData</c> JSON island (spec #617): the labelled nodes with their
/// URLs, and the edges. Built in the view, where the URL helper lives; slice 2's renderer only draws it.
/// </summary>
public sealed class FinancialUniversePageData
{
    public int Year { get; init; }

    public int Month { get; init; }

    public string MonthLabel { get; init; } = string.Empty;

    public string CurrencyCode { get; init; } = string.Empty;

    public List<FinancialUniversePageNode> Nodes { get; init; } = [];

    public List<FinancialUniverseEdgeViewModel> Edges { get; init; } = [];

    /// <summary>The page data for <paramref name="model"/>, with each node's URL from <paramref name="hrefFor"/>.</summary>
    public static FinancialUniversePageData From(FinancialUniverseViewModel model,
        Func<FinancialUniverseLinkTarget, string?> hrefFor)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(hrefFor);
        return new FinancialUniversePageData
        {
            Year = model.Year,
            Month = model.Month,
            MonthLabel = model.MonthLabel,
            CurrencyCode = model.CurrencyCode,
            Nodes = model.Nodes.Select(n => new FinancialUniversePageNode
            {
                Id = n.Id,
                Kind = n.Kind.ToString(),
                Label = n.Label,
                AmountText = n.AmountText,
                DetailText = n.DetailText,
                Amount = n.Amount,
                Href = hrefFor(n.LinkTarget) ?? string.Empty,
                IsPlaceholder = n.IsPlaceholder,
                ColorSlot = n.ColorSlot
            }).ToList(),
            Edges = model.Edges
        };
    }
}

/// <summary>One node as the JavaScript reads it.</summary>
public sealed class FinancialUniversePageNode
{
    public string Id { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string AmountText { get; init; } = string.Empty;

    public string DetailText { get; init; } = string.Empty;

    public decimal? Amount { get; init; }

    public string Href { get; init; } = string.Empty;

    public bool IsPlaceholder { get; init; }

    public int? ColorSlot { get; init; }
}
