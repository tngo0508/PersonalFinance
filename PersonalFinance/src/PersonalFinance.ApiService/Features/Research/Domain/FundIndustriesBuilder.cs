using PersonalFinance.Shared.DTOs.Research;

namespace PersonalFinance.ApiService.Features.Research.Domain;

/// <summary>
/// Builds the fund industries snapshot from each fund's latest report, over every holding (securities-lending cash
/// collateral left out):
/// <list type="bullet">
/// <item>a stock whose issuer matches an SEC filer with a SIC code goes to that filer's SEC industry office and SIC
/// industry (<see cref="SectorsClassifier"/>); each industry keeps its <see cref="CompaniesPerIndustry"/> largest
/// companies, while its share sums them all;</item>
/// <item>any other stock is "not classified by the SEC" and goes to its issuer's country, keeping the
/// <see cref="IssuersPerCountry"/> largest issuers;</item>
/// <item>shares of a fund are not a company's stock, so they count as cash and other: a holding filed with a fund issuer
/// type, or a stock matched to no SEC filer whose name has the word FUND or ETF (VNQ files its 14.5% holding of
/// Vanguard Real Estate II Index Fund as a corporate stock);</item>
/// <item>debt goes to its issuer type;</item>
/// <item>the rest of the fund is cash and other.</item>
/// </list>
/// </summary>
internal static class FundIndustriesBuilder
{
    /// <summary>How many of an industry's largest companies each fund lists.</summary>
    public const int CompaniesPerIndustry = 10;

    /// <summary>How many of a country's largest unclassified issuers each fund lists.</summary>
    public const int IssuersPerCountry = 10;

    private static readonly HashSet<string> StockCategories = new(StringComparer.Ordinal) { "EC", "EP" };

    private static readonly HashSet<string> DebtCategories = new(StringComparer.Ordinal) { "DBT", "LON" };

    private static readonly HashSet<string> FundIssuers = new(StringComparer.Ordinal) { "RF", "PF" };

    private static readonly Dictionary<string, string> IssuerTypes = new(StringComparer.Ordinal)
    {
        ["CORP"] = "Corporate",
        ["UST"] = "US Treasury",
        ["USGA"] = "US government agency",
        ["USGSE"] = "US government-sponsored enterprise",
        ["MUN"] = "Municipal",
        ["NUSS"] = "Non-US government",
        ["PF"] = "Private fund",
        ["RF"] = "Registered fund",
    };

    private const string OtherIssuers = "Other issuers";

    /// <summary>
    /// The snapshot of <paramref name="reports"/>, in their order.
    /// </summary>
    /// <param name="reports">Each fund's latest report.</param>
    /// <param name="cikOf">The SEC filer a holding's issuer is (by its name and country), or null.</param>
    /// <param name="filers">The submissions of the filers matched, by CIK.</param>
    /// <param name="signature">What the snapshot is built with (<see cref="FundIndustriesSnapshotDto.Signature"/>).</param>
    public static FundIndustriesSnapshotDto Build(IReadOnlyList<LatestFundReport> reports,
        Func<NportHolding, long?> cikOf, IReadOnlyDictionary<long, SecFiler> filers, string signature)
    {
        var classifier = new SectorsClassifier(filers);
        List<FundTotals> totals = reports.Select(r => TotalsOf(r, cikOf, filers, classifier)).ToList();

        List<SectorsOffice> offices = totals.SelectMany(t => t.Industries.Keys).Select(k => k.Office).Distinct()
            .OrderBy(o => o.Order).ThenBy(o => o.Name, StringComparer.Ordinal).ToList();
        List<IndustryKey> industries = totals.SelectMany(t => t.Industries.Keys).Distinct()
            .OrderBy(k => k.Office.Order).ThenBy(k => k.Office.Name, StringComparer.Ordinal)
            .ThenBy(k => k.SicCode, StringComparer.Ordinal).ToList();
        Dictionary<IndustryKey, int> industryIndex = industries.Select((k, i) => (k, i)).ToDictionary(p => p.k, p => p.i);

        List<List<(long Cik, IndustryKey Industry, decimal Percent)>> listed = totals.Select(ListedCompanies).ToList();
        List<(long Cik, string Name, string? Ticker, IndustryKey Industry)> companies = listed
            .SelectMany(l => l)
            .Select(c => c.Cik)
            .Distinct()
            .Select(cik => (Cik: cik, Name: SectorsClassifier.CompanyName(filers[cik].Name), filers[cik].Ticker,
                Industry: IndustryOf(classifier.Place(cik))))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Cik)
            .ToList();
        Dictionary<long, int> companyIndex = companies.Select((c, i) => (c.Cik, i)).ToDictionary(p => p.Cik, p => p.i);

