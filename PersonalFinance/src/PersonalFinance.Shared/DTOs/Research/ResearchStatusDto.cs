namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// What each stored Research dataset was built from and when, without the datasets themselves.
/// </summary>
public sealed class ResearchStatusDto
{
    /// <summary>One entry per stored dataset, ordered by dataset name; empty when nothing has loaded.</summary>
    public List<ResearchDatasetStatusDto> Datasets { get; set; } = [];
}

/// <summary>
/// One stored Research dataset's freshness.
/// </summary>
public sealed class ResearchDatasetStatusDto
{
    /// <summary>The dataset's name, e.g. <c>macro</c>.</summary>
    public string Dataset { get; set; } = string.Empty;

    /// <summary>The date the source data is as of.</summary>
    public DateOnly SourceAsOf { get; set; }

    /// <summary>When the Research refresh job last built the dataset (UTC).</summary>
    public DateTime BuiltAt { get; set; }
}
