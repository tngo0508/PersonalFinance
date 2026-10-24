using System.Text.Json;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Shared.Helpers;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Company Profile's data rules at their edges, on facts built in code: how fiscal years are labelled, when a
/// derived figure is left out, and what a company reporting under IFRS shows. The rules on a full sample are covered
/// by <see cref="CompanyProfileServiceTests"/>.
/// </summary>
public class CompanyProfileBuilderTests
{
    private static readonly SecCompanySubmissions Submissions = new(42, "EXAMPLE RETAIL CO", ["EXR"], ["NYSE"], "5331",
        "RETAIL-VARIETY STORES", "07 Trade & Services", "MN", "0131", []);

    private static SecFact Flow(DateOnly start, DateOnly end, decimal value, int fiscalYear, string accession,
        DateOnly filedOn, string form = "10-K") =>
        new(start, end, value, fiscalYear, "FY", form, filedOn, accession);

    private static SecFact Balance(DateOnly end, decimal value, int fiscalYear, string accession, DateOnly filedOn) =>
        new(null, end, value, fiscalYear, "FY", "10-K", filedOn, accession);

    private static SecCompanyFacts Facts(params (string Key, SecFact Fact)[] facts) =>
        new(
            facts.GroupBy(f => f.Key)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<SecFact>)g.Select(f => f.Fact).ToList()),
            HasIfrsFacts: false);

    private static CompanyProfileDto Build(SecCompanyFacts facts) =>
        CompanyProfileBuilder.Build("EXR", Submissions, facts);

    [Fact]
    public void FiscalYears_AreLabelledAsTheCompanyLabelsThem_EvenWhenTheYearEndsInTheNextCalendarYear()
    {
        // A retailer whose fiscal 2024 ends on 1 February 2025 (53 weeks) and fiscal 2023 on 3 February 2024. Its only
        // annual report is fiscal 2024's, which also reports fiscal 2023 as the prior year.
        DateOnly filed = new(2025, 3, 20);
        SecCompanyFacts facts = Facts(
            ("us-gaap:Revenues", Flow(new DateOnly(2024, 2, 4), new DateOnly(2025, 2, 1), 110m, 2024, "a-25", filed)),
            ("us-gaap:Revenues", Flow(new DateOnly(2023, 1, 29), new DateOnly(2024, 2, 3), 100m, 2024, "a-25", filed)),
            ("us-gaap:Assets", Balance(new DateOnly(2025, 2, 1), 500m, 2024, "a-25", filed)));

        CompanyProfileDto profile = Build(facts);

        Assert.Equal([2023, 2024], profile.FiscalYears.Select(y => y.FiscalYear));
        Assert.Equal(new DateOnly(2025, 2, 1), profile.FiscalYears[1].PeriodEnd);
        Assert.Equal(500m, profile.FiscalYears[1].TotalAssets);
        Assert.Equal(0.1m, profile.FiscalYears[1].RevenueGrowth);
        Assert.Equal("31 January", profile.FiscalYearEnd);
    }

    [Fact]
    public void ABalanceNotAtAFiscalYearEnd_IsIgnored()
    {
        DateOnly filed = new(2026, 2, 20);
        SecCompanyFacts facts = Facts(
            ("us-gaap:Revenues", Flow(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), 100m, 2025, "a", filed)),
            ("us-gaap:Assets", Balance(new DateOnly(2025, 6, 30), 999m, 2025, "a", filed)),
            ("us-gaap:Assets", Balance(new DateOnly(2025, 12, 31), 300m, 2025, "a", filed)));

        Assert.Equal(300m, Assert.Single(Build(facts).FiscalYears).TotalAssets);
    }

    [Fact]
    public void ZeroRevenue_GivesNoMargins_AndNoGrowthFromIt()
    {
        DateOnly filed = new(2026, 2, 20);
        SecCompanyFacts facts = Facts(
            ("us-gaap:Revenues", Flow(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), 0m, 2025, "a", filed)),
            ("us-gaap:Revenues", Flow(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), 0m, 2025, "a", filed)),
            ("us-gaap:NetIncomeLoss",
                Flow(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), -5m, 2025, "a", filed)));

        CompanyFiscalYearDto year = Build(facts).FiscalYears[^1];

        Assert.Equal(0m, year.Revenue);
        Assert.Null(year.NetMargin);
        Assert.Null(year.RevenueGrowth);
    }

    [Fact]
    public void NegativeEquity_GivesNoLiabilitiesToEquity()
    {
        DateOnly filed = new(2026, 2, 20);
        SecCompanyFacts facts = Facts(
            ("us-gaap:Revenues", Flow(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), 10m, 2025, "a", filed)),
            ("us-gaap:Liabilities", Balance(new DateOnly(2025, 12, 31), 120m, 2025, "a", filed)),
            ("us-gaap:StockholdersEquity", Balance(new DateOnly(2025, 12, 31), -20m, 2025, "a", filed)));

        CompanyFiscalYearDto year = Assert.Single(Build(facts).FiscalYears);

        Assert.Equal(-20m, year.Equity);
        Assert.Null(year.LiabilitiesToEquity);
    }

    [Fact]
    public void AnAmendmentFiledLater_ReplacesTheOriginal()
    {
        SecCompanyFacts facts = Facts(
            ("us-gaap:Revenues", Flow(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), 100m, 2025, "orig",
                new DateOnly(2026, 2, 20))),
            ("us-gaap:Revenues", Flow(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), 97m, 2025, "amend",
                new DateOnly(2026, 5, 1), form: "10-K/A")));

        Assert.Equal(97m, Assert.Single(Build(facts).FiscalYears).Revenue);
    }

    [Fact]
    public void ACompanyReportingUnderIfrs_IsFlagged_AndShowsNoFigures()
    {
        CompanyProfileDto profile = CompanyProfileBuilder.Build("EXR", Submissions,
            new SecCompanyFacts(new Dictionary<string, IReadOnlyList<SecFact>>(), HasIfrsFacts: true));

        Assert.True(profile.ReportsUnderIfrs);
        Assert.Empty(profile.FiscalYears);
    }

    [Fact]
    public void ACompanyWithNoFacts_StillHasItsIdentity()
    {
        CompanyProfileDto profile = CompanyProfileBuilder.Build("EXR", Submissions, facts: null);

        Assert.Equal("Example Retail Co", profile.Name);
        Assert.Equal("Trade & Services", profile.IndustryOffice);
        Assert.Empty(profile.FiscalYears);
        Assert.Null(profile.DataAsOf);
    }

    [Fact]
    public void ParsingFacts_SkipsNullFields_AsSecPublishesThem()
    {
        // SEC leaves fy and fp null on facts from some filings (Microsoft's file has over a thousand), and a row may
        // lack a value; neither may stop the rest of the file being read.
        using JsonDocument document = JsonDocument.Parse("""
                                                         { "facts": { "us-gaap": { "Revenues": { "units": { "USD": [
                                                           { "start": "2024-01-01", "end": "2024-12-31", "val": 7, "accn": "a", "fy": null, "fp": null, "form": "8-K", "filed": "2025-01-10" },
                                                           { "start": "2024-01-01", "end": "2024-12-31", "val": null, "accn": "b", "fy": 2024, "fp": "FY", "form": "10-K", "filed": "2025-02-10" },
                                                           { "start": "2024-01-01", "end": "2024-12-31", "val": 9, "accn": "c", "fy": "2024", "fp": "FY", "form": "10-K", "filed": "2025-02-11" },
                                                           { "start": "2024-01-01", "end": "2024-12-31", "val": 10, "accn": "d", "fy": 2024, "fp": "FY", "form": "10-K", "filed": "2025-02-12" }
                                                         ] } } } } }
                                                         """);

        IReadOnlyList<SecFact> facts = SecEdgarClient.ParseCompanyFacts(document.RootElement)
            .Of(CompanyProfileConcepts.All[0]);

        Assert.Equal(["a", "c", "d"], facts.Select(f => f.AccessionNumber));
        Assert.Equal([null, null, 2024], facts.Select(f => f.FiscalYear));
        Assert.Null(facts[0].FiscalPeriod);
    }

    [Fact]
    public void EveryEightKItem_HasPlainWords()
    {
        string[] items =
        [
            "1.01", "1.02", "1.03", "1.04", "1.05", "2.01", "2.02", "2.03", "2.04", "2.05", "2.06", "3.01", "3.02",
            "3.03", "4.01", "4.02", "5.01", "5.02", "5.03", "5.04", "5.05", "5.06", "5.07", "5.08", "6.01", "6.02",
            "6.03", "6.04", "6.05", "7.01", "8.01", "9.01",
        ];

        Assert.All(items, item => Assert.NotEqual(item, EightKItems.Describe(item)));
        Assert.Equal("Results of operations", EightKItems.Describe(" 2.02 "));
    }

    [Theory]
    [InlineData("msft", "MSFT")]
    [InlineData(" brk.b ", "BRK-B")]
    [InlineData("BF-B", "BF-B")]
    [InlineData("A", "A")]
    public void TickerRules_AcceptAndNormalize(string input, string expected)
    {
        Assert.True(CompanyLookupRules.TryNormalizeTicker(input, out string? normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TOOLONGTICKER")]
    [InlineData("-ABC")]
    [InlineData("AB C")]
    [InlineData("AB/C")]
    [InlineData("<script>")]
    public void TickerRules_Reject(string? input)
    {
        Assert.False(CompanyLookupRules.TryNormalizeTicker(input, out _));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("a", true)]
    [InlineData("Microsoft", true)]
    public void QueryRules_AcceptOneToFortyCharacters(string input, bool valid)
    {
        Assert.Equal(valid, CompanyLookupRules.TryNormalizeQuery(input, out _));
        Assert.False(
            CompanyLookupRules.TryNormalizeQuery(new string('a', CompanyLookupRules.MaxQueryLength + 1), out _));
    }
}
