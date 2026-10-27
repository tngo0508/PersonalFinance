using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Data;
using PersonalFinance.Data.Entities;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Research Feature's fund facts snapshot, built through the real SEC EDGAR client over a recorded excerpt of VTI's
/// June 2026 shareholder report (Form N-CSRS) XBRL, in <c>FundFactsSamples/</c>, served by a stub HTTP handler: net
/// assets from the stored funds snapshot, and each fund's expense ratio for its share class. SPY files no shareholder
/// report, and QQQ's here has no XBRL, so neither has a ratio.
/// </summary>
public class ResearchFundFactsServiceTests
{
    private const string UserAgent = "PersonalFinance tests contact@example.org";
    private const string VtiReport = "0001104659-26-102230";
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly TrackedFund Spy = new("SPY", "SPDR S&P 500 ETF Trust", 884394);
    private static readonly TrackedFund Vti = new("VTI", "Vanguard Total Stock Market ETF", null);
    private static readonly TrackedFund Qqq = new("QQQ", "Invesco QQQ Trust", 1067839);

    private static readonly string Samples =
        Path.Combine(RepositoryRoot.Find(), "PersonalFinance", "tests", "PersonalFinance.Tests", "Research", "FundFactsSamples");

    private static AppDbContext CreateContext(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static ResearchService CreateService(AppDbContext db, RecordedSec sec)
    {
        var settings = new SecEdgarSettings { UserAgent = UserAgent, MaxRequestsPerSecond = 1000 };
        var timeProvider = new FixedTimeProvider(Now);
        SecEdgarClient client = ResearchServiceFactory.SecClient(sec, settings);
        var worldClient = new WorldBankClient(new HttpClient(new DelegatingTestHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))));
        var research = new ResearchSettings { SectorsFirstYear = 2030, Funds = [Spy, Vti, Qqq], FundsFirstQuarter = new DateOnly(2026, 1, 1) };
        return ResearchServiceFactory.Create(db, client, worldClient, research, timeProvider);
    }

    /// <summary>Stores a funds snapshot with each fund's net assets for two quarters (the funds refresh finds no filings here).</summary>
    private static async Task SeedFundsAsync(AppDbContext db)
    {
        FundsPeriodDto Quarter(string period, DateOnly date, decimal scale) => new()
        {
            Period = period,
            Funds =
            [
                new FundHoldingsDto { Ticker = "SPY", ReportDate = date, NetAssets = 600_000_000_000m * scale },
                new FundHoldingsDto { Ticker = "VTI", ReportDate = date, NetAssets = 2_000_000_000_000m * scale },
                new FundHoldingsDto { Ticker = "QQQ", ReportDate = date, NetAssets = 350_000_000_000m * scale },
            ],
        };
        var funds = new FundsSnapshotDto
        {
            AsOf = new DateOnly(2026, 6, 30),
            Periods = ["2026 Q1", "2026 Q2"],
            Values = [Quarter("2026 Q1", new DateOnly(2026, 3, 31), 1m), Quarter("2026 Q2", new DateOnly(2026, 6, 30), 1.1m)],
        };
        db.ResearchSnapshots.Add(new ResearchSnapshot
        {
            Dataset = ResearchDatasets.Funds,
            FirstPeriod = "2026Q1",
            LastPeriod = "2026Q2",
            SourceAsOf = funds.AsOf,
            Payload = JsonSerializer.Serialize(funds, JsonSerializerOptions.Web),
            BuiltAt = Now.UtcDateTime,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task XbrlExpenseRatioParser_ReadsEachClassesRatioFromTheRecordedInstance_AsAPercent()
    {
        await using FileStream file = File.OpenRead(Path.Combine(Samples, "vti-ncsrs-instance-excerpt.xml"));

        IReadOnlyList<ClassExpenseRatio> ratios = await XbrlExpenseRatioParser.ParseAsync(file, CancellationToken.None);

        Assert.Equal(3, ratios.Count);
        // VTI's ETF class: 0.0003 in the XBRL, 0.03% (six months to 30 June 2026).
        Assert.Contains(new ClassExpenseRatio("C000007808", 0.03m, new DateOnly(2026, 6, 30)), ratios);
        Assert.Contains(ratios, r => r.ClassId == "C000007779" && r.Percent == 0.19m);
    }

    [Fact]
    public async Task XbrlInstanceOf_FindsTheExtractedInstanceInTheRecordedIndex()
    {
        await using FileStream file = File.OpenRead(Path.Combine(Samples, "vti-ncsrs-index.json"));

        Assert.Equal("tm2620528d1_ncsrs_htm.xml", await SecEdgarClient.XbrlInstanceOfAsync(file, CancellationToken.None));
    }

    [Fact]
    public void RatioFor_TakesTheTickersClass_ElseAReportsOnlyClass()
    {
        ClassExpenseRatio[] several = [new("C1", 0.1m, null), new("C2", 0.2m, null)];
        ClassExpenseRatio[] single = [new("C9", 0.68m, new DateOnly(2026, 5, 31))];

        Assert.Equal(0.2m, FundFactsRefresher.RatioFor(several, "C2")!.Percent);
        Assert.Null(FundFactsRefresher.RatioFor(several, "C3"));
        Assert.Equal(0.68m, FundFactsRefresher.RatioFor(single, null)!.Percent);
    }

    [Fact]
    public async Task RefreshAsync_BuildsFundFacts_WithTheTickersClassRatio_AndNoneForAFundWithoutXbrl()
    {
        await using AppDbContext db = CreateContext($"Facts_{Guid.NewGuid():N}");
        await SeedFundsAsync(db);
        ResearchService service = CreateService(db, new RecordedSec());

        ResearchRefreshSummary summary = await service.RefreshAsync();
        FundFactsSnapshotDto facts = (await service.GetFundFactsAsync()).Snapshot!;

        Assert.Contains(ResearchDatasets.FundFacts, summary.Built);
        Assert.Equal(["SPY", "VTI", "QQQ"], facts.Funds.Select(f => f.Ticker));
        FundFactsDto vti = facts.Funds[1];
        Assert.Equal((0.03m, new DateOnly(2026, 6, 30), VtiReport), (vti.ExpenseRatio, vti.ExpenseRatioAsOf, vti.ShareholderReport));
        // The latest quarter's net assets.
        Assert.Equal((2_200_000_000_000m, new DateOnly(2026, 6, 30)), (vti.NetAssets!.Value, vti.NetAssetsAsOf!.Value));
        // SPY files no shareholder report; QQQ's has no XBRL: no expense ratio, but net assets still.
        Assert.Null(facts.Funds[0].ExpenseRatio);
        Assert.Null(facts.Funds[0].ShareholderReport);
        Assert.Null(facts.Funds[2].ExpenseRatio);
        Assert.Equal(660_000_000_000m, facts.Funds[0].NetAssets);
        Assert.Equal(new DateOnly(2026, 6, 30), facts.AsOf);
    }

    [Fact]
    public async Task RefreshAsync_KeepsAStoredRatio_WithoutDownloadingTheSameReportAgain()
    {
        string database = $"Facts_{Guid.NewGuid():N}";
        var sec = new RecordedSec();
        await using (AppDbContext db = CreateContext(database))
        {
            await SeedFundsAsync(db);
            await CreateService(db, sec).RefreshAsync();
        }

        sec.Requests.Clear();
        await using AppDbContext later = CreateContext(database);
        ResearchService service = CreateService(later, sec);
        await service.RefreshAsync();

        Assert.Equal(0.03m, (await service.GetFundFactsAsync()).Snapshot!.Funds[1].ExpenseRatio);
        Assert.DoesNotContain(sec.Requests, url => url.Contains("000110465926102230/", StringComparison.Ordinal));
    }

    /// <summary>
    /// Answers SEC's fund ticker list, the shareholder-report lists (VTI's series lists its June 2026 N-CSRS; SPY's CIK
    /// none; QQQ's one report without XBRL), and the recorded VTI filing's index and instance. Anything else is an
    /// empty list or a 404.
    /// </summary>
    private sealed class RecordedSec : HttpMessageHandler
    {
        private const string Browse = "https://www.sec.gov/cgi-bin/browse-edgar?";

        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            Requests.Add(url);
            if (url == "https://www.sec.gov/files/company_tickers_mf.json")
            {
                return Ok("""{"fields":["cik","seriesId","classId","symbol"],"data":[[36405,"S000002848","C000007808","VTI"],[1067839,"S000101292","C000271435","QQQ"]]}""",
                    "application/json");
            }

            if (url.StartsWith(Browse, StringComparison.Ordinal))
            {
                string entries = url.Contains("type=N-CSR", StringComparison.Ordinal) switch
                {
                    true when url.Contains("CIK=S000002848", StringComparison.Ordinal) =>
                        Entry(VtiReport, "2026-08-27", "N-CSRS", 36405),
                    true when url.Contains("CIK=1067839", StringComparison.Ordinal) =>
                        Entry("0001193125-26-200000", "2026-06-01", "N-CSRS", 1067839),
                    _ => string.Empty,
                };
                return Ok($"""<?xml version="1.0" encoding="ISO-8859-1" ?><feed xmlns="http://www.w3.org/2005/Atom">{entries}</feed>""",
                    "application/atom+xml");
            }

            if (url == "https://www.sec.gov/Archives/edgar/data/36405/000110465926102230/index.json")
            {
                return Ok(await File.ReadAllTextAsync(Path.Combine(Samples, "vti-ncsrs-index.json"), cancellationToken),
                    "application/json");
            }

            if (url == "https://www.sec.gov/Archives/edgar/data/36405/000110465926102230/tm2620528d1_ncsrs_htm.xml")
            {
                return Ok(await File.ReadAllTextAsync(Path.Combine(Samples, "vti-ncsrs-instance-excerpt.xml"), cancellationToken),
                    "text/xml");
            }

            if (url == "https://www.sec.gov/Archives/edgar/data/1067839/000119312526200000/index.json")
            {
                // A report filed without XBRL: no *_htm.xml instance.
                return Ok("""{"directory":{"item":[{"name":"d123ncsrs.htm"},{"name":"FilingSummary.xml"}]}}""", "application/json");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static string Entry(string accession, string filedOn, string formType, long cik)
        {
            string folder = accession.Replace("-", string.Empty, StringComparison.Ordinal);
            return $"""
                <entry><content type="text/xml">
                  <accession-number>{accession}</accession-number>
                  <filing-date>{filedOn}</filing-date>
                  <filing-href>https://www.sec.gov/Archives/edgar/data/{cik}/{folder}/{accession}-index.htm</filing-href>
                  <filing-type>{formType}</filing-type>
                </content></entry>
                """;
        }

        private static HttpResponseMessage Ok(string body, string mediaType) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
    }
}
