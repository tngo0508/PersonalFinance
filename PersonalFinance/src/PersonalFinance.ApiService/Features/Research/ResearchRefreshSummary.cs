namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// What a Research refresh did: the datasets it rebuilt, the ones whose build failed and the ones whose source it left
/// out (both of which kept their previous snapshot).
/// </summary>
public sealed class ResearchRefreshSummary
{
    /// <summary>The datasets rebuilt and stored.</summary>
    public List<string> Built { get; } = [];

    /// <summary>The datasets whose build failed.</summary>
    public List<string> Failed { get; } = [];

    /// <summary>The datasets not rebuilt because the refresh left out their source; they keep their snapshot.</summary>
    public List<string> Skipped { get; } = [];
}
