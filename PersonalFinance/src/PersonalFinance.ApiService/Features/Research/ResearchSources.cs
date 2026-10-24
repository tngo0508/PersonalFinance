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

    /// <summary>Every source.</summary>
    All = SecEdgar | WorldBank,
}
