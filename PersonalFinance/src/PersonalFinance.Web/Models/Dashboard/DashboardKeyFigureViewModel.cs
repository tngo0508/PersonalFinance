namespace PersonalFinance.Web.Models.Dashboard;

/// <summary>
/// One key-figure tile on the Dashboard (rendered by <c>Views/Dashboard/_KeyFigureTile.cshtml</c>).
/// Values arrive pre-formatted; the tile only lays them out.
/// </summary>
public class DashboardKeyFigureViewModel
{
    /// <summary>Short label above the value, e.g. "Net Worth".</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Formatted value, or null when the figure is unavailable.</summary>
    public string? Value { get; init; }

    /// <summary>Optional supporting line beneath the value.</summary>
    public string? Caption { get; init; }

    /// <summary>Text shown in place of the value when <see cref="Value"/> is null, already in the request's Language.</summary>
    public string UnavailableText { get; init; } = string.Empty;

    /// <summary>Whether the value is money or a number that should use tabular monospace figures.</summary>
    public bool IsNumeric { get; init; } = true;

    /// <summary>Lucide icon name.</summary>
    public string Icon { get; init; } = "circle";

    /// <summary>MVC controller the tile links to, where the figure can be changed.</summary>
    public string Controller { get; init; } = string.Empty;

    /// <summary>MVC action the tile links to.</summary>
    public string Action { get; init; } = "Index";
}
