namespace PersonalFinance.Web.Models.Networth;

/// <summary>
///     The FI Breakdown on the Net Worth page (GLOSSARY: FI Breakdown): one card per calculated figure, showing its
///     value, the inputs it comes from and its formula in the user's own numbers. Every string is ready to display,
///     so the page script only places text.
/// </summary>
public sealed class FiBreakdownViewModel
{
    public List<FiBreakdownCardViewModel> Cards { get; set; } = new();
}

public sealed class FiBreakdownCardViewModel
{
    /// <summary>A stable key, e.g. "fi-target", used for element ids.</summary>
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    /// <summary>The figures this one is calculated from.</summary>
    public List<FiBreakdownInputViewModel> Inputs { get; set; } = new();

    /// <summary>The calculation with the user's numbers; <c>null</c> when the figure can't be calculated.</summary>
    public string? Formula { get; set; }

    /// <summary>One line per return scenario (FI age only).</summary>
    public List<string> Scenarios { get; set; } = new();

    /// <summary>What to enter to see the figure, when it can't be calculated.</summary>
    public string? Note { get; set; }
}

public sealed class FiBreakdownInputViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
