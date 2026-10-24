namespace PersonalFinance.Shared.DTOs.Research;

/// <summary>
/// The 503 body of a Research endpoint whose dataset has never loaded.
/// </summary>
public sealed class ResearchUnavailableDto
{
    /// <summary>Why the dataset is unavailable, safe to show or log.</summary>
    public string Reason { get; set; } = string.Empty;
}
