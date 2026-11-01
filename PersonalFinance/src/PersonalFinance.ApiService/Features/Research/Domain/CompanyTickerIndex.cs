using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// SEC's company ticker list, searchable for the Research company lookup: suggestions by ticker or name, and the
/// company behind a
/// ticker. Fund tickers (mutual fund and ETF share classes) are known so a lookup can say a ticker is a fund.
/// </summary>
internal sealed class CompanyTickerIndex
{
    /// <summary>The most suggestions returned for one query.</summary>
    public const int MaxSuggestions = 8;

    private readonly IReadOnlyList<SecCompanyListing> _listings;
    private readonly Dictionary<string, SecCompanyListing> _byTicker;
    private readonly HashSet<string> _fundTickers;

    /// <summary>An index over <paramref name="listings"/>, knowing <paramref name="fundTickers"/> as funds.</summary>
    public CompanyTickerIndex(IEnumerable<SecCompanyListing> listings, IEnumerable<string> fundTickers)
    {
        _listings = listings
            .Where(l => !string.IsNullOrWhiteSpace(l.Ticker))
            .Select(l => l with { Ticker = l.Ticker.Trim().ToUpperInvariant() })
            .ToList();
        _byTicker = new Dictionary<string, SecCompanyListing>(StringComparer.Ordinal);
        foreach (SecCompanyListing listing in _listings)
        {
            _byTicker.TryAdd(listing.Ticker, listing); // SEC's first row for a ticker wins.
        }

        _fundTickers = new HashSet<string>(fundTickers.Select(t => t.Trim().ToUpperInvariant()), StringComparer.Ordinal);
    }

    /// <summary>The company listed under <paramref name="ticker"/> (as SEC lists it), or null.</summary>
    public SecCompanyListing? Find(string ticker) => _byTicker.GetValueOrDefault(ticker);

    /// <summary>True when <paramref name="ticker"/> is a fund share class.</summary>
    public bool IsFund(string ticker) => _fundTickers.Contains(ticker);

    /// <summary>
    /// Up to <see cref="MaxSuggestions"/> listings matching <paramref name="query"/>, case-insensitively: the exact
    /// ticker first, then tickers starting with the query, then names with a word starting with it; alphabetical by
    /// ticker within each group. A dot in the query matches SEC's dash (<c>BRK.B</c> finds <c>BRK-B</c>).
    /// </summary>
    public List<CompanySuggestionDto> Suggest(string query)
    {
        string trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        string ticker = trimmed.ToUpperInvariant().Replace('.', '-');
        return _listings
            .Select(l => (Listing: l, Rank: Rank(l, ticker, trimmed)))
            .Where(m => m.Rank is not null)
            .OrderBy(m => m.Rank)
            .ThenBy(m => m.Listing.Ticker, StringComparer.Ordinal)
            .Take(MaxSuggestions)
            .Select(m => new CompanySuggestionDto
            {
                Ticker = m.Listing.Ticker,
                Name = SectorsClassifier.CompanyName(m.Listing.Name),
                Exchange = m.Listing.Exchange,
                IsFund = IsFund(m.Listing.Ticker),
            })
            .ToList();
    }

    private static int? Rank(SecCompanyListing listing, string ticker, string query)
    {
        if (string.Equals(listing.Ticker, ticker, StringComparison.Ordinal))
        {
            return 0;
        }

        if (listing.Ticker.StartsWith(ticker, StringComparison.Ordinal))
        {
            return 1;
        }

        return NameHasWordStartingWith(listing.Name, query) ? 2 : null;
    }

    private static bool NameHasWordStartingWith(string name, string query)
    {
        for (int index = name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
             index >= 0;
             index = name.IndexOf(query, index + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (index == 0 || !char.IsLetterOrDigit(name[index - 1]))
            {
                return true;
            }
        }

        return false;
    }
}
