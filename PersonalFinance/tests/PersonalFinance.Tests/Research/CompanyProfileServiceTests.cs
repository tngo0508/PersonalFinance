using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Companies tab's service, through the real SEC EDGAR client over a stub SEC that serves the made-up samples in
/// <c>CompanySamples/</c>: Sample Widgets Inc (SWI), a company with a September fiscal year and 10-Ks for fiscal
/// 2014 to 2025, each reporting the prior year too.
/// </summary>
public class CompanyProfileServiceTests
{
    private const string UserId = "account-1";
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private sealed class StubSec : HttpMessageHandler
    {
        private static readonly string Samples =
            Path.Combine(RepositoryRoot.Find(), "PersonalFinance", "tests", "PersonalFinance.Tests", "Research",
                "CompanySamples");

        public ConcurrentDictionary<string, int> Requests { get; } = new();

        /// <summary>True to answer 500 for one company's documents (SEC down).</summary>
        public bool CompanyDocumentsFail { get; set; }

        /// <summary>True to answer 500 for the ticker lists.</summary>
        public bool TickerListsFail { get; set; }

        /// <summary>A company-facts document to serve instead of the sample, or null for the sample.</summary>
        public string? CompanyFactsBody { get; set; }

        public int CompanyDocumentRequests =>
            Requests.Where(r => r.Key.Contains("CIK", StringComparison.Ordinal)).Sum(r => r.Value);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            Requests.AddOrUpdate(url, 1, (_, n) => n + 1);
            Assert.Contains("contact@example.com", request.Headers.UserAgent.ToString(), StringComparison.Ordinal);

            (string? file, bool fails) = url switch
            {
                "https://www.sec.gov/files/company_tickers_exchange.json" => ("company-tickers-sample.json",
                    TickerListsFail),
                "https://www.sec.gov/files/company_tickers_mf.json" => ("company-tickers-mf-sample.json",
                    TickerListsFail),
                "https://data.sec.gov/submissions/CIK0001234567.json" => ("submissions-sample-widgets.json",
                    CompanyDocumentsFail),
                "https://data.sec.gov/api/xbrl/companyfacts/CIK0001234567.json" => ("companyfacts-sample-widgets.json",
                    CompanyDocumentsFail),
                _ => (null, false),
            };
            HttpResponseMessage response = fails
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : file is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            file.StartsWith("companyfacts", StringComparison.Ordinal) && CompanyFactsBody is { } body
                                ? body
                                : File.ReadAllText(Path.Combine(Samples, file)),
                            Encoding.UTF8, "application/json"),
                    };
            return Task.FromResult(response);
        }
    }

    private sealed class MovableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static CompanyProfileService CreateService(StubSec sec, TimeProvider? timeProvider = null,
        IRateLimiterService? rateLimiter = null, ResearchSettings? research = null)
    {
        timeProvider ??= new MovableTimeProvider(Now);
        var settings = new SecEdgarSettings
            { UserAgent = "PersonalFinance tests contact@example.com", MaxRequestsPerSecond = 1000 };
        var client = new SecEdgarClient(new HttpClient(sec), settings, new SecRequestGate(settings, timeProvider));
        return new CompanyProfileService(client,
            new CompanyTickerIndexCache(research ?? new ResearchSettings(), timeProvider,
                NullLogger<CompanyTickerIndexCache>.Instance),
            new CompanyProfileCache(timeProvider),
            rateLimiter ??
            new RateLimiterService(CompanyLookupRateLimit.MaxLookupsPerWindow, CompanyLookupRateLimit.Window),
            NullLogger<CompanyProfileService>.Instance);
    }

    private static async Task<CompanyProfileDto> ProfileOf(CompanyProfileService service, string ticker = "SWI")
    {
        CompanyLookupOutcome outcome = await service.LookUpAsync(UserId, ticker);
        Assert.NotNull(outcome.Result);
        Assert.Equal(CompanyLookupKinds.Profile, outcome.Result.Kind);
        return Assert.IsType<CompanyProfileDto>(outcome.Result.Profile);
    }

    private static CompanyFiscalYearDto Year(CompanyProfileDto profile, int year) =>
        Assert.Single(profile.FiscalYears, y => y.FiscalYear == year);

    [Fact]
    public async Task LookUp_BuildsTheIdentity_FromTheSubmissions()
    {
        CompanyProfileDto profile = await ProfileOf(CreateService(new StubSec()));

        Assert.Equal(1234567, profile.Cik);
        Assert.Equal("Sample Widgets Inc", profile.Name); // SEC's "/DE/" suffix dropped, title-cased.
        Assert.Equal("SWI", profile.Ticker);
        Assert.Equal("Nasdaq", profile.Exchange);
        Assert.Equal("3580", profile.SicCode);
        Assert.Equal("Refrigeration & Service Industry Machinery", profile.SicDescription);
        Assert.Equal("Manufacturing", profile.IndustryOffice);
        Assert.Equal("DE", profile.StateOfIncorporation);
        Assert.Equal("30 September", profile.FiscalYearEnd);
        Assert.False(profile.ReportsUnderIfrs);
        Assert.Equal("https://www.sec.gov/edgar/browse/?CIK=1234567", profile.AllFilingsUrl);
    }

    [Fact]
    public async Task LookUp_ShowsTheTenLatestFiscalYears_OldestFirst()
    {
        CompanyProfileDto profile = await ProfileOf(CreateService(new StubSec()));

        // The sample reports fiscal 2013 (as a prior year) to 2025.
        Assert.Equal(Enumerable.Range(2016, 10), profile.FiscalYears.Select(y => y.FiscalYear));
        Assert.Equal(new DateOnly(2025, 9, 30), Year(profile, 2025).PeriodEnd);
    }

    [Fact]
    public async Task LookUp_ReadsEachFigure_FromItsConcept()
    {
        CompanyFiscalYearDto year = Year(await ProfileOf(CreateService(new StubSec())), 2021);

        Assert.Equal(1_800_000_000m, year.Revenue);
        Assert.Equal(720_000_000m, year.GrossProfit);
        Assert.Equal(180_000_000m, year.NetIncome);
        Assert.Equal(1.8m, year.DilutedEps);
        Assert.Equal(246_000_000m, year.OperatingCashFlow);
        Assert.Equal(2_800_000_000m, year.TotalAssets);
        Assert.Equal(1_600_000_000m, year.TotalLiabilities);
        Assert.Equal(1_200_000_000m, year.Equity);
        Assert.Equal(107_000_000m, year.SharesOutstanding);
        Assert.Equal(6_750_000_000m, year.PublicFloat);
    }

    [Fact]
    public async Task LookUp_TakesTheFirstRevenueConceptWithAValue_EachYear()
    {
        CompanyProfileDto profile = await ProfileOf(CreateService(new StubSec()));

        Assert.Equal(1_400_000_000m, Year(profile, 2017).Revenue); // SalesRevenueNet only.
        Assert.Equal(1_500_000_000m, Year(profile, 2018).Revenue); // RevenueFromContractWithCustomer... only.
        Assert.Equal(2_205_000_000m, Year(profile, 2025).Revenue); // Revenues wins over the ASC 606 concept.
    }

    [Fact]
    public async Task LookUp_IgnoresQuarters_TenQs_AndOtherCurrencies()
    {
        CompanyProfileDto profile = await ProfileOf(CreateService(new StubSec()));
        CompanyFiscalYearDto year = Year(profile, 2025);

        // The sample also holds a 10-Q quarter of fiscal 2026, a quarter inside the 10-K, and a net income in euros.
        Assert.Equal(2_205_000_000m, year.Revenue);
        Assert.Equal(220_000_000m, year.NetIncome);
        Assert.DoesNotContain(profile.FiscalYears, y => y.FiscalYear == 2026);
    }

    [Fact]
    public async Task LookUp_TakesTheLatestFiledValue_SoARestatementWins()
    {
        CompanyFiscalYearDto year = Year(await ProfileOf(CreateService(new StubSec())), 2024);

        // The fiscal 2024 10-K reported $210M; the fiscal 2025 10-K restated it to $190M.
        Assert.Equal(190_000_000m, year.NetIncome);
    }

    [Fact]
    public async Task LookUp_FallsBackForLiabilitiesAndEquity()
    {
        CompanyProfileDto profile = await ProfileOf(CreateService(new StubSec()));

        // Fiscal 2023 has no Liabilities: liabilities-and-equity ($3,000M) minus equity ($1,300M).
        Assert.Equal(1_700_000_000m, Year(profile, 2023).TotalLiabilities);
        // Fiscal 2022 reports equity only including noncontrolling interests.
        Assert.Equal(1_250_000_000m, Year(profile, 2022).Equity);
    }

    [Fact]
    public async Task LookUp_DerivesMarginsGrowthAndLeverage()
    {
        CompanyProfileDto profile = await ProfileOf(CreateService(new StubSec()));
        CompanyFiscalYearDto year = Year(profile, 2025);

        Assert.Equal(0.3991m, year.GrossMargin); // 880 / 2,205
        Assert.Equal(0.0998m, year.NetMargin); // 220 / 2,205
        Assert.Equal(0.05m, year.RevenueGrowth); // 2,205 / 2,100 - 1
        Assert.Equal(0.0476m, year.EpsGrowth); // 2.20 / 2.10 - 1
        Assert.Equal(1.2857m, year.LiabilitiesToEquity); // 1,800 / 1,400
        // The oldest year shown still has growth: its prior year is read, just not shown.
        Assert.Equal(0.0833m, Year(profile, 2016).RevenueGrowth); // 1,300 / 1,200 - 1
    }

    [Fact]
    public async Task LookUp_LeavesAFigureNull_WhenItOrAnInputIsNotReported()
    {
        CompanyProfileDto profile = await ProfileOf(CreateService(new StubSec()));

        // No gross profit in fiscal 2019, so no gross margin either; never zero.
        Assert.Null(Year(profile, 2019).GrossProfit);
        Assert.Null(Year(profile, 2019).GrossMargin);
        Assert.NotNull(Year(profile, 2019).NetMargin);
        // Fiscal 2016 was a loss, so fiscal 2017's EPS growth from it is not shown.
        Assert.Equal(-0.5m, Year(profile, 2016).DilutedEps);
        Assert.Null(Year(profile, 2017).EpsGrowth);
    }

    [Fact]
    public async Task LookUp_ListsTheTwentyLatestReports_WithEightKEventsInWords()
    {
        CompanyProfileDto profile = await ProfileOf(CreateService(new StubSec()));

        Assert.Equal(CompanyProfileBuilder.MaxFilings, profile.Filings.Count);
        Assert.DoesNotContain(profile.Filings, f => f.Form is "4" or "S-8");
        Assert.Equal(profile.Filings.OrderByDescending(f => f.FiledOn).Select(f => f.FiledOn),
            profile.Filings.Select(f => f.FiledOn));

        CompanyFilingDto latest = profile.Filings[0];
        Assert.Equal("8-K", latest.Form);
        Assert.Equal(["9.99"], latest.Events); // An unknown item code is shown as it is.
        Assert.Equal(
            "https://www.sec.gov/Archives/edgar/data/1234567/000123456726000041/0001234567-26-000041-index.htm",
            latest.Url);
        Assert.Equal(["Departure or appointment of directors or officers"], profile.Filings[1].Events);
        Assert.Equal(["Results of operations", "Financial statements and exhibits"], profile.Filings[2].Events);
        Assert.Equal("10-Q", profile.Filings[3].Form);
        Assert.Empty(profile.Filings[3].Events);
    }

    [Fact]
    public async Task LookUp_DataAsOf_IsTheLatestListedFilingDate()
    {
        CompanyProfileDto profile = await ProfileOf(CreateService(new StubSec()));

        Assert.Equal(new DateOnly(2026, 10, 20), profile.DataAsOf);
    }

    [Fact]
    public async Task LookUp_OfASecondShareClass_ShowsThatTicker()
    {
        CompanyProfileDto profile = await ProfileOf(CreateService(new StubSec()), "SWI-P");

        Assert.Equal("SWI-P", profile.Ticker);
        Assert.Null(profile.Exchange); // SEC lists no exchange for it.
    }

    [Theory]
    [InlineData("SWFD")] // In SEC's company and fund lists.
    [InlineData("SWFDX")] // Only in SEC's fund list.
    [InlineData("SPY")] // A ticker of the ETFs tab.
    public async Task LookUp_OfAFund_SaysItIsAFund_WithoutReadingAProfile(string ticker)
    {
        var sec = new StubSec();
        var research = new ResearchSettings { Etfs = [new ResearchEtf("SPY", "SPDR S&P 500 ETF Trust", 884394)] };

        CompanyLookupOutcome outcome = await CreateService(sec, research: research).LookUpAsync(UserId, ticker);

        Assert.Equal(CompanyLookupKinds.Fund, outcome.Result?.Kind);
        Assert.Null(outcome.Result?.Profile);
        Assert.Equal(0, sec.CompanyDocumentRequests);
    }

    [Fact]
    public async Task LookUp_OfAnUnlistedTicker_IsNotFound()
    {
        CompanyLookupOutcome outcome = await CreateService(new StubSec()).LookUpAsync(UserId, "ZZZZ");

        Assert.Equal(CompanyLookupKinds.NotFound, outcome.Result?.Kind);
        Assert.Equal("ZZZZ", outcome.Result?.Ticker);
    }

    [Fact]
    public async Task LookUp_Twice_ReadsSecOnce()
    {
        var sec = new StubSec();
        CompanyProfileService service = CreateService(sec);

        await ProfileOf(service);
        await ProfileOf(service);

        Assert.Equal(2, sec.CompanyDocumentRequests); // Submissions and facts, once each.
    }

    [Fact]
    public async Task LookUp_AfterADay_ReadsSecAgain()
    {
        var sec = new StubSec();
        var time = new MovableTimeProvider(Now);
        CompanyProfileService service = CreateService(sec, time);

        await ProfileOf(service);
        time.Now = Now + CompanyProfileCache.FreshFor;
        await ProfileOf(service);

        Assert.Equal(4, sec.CompanyDocumentRequests);
    }

    [Fact]
    public async Task LookUp_WhenSecFails_ServesTheKeptProfile()
    {
        var sec = new StubSec();
        var time = new MovableTimeProvider(Now);
        CompanyProfileService service = CreateService(sec, time);
        CompanyProfileDto first = await ProfileOf(service);

        time.Now = Now + TimeSpan.FromDays(2);
        sec.CompanyDocumentsFail = true;
        CompanyProfileDto served = await ProfileOf(service);

        Assert.Same(first, served);
    }

    [Fact]
    public async Task LookUp_WhenSecFails_WithNoKeptProfile_IsUnavailable()
    {
        var sec = new StubSec { CompanyDocumentsFail = true };

        CompanyLookupOutcome outcome = await CreateService(sec).LookUpAsync(UserId, "SWI");

        Assert.True(outcome.Unavailable);
        Assert.False(outcome.RateLimited);
    }

    [Fact]
    public async Task LookUp_WhenSecSendsSomethingTheParserDoesNotExpect_IsUnavailable_NotAnError()
    {
        // Facts whose taxonomy is a number, not an object: the lookup must not escape as a 500.
        var sec = new StubSec { CompanyFactsBody = """{ "facts": { "us-gaap": 5, "dei": [1, 2] } }""" };

        CompanyLookupOutcome outcome = await CreateService(sec).LookUpAsync(UserId, "SWI");

        Assert.Equal(CompanyLookupKinds.Profile, outcome.Result?.Kind);
        Assert.Empty(outcome.Result!.Profile!.FiscalYears);
    }

    [Fact]
    public async Task LookUp_WhenReadingSecThrowsAnythingElse_ServesNoErrorButUnavailable()
    {
        var timeProvider = new MovableTimeProvider(Now);
        var service = new CompanyProfileService(new ThrowingFactsClient(),
            new CompanyTickerIndexCache(new ResearchSettings(), timeProvider,
                NullLogger<CompanyTickerIndexCache>.Instance),
            new CompanyProfileCache(timeProvider),
            new RateLimiterService(CompanyLookupRateLimit.MaxLookupsPerWindow, CompanyLookupRateLimit.Window),
            NullLogger<CompanyProfileService>.Instance);

        CompanyLookupOutcome outcome = await service.LookUpAsync(UserId, "SWI");

        Assert.True(outcome.Unavailable);
    }

    private sealed class ThrowingFactsClient : ISecCompanyClient
    {
        public Task<IReadOnlyList<SecCompanyListing>> GetCompanyTickersAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SecCompanyListing>>([
                new SecCompanyListing(1234567, "SWI", "SAMPLE WIDGETS INC", null)
            ]);

        public Task<IReadOnlyList<string>> GetFundTickersAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<SecCompanySubmissions?> GetCompanySubmissionsAsync(long cik, CancellationToken cancellationToken) =>
            Task.FromResult<SecCompanySubmissions?>(new SecCompanySubmissions(cik, "SAMPLE WIDGETS INC", ["SWI"],
                [null],
                null, null, null, null, null, []));

        public Task<SecCompanyFacts?> GetCompanyFactsAsync(long cik, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An unexpected document shape.");
    }

    [Fact]
    public async Task LookUp_WhenTheTickerListCannotBeRead_IsUnavailable()
    {
        CompanyLookupOutcome outcome =
            await CreateService(new StubSec { TickerListsFail = true }).LookUpAsync(UserId, "SWI");

        Assert.True(outcome.Unavailable);
    }

    [Fact]
    public async Task LookUp_RefusesTheLookupOverTheLimit_PerAccount()
    {
        var sec = new StubSec();
        CompanyProfileService service =
            CreateService(sec, rateLimiter: new RateLimiterService(3, TimeSpan.FromMinutes(1)));

        for (int i = 0; i < 3; i++)
        {
            Assert.False((await service.LookUpAsync(UserId, "SWI")).RateLimited);
        }

        CompanyLookupOutcome refused = await service.LookUpAsync(UserId, "SWI");
        CompanyLookupOutcome otherAccount = await service.LookUpAsync("account-2", "SWI");

        Assert.True(refused.RateLimited);
        Assert.Null(refused.Result);
        Assert.False(otherAccount.RateLimited);
    }

    [Fact]
    public async Task Suggest_DoesNotCountTowardTheLookupLimit()
    {
        CompanyProfileService service =
            CreateService(new StubSec(), rateLimiter: new RateLimiterService(1, TimeSpan.FromMinutes(1)));

        for (int i = 0; i < 5; i++)
        {
            Assert.NotNull(await service.SuggestAsync("swi"));
        }

        Assert.False((await service.LookUpAsync(UserId, "SWI")).RateLimited);
    }

    [Fact]
    public async Task Suggest_RanksTheExactTicker_ThenTickerPrefixes_ThenNameWords()
    {
        List<CompanySuggestionDto>? suggestions = await CreateService(new StubSec()).SuggestAsync("swi");

        Assert.NotNull(suggestions);
        Assert.Equal(["SWI", "SWI-P", "SWIF", "ACSW", "BSN"], suggestions.Select(s => s.Ticker));
        Assert.Equal("Sample Widgets Inc", suggestions[0].Name);
        Assert.Equal("Nasdaq", suggestions[0].Exchange);
    }

    [Fact]
    public async Task Suggest_ByName_FlagsFunds()
    {
        List<CompanySuggestionDto>? suggestions = await CreateService(new StubSec()).SuggestAsync("Sample");

        Assert.NotNull(suggestions);
        Assert.Equal(["SH-B", "SWFD", "SWI", "SWI-P"], suggestions.Select(s => s.Ticker));
        Assert.Equal([false, true, false, false], suggestions.Select(s => s.IsFund));
    }

    [Fact]
    public async Task Suggest_ReadsADotAsSecsDash()
    {
        List<CompanySuggestionDto>? suggestions = await CreateService(new StubSec()).SuggestAsync("sh.b");

        Assert.Equal("SH-B", Assert.Single(suggestions!).Ticker);
    }

    [Fact]
    public async Task Suggest_ReadsTheTickerListOnce()
    {
        var sec = new StubSec();
        CompanyProfileService service = CreateService(sec);

        await service.SuggestAsync("swi");
        await service.SuggestAsync("sam");

        Assert.Equal(1, sec.Requests["https://www.sec.gov/files/company_tickers_exchange.json"]);
    }

    [Fact]
    public async Task Suggest_WhenARereadFails_KeepsServingThePreviousList()
    {
        var sec = new StubSec();
        var time = new MovableTimeProvider(Now);
        CompanyProfileService service = CreateService(sec, time);
        await service.SuggestAsync("swi");

        time.Now = Now + CompanyTickerIndexCache.FreshFor;
        sec.TickerListsFail = true;
        List<CompanySuggestionDto>? suggestions = await service.SuggestAsync("swi");

        Assert.Equal(2, sec.Requests["https://www.sec.gov/files/company_tickers_exchange.json"]);
        Assert.NotEmpty(suggestions!);
    }

    [Fact]
    public async Task Suggest_WhenTheTickerListWasNeverRead_IsNull()
    {
        Assert.Null(await CreateService(new StubSec { TickerListsFail = true }).SuggestAsync("swi"));
    }

    [Fact]
    public async Task Suggest_ReturnsAtMostEight()
    {
        var index = new CompanyTickerIndex(
            Enumerable.Range(0, 20).Select(i => new SecCompanyListing(i, $"AB{i:00}", $"Company {i}", null)), []);

        Assert.Equal(CompanyTickerIndex.MaxSuggestions, index.Suggest("AB").Count);
    }
}
