namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// One recession the Performance chart shades, from configuration (<c>Research:Recessions</c>), so an entry can be added
/// without a code change. Months are inclusive, written <c>yyyy-MM</c>.
/// </summary>
internal sealed record RecessionPeriod
{
    /// <summary>The first month, e.g. <c>2020-02</c>.</summary>
    public string Start { get; init; } = string.Empty;

    /// <summary>The last month, e.g. <c>2020-04</c>.</summary>
    public string End { get; init; } = string.Empty;

    /// <summary>The band's label, e.g. "2020 recession".</summary>
    public string Label { get; init; } = string.Empty;
}
