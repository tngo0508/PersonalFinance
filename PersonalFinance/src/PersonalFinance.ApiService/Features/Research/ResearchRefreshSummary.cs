namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// What a Research refresh did: the datasets it rebuilt and the ones whose build failed (and so kept their previous
/// snapshot).
/// </summary>
public sealed class ResearchRefreshSummary
{
    /// <summary>The datasets rebuilt and stored.</summary>
    public List<string> Built { get; } = [];

    /// <summary>The datasets whose build failed.</summary>
    public List<string> Failed { get; } = [];
}
