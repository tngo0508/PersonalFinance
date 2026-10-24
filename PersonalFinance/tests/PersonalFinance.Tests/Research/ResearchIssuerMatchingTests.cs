using System.Globalization;
using System.Text.Json;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// Matching a Form N-PORT holding's issuer to an SEC filer by name (<see cref="IssuerCikIndex"/>), with no security
/// identifier. The recorded cases in <c>FundIndustriesSamples/</c> are SPY's and QQQ's stocks from their 30 June 2026
/// reports (issuer name, country and % of net assets only) and the rows of SEC's company list of 24 October 2026 that
/// share a matching key with any of them; rows sharing none cannot change a match, so leaving them out changes nothing.
/// </summary>
public class ResearchIssuerMatchingTests
{
    private static readonly string SamplesDirectory =
        Path.Combine(RepositoryRoot.Find(), "PersonalFinance", "tests", "PersonalFinance.Tests", "Research", "FundIndustriesSamples");

    private static IssuerCikIndex Index(params (long Cik, string Name)[] companies) =>
        new(companies.Select(c => new SecCompanyListing(c.Cik, $"T{c.Cik}", c.Name, null)), []);

    [Theory]
    [InlineData("Apple Inc", "Apple Inc.")]
    [InlineData("Wells Fargo & Co", "WELLS FARGO & COMPANY/MN")]
    [InlineData("Vertex Pharmaceuticals Inc", "VERTEX PHARMACEUTICALS INC / MA")]
    [InlineData("Charles Schwab Corp/The", "SCHWAB CHARLES CORP")]
    [InlineData("DR Horton Inc", "HORTON D R INC /DE/")]
    [InlineData("JB Hunt Transport Services Inc", "HUNT J B TRANSPORT SERVICES INC")]
    [InlineData("CRH PLC", "CRH PUBLIC LTD CO")]
    [InlineData("NXP Semiconductors NV", "NXP Semiconductors N.V.")]
    public void Find_MatchesTheSameCompanyUnderSecsSpelling(string holding, string secName)
    {
        Assert.Equal(42, Index((42, secName)).Find(holding, "US"));
    }

    [Theory]
    [InlineData("EQT AB", "SE", "EQT Corp")]
    [InlineData("Compass Group PLC", "GB", "Compass, Inc.")]
    [InlineData("SoftBank Corp", "JP", "SOFTBANK GROUP CORP.")]
    public void Find_NeverTakesAForeignIssuerForAUsCompanyOfASimilarName(string holding, string country, string secName)
    {
        Assert.Null(Index((42, secName)).Find(holding, country));
    }

    [Fact]
    public void Find_TakesAForeignIssuerWithTheSameFormOrNone_ButNotSurnameFirst()
    {
        IssuerCikIndex index = Index((1, "Sanofi"), (2, "VODAFONE GROUP PUBLIC LTD CO"), (3, "SMITH A O CORP"));

        Assert.Equal(1, index.Find("Sanofi SA", "FR"));
        Assert.Equal(2, index.Find("Vodafone Group PLC", "GB"));
        Assert.Null(index.Find("A O Smith Corp", "GB"));
        Assert.Equal(3, index.Find("A O Smith Corp", null));
    }

    [Fact]
    public void Find_LeavesOutDepositaryReceiptRegistrants_AndNamesThatLeadToTwoFilers()
    {
        IssuerCikIndex index = Index((1, "Samsung Electronics Co Ltd/ADR"), (2, "TARGET CORP"), (3, "Target Corp /NEW/"));

        Assert.Null(index.Find("Samsung Electronics Co Ltd", "KR"));
        Assert.Null(index.Find("Target Corp", "US"));
    }

    [Fact]
    public void Find_PrefersAnOverride_MatchedWhateverItsPunctuationAndForm()
    {
        var index = new IssuerCikIndex([new SecCompanyListing(1, "XOM", "ExxonMobil Holdings Corp", "NYSE")],
            [new IssuerCikOverride("EXXON MOBIL CORPORATION", 34088)]);

        Assert.Equal(34088, index.Find("Exxon Mobil Corp", "US"));
        Assert.Equal(34088, index.Find("Exxon Mobil Corp.", null));
    }

    [Theory]
    [InlineData("spy-2026-06-30-equities.tsv")]
    [InlineData("qqq-2026-06-30-equities.tsv")]
    public void Find_OnRecordedFilings_MatchesAbout95PercentOfAUsFundsStocksByWeight(string sample)
    {
        IssuerCikIndex index = new(ReadCompanyList(), []);
        List<(string Name, string? Country, decimal Percent)> stocks = File.ReadAllLines(Path.Combine(SamplesDirectory, sample))
            .Skip(1)
            .Select(line => line.Split('\t'))
            .Select(f => (f[0], f[1].Length == 0 ? null : f[1], decimal.Parse(f[2], CultureInfo.InvariantCulture)))
            .ToList();

        decimal matched = stocks.Where(s => index.Find(s.Name, s.Country) is not null).Sum(s => s.Percent);
        decimal share = matched / stocks.Sum(s => s.Percent) * 100m;

        // Measured at 98.9% (SPY) and 99.5% (QQQ) with no overrides; the misses are companies renamed or delisted since.
        Assert.True(share >= 95m, $"{sample}: {share:F2}% of stock weight matched");
    }

    private static List<SecCompanyListing> ReadCompanyList()
    {
        using JsonDocument list = JsonDocument.Parse(File.ReadAllText(Path.Combine(SamplesDirectory,
            "company-tickers-exchange-excerpt.json")));
        return list.RootElement.GetProperty("data").EnumerateArray()
            .Where(row => row[2].ValueKind == JsonValueKind.String)
            .Select(row => new SecCompanyListing(row[0].GetInt64(), row[2].GetString()!, row[1].GetString()!, null))
            .ToList();
    }
}
