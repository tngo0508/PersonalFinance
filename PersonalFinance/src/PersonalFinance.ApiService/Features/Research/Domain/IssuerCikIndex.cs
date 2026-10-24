namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Finds the SEC filer a Form N-PORT holding's issuer is, by name, against SEC's company list and the configured
/// overrides (ADR 0010: no security identifier is used). Only a name that leads to exactly one filer counts, tried in
/// turn:
/// <list type="number">
/// <item>an override, by its name's <see cref="IssuerName.Base"/>;</item>
/// <item>the same name (<see cref="IssuerName.Exact"/>);</item>
/// <item>the same name before its legal form (<see cref="IssuerName.Base"/>). For an issuer organized outside the US,
/// the two names' legal forms must agree, or one must have none, so <c>EQT AB</c> is never <c>EQT Corp</c>;</item>
/// <item>for an issuer organized in the US (or not said), the same words in any order, for SEC's surname-first names
/// (<c>SCHWAB CHARLES CORP</c>).</item>
/// </list>
/// Depositary-receipt registrants (<c>…/ADR</c>) are left out: they are not the company and SEC gives them no industry.
/// </summary>
internal sealed class IssuerCikIndex
{
    private const string UnitedStates = "US";

    private readonly Dictionary<string, long> _overrides;
    private readonly Dictionary<string, HashSet<long>> _byExact = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(long Cik, IReadOnlySet<string> Forms)>> _byBase = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<long>> _byWords = new(StringComparer.Ordinal);

    /// <summary>An index over SEC's company list (one row per ticker) and the overrides.</summary>
    public IssuerCikIndex(IEnumerable<SecCompanyListing> listings, IEnumerable<IssuerCikOverride> overrides)
    {
        _overrides = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (IssuerCikOverride entry in overrides)
        {
            _overrides[IssuerName.Of(entry.Name).Base] = entry.Cik; // A later entry for the same name wins.
        }

        foreach (SecCompanyListing listing in listings)
        {
            if (listing.Name.Contains("/ADR", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            IssuerName name = IssuerName.Of(listing.Name);
            Add(_byExact, name.Exact, listing.Cik);
            Add(_byWords, name.Words, listing.Cik);
            if (!_byBase.TryGetValue(name.Base, out List<(long, IReadOnlySet<string>)>? entries))
            {
                _byBase[name.Base] = entries = [];
            }

            entries.Add((listing.Cik, name.Forms));
        }
    }

    /// <summary>
    /// The CIK of the filer <paramref name="issuer"/> is, or null when no rule leads to exactly one filer.
    /// </summary>
    /// <param name="issuer">The issuer's name as the fund reports it.</param>
    /// <param name="country">The ISO alpha-2 code of the country where it is organized, or null when not reported.</param>
    public long? Find(string issuer, string? country)
    {
        IssuerName name = IssuerName.Of(issuer);
        if (name.Base.Length == 0)
        {
            return null;
        }

        if (_overrides.TryGetValue(name.Base, out long overridden))
        {
            return overridden;
        }

        if (_byExact.TryGetValue(name.Exact, out HashSet<long>? exact))
        {
            return exact.Count == 1 ? exact.First() : null;
        }

        bool inUs = country is null || string.Equals(country, UnitedStates, StringComparison.OrdinalIgnoreCase);
        if (_byBase.TryGetValue(name.Base, out List<(long Cik, IReadOnlySet<string> Forms)>? entries))
        {
            List<long> agreeing = entries
                .Where(e => inUs || name.Forms.Count == 0 || e.Forms.Count == 0 || e.Forms.SetEquals(name.Forms))
                .Select(e => e.Cik)
                .Distinct()
                .ToList();
            if (agreeing.Count > 0)
            {
                return agreeing.Count == 1 ? agreeing[0] : null;
            }
        }

        return inUs && _byWords.TryGetValue(name.Words, out HashSet<long>? words) && words.Count == 1
            ? words.First()
            : null;
    }

    private static void Add(Dictionary<string, HashSet<long>> index, string key, long cik)
    {
        if (!index.TryGetValue(key, out HashSet<long>? ciks))
        {
            index[key] = ciks = [];
        }

        ciks.Add(cik);
    }
}
