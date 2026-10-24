namespace PersonalFinance.Data.Entities;

/// <summary>
/// One Research dataset (Sectors, and later ETFs and World) as last built from its public source. It is not
/// user-owned, so it is not an <see cref="AuditableEntity"/> and is never soft-deleted: a refresh replaces the row
/// wholesale, and only when the build succeeded, so a failed refresh keeps the previous snapshot.
/// </summary>
public class ResearchSnapshot
{
    public int Id { get; set; }

    /// <summary>
    /// The dataset's key, e.g. <c>sectors</c>; one row per dataset.
    /// </summary>
    public string Dataset { get; set; } = string.Empty;

    /// <summary>
    /// The first period the snapshot covers, e.g. <c>2011</c> (a year) or <c>2019Q4</c> (a quarter).
    /// </summary>
    public string FirstPeriod { get; set; } = string.Empty;

    /// <summary>
    /// The latest period the snapshot covers.
    /// </summary>
    public string LastPeriod { get; set; } = string.Empty;

    /// <summary>
    /// The snapshot as JSON, in the shape of the dataset's Shared DTO.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// The date the source data is as of (shown on the page as "Data as of").
    /// </summary>
    public DateOnly SourceAsOf { get; set; }

    /// <summary>
    /// When the snapshot was built (UTC).
    /// </summary>
    public DateTime BuiltAt { get; set; }
}
