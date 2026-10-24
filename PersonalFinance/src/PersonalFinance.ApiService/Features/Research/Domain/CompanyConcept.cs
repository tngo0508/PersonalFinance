namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// One XBRL concept a Company Profile reads.
/// </summary>
/// <param name="Figure">The figure it gives.</param>
/// <param name="Taxonomy">The taxonomy, <c>us-gaap</c> or <c>dei</c>.</param>
/// <param name="Name">The concept's name, e.g. <c>Revenues</c>.</param>
/// <param name="Unit">The only unit read, e.g. <c>USD</c>; values in other currencies are never converted.</param>
/// <param name="Timing">How the value is dated.</param>
internal sealed record CompanyConcept(CompanyFigure Figure, string Taxonomy, string Name, string Unit,
    CompanyFactTiming Timing)
{
    /// <summary>The concept's key, e.g. <c>us-gaap:Revenues</c>.</summary>
    public string Key => $"{Taxonomy}:{Name}";
}
