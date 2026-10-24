namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// The XBRL facts a company reported to SEC (<c>companyfacts</c>), trimmed to the concepts of
/// <see cref="CompanyProfileConcepts"/>.
/// </summary>
/// <param name="Facts">The facts of each concept, keyed by <see cref="CompanyConcept.Key"/>.</param>
/// <param name="HasIfrsFacts">True when the company also reports under the IFRS taxonomy (<c>ifrs-full</c>).</param>
internal sealed record SecCompanyFacts(IReadOnlyDictionary<string, IReadOnlyList<SecFact>> Facts, bool HasIfrsFacts)
{
    /// <summary>The facts of <paramref name="concept"/>, or none.</summary>
    public IReadOnlyList<SecFact> Of(CompanyConcept concept) =>
        Facts.TryGetValue(concept.Key, out IReadOnlyList<SecFact>? facts) ? facts : [];
}
