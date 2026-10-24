namespace PersonalFinance.ApiService.Features.Research;

/// <summary>
/// The public sources the Research datasets are built from, so a refresh can leave out one it cannot reach.
/// </summary>
[Flags]
public enum ResearchSources
{
    /// <summary>No source.</summary>
    None = 0,

    /// <summary>SEC EDGAR: the Sectors and funds datasets.</summary>
    SecEdgar = 1,

    /// <summary>The World Bank: the World dataset.</summary>
    WorldBank = 2,

    /// <summary>
    /// US government agencies (the Bureau of Labor Statistics, the Fed Board and the Treasury): the macro dataset. Its
    /// requests carry the same contact as SEC's.
    /// </summary>
    UsAgencies = 4,

    /// <summary>Every source.</summary>
    All = SecEdgar | WorldBank | UsAgencies,
}