        return new FundIndustriesSnapshotDto
        {
            AsOf = reports.Count == 0 ? default : reports.Max(r => r.Report.ReportDate),
            Signature = signature,
            Offices = offices.Select(o => o.Name).ToList(),
            Industries = industries.Select(k => new SectorsIndustryDto
            {
                SicCode = k.SicCode,
                Name = k.Name,
                Office = offices.IndexOf(k.Office),
                Technology = SectorsClassifier.IsTechnology(k.Office.Name, k.SicCode),
            }).ToList(),
            Companies = companies.Select(c => new FundIndustryCompanyDto
            {
                Name = c.Name,
                Ticker = c.Ticker,
                Industry = industryIndex[c.Industry],
            }).ToList(),
            Funds = totals.Select((t, i) => FundOf(t, listed[i], industryIndex, companyIndex)).ToList(),
        };
    }

    /// <summary>One fund's totals before rounding: by industry (and company), unclassified country, and debt type.</summary>
    private static FundTotals TotalsOf(LatestFundReport latest, Func<NportHolding, long?> cikOf,
        IReadOnlyDictionary<long, SecFiler> filers, SectorsClassifier classifier)
    {
        var totals = new FundTotals(latest);
        foreach (NportHolding holding in latest.Report.Holdings.Where(h => !h.IsCashCollateral))
        {
            if (StockCategories.Contains(holding.AssetCategory)
                && !(holding.IssuerCategory is { } issuerType && FundIssuers.Contains(issuerType)))
            {
                if (cikOf(holding) is { } cik && filers.TryGetValue(cik, out SecFiler? filer) && filer.SicCode is not null)
                {
                    IndustryKey industry = IndustryOf(classifier.Place(cik));
                    if (!totals.Industries.TryGetValue(industry, out Dictionary<long, decimal>? byCompany))
                    {
                        totals.Industries[industry] = byCompany = [];
                    }

                    byCompany[cik] = byCompany.GetValueOrDefault(cik) + holding.Percent;
                }
                else if (!IsFundByName(holding.Name))
                {
                    if (!totals.Unclassified.TryGetValue(holding.Country ?? string.Empty,
                            out Dictionary<string, (string Name, decimal Percent)>? byIssuer))
                    {
                        totals.Unclassified[holding.Country ?? string.Empty] = byIssuer = new(StringComparer.Ordinal);
                    }

                    // One issuer's share classes and lines merge under one name; the first spelling is kept.
                    string key = IssuerName.Of(holding.Name).Exact;
                    (string Name, decimal Percent) issuer = byIssuer.GetValueOrDefault(key, (holding.Name, 0m));
                    byIssuer[key] = (issuer.Name, issuer.Percent + holding.Percent);
                }
            }
            else if (DebtCategories.Contains(holding.AssetCategory)
                     || holding.AssetCategory.StartsWith("ABS", StringComparison.Ordinal))
            {
                string type = holding.IssuerCategory is { } code ? IssuerTypes.GetValueOrDefault(code, OtherIssuers) : OtherIssuers;
                totals.Bonds[type] = totals.Bonds.GetValueOrDefault(type) + holding.Percent;
            }
        }

        return totals;
    }

    /// <summary>Whether a holding's name says it is a fund: the word FUND or ETF in it.</summary>
    private static bool IsFundByName(string name) =>
        name.Split([' ', ',', '.', '-', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Any(w => w.Equals("FUND", StringComparison.OrdinalIgnoreCase) || w.Equals("ETF", StringComparison.OrdinalIgnoreCase));

    /// <summary>The companies a fund lists: each industry's largest with a positive share, largest first.</summary>
    private static List<(long Cik, IndustryKey Industry, decimal Percent)> ListedCompanies(FundTotals totals) =>
        totals.Industries
            .SelectMany(industry => industry.Value
                .Where(c => FundsSnapshotBuilder.Round(c.Value) > 0m)
                .OrderByDescending(c => c.Value)
                .ThenBy(c => c.Key)
                .Take(CompaniesPerIndustry)
                .Select(c => (c.Key, industry.Key, c.Value)))
            .ToList();

    private static FundIndustriesDto FundOf(FundTotals totals, List<(long Cik, IndustryKey Industry, decimal Percent)> listed,
        Dictionary<IndustryKey, int> industryIndex, Dictionary<long, int> companyIndex)
    {
        List<FundIndustryShareDto> industries = totals.Industries
            .Select(i => new FundIndustryShareDto
            {
                Industry = industryIndex[i.Key],
                Percent = FundsSnapshotBuilder.Round(i.Value.Values.Sum()),
                CompanyCount = i.Value.Count,
            })
            .Where(i => i.Percent != 0m)
            .OrderByDescending(i => i.Percent)
            .ThenBy(i => i.Industry)
            .ToList();
        List<FundUnclassifiedCountryDto> unclassified = totals.Unclassified
            .Select(c => new FundUnclassifiedCountryDto
            {
                Country = c.Key.Length == 0 ? null : c.Key,
                Percent = FundsSnapshotBuilder.Round(c.Value.Values.Sum(i => i.Percent)),
                IssuerCount = c.Value.Count,
                Issuers = c.Value.Values
                    .Where(i => FundsSnapshotBuilder.Round(i.Percent) > 0m)
                    .OrderByDescending(i => i.Percent)
                    .ThenBy(i => i.Name, StringComparer.Ordinal)
                    .Take(IssuersPerCountry)
                    .Select(i => new FundNamedShareDto
                    {
                        Name = SectorsClassifier.CompanyName(i.Name),
                        Percent = FundsSnapshotBuilder.Round(i.Percent),
                    })
                    .ToList(),
            })
            .Where(c => c.Percent != 0m)
            .OrderByDescending(c => c.Percent)
            .ThenBy(c => c.Country, StringComparer.Ordinal)
            .ToList();
        List<FundNamedShareDto> bonds = totals.Bonds
            .Select(b => new FundNamedShareDto { Name = b.Key, Percent = FundsSnapshotBuilder.Round(b.Value) })
            .Where(b => b.Percent != 0m)
            .OrderByDescending(b => b.Percent)
            .ThenBy(b => b.Name, StringComparer.Ordinal)
            .ToList();
        decimal placed = industries.Sum(i => i.Percent) + unclassified.Sum(c => c.Percent) + bonds.Sum(b => b.Percent);
        return new FundIndustriesDto
        {
            Ticker = totals.Latest.Ticker,
            ReportDate = totals.Latest.Report.ReportDate,
            Accession = totals.Latest.Accession,
            Industries = industries,
            Companies = listed
                .OrderBy(c => industryIndex[c.Industry])
                .ThenByDescending(c => c.Percent)
                .Select(c => new FundCompanyShareDto { Company = companyIndex[c.Cik], Percent = FundsSnapshotBuilder.Round(c.Percent) })
                .ToList(),
            Unclassified = unclassified,
            Bonds = bonds,
            // A fund over 100% in its holdings (derivatives, borrowing) has nothing left over.
            CashAndOther = Math.Max(0m, FundsSnapshotBuilder.Round(100m - placed)),
        };
    }

    private static IndustryKey IndustryOf(SectorsPlacement placement) =>
        new(placement.Office, placement.SicCode, placement.Industry);

    /// <summary>A SIC industry within an office; <see cref="Name"/> is the SIC description for display.</summary>
    private sealed record IndustryKey(SectorsOffice Office, string? SicCode, string Name);

    private sealed class FundTotals(LatestFundReport latest)
    {
        public LatestFundReport Latest { get; } = latest;

        public Dictionary<IndustryKey, Dictionary<long, decimal>> Industries { get; } = [];

        /// <summary>By country code (empty when the fund gives none), then issuer name key.</summary>
        public Dictionary<string, Dictionary<string, (string Name, decimal Percent)>> Unclassified { get; } =
            new(StringComparer.Ordinal);

        public Dictionary<string, decimal> Bonds { get; } = new(StringComparer.Ordinal);
    }
}
