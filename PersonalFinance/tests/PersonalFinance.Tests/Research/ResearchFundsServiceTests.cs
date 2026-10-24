using System.Globalization;
using System.Net;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Research Feature's funds snapshot, built through the real SEC EDGAR client over canned Form N-PORT filings served
/// by a stub HTTP handler and stored in an InMemory database: per quarter and fund, the asset-category weights, the
/// top 25 holdings of each category with the rest summed into "Other", and the holding count; and the fund overlap
/// snapshot built from each fund's latest filing, whose securities are matched by identifiers that are never stored.
/// </summary>
public class ResearchFundsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    private const string UserAgent = "PersonalFinance tests contact@example.com";

    private static readonly TrackedFund Spy = new("SPY", "SPDR S&P 500 ETF Trust", 884394);
    private static readonly TrackedFund Vti = new("VTI", "Vanguard Total Stock Market ETF", null);
    private static readonly TrackedFund Iwm = new("IWM", "iShares Russell 2000 ETF", null);

    private static AppDbContext CreateContext(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static ResearchService CreateService(AppDbContext db, StubNportHandler handler, params TrackedFund[] funds) =>
        CreateService(db, handler, null, funds);

    private static ResearchService CreateService(AppDbContext db, StubNportHandler handler, ILoggerProvider? logs,
        params TrackedFund[] funds)
    {
        var settings = new SecEdgarSettings { UserAgent = UserAgent, MaxRequestsPerSecond = 1000 };
        var timeProvider = new FixedTimeProvider(Now);
        var client = new SecEdgarClient(new HttpClient(handler), settings, new SecRequestGate(settings, timeProvider));
        // No Public Float frames are served and the World Bank is down, so the Sectors and World datasets fail on
        // their own; these tests read only funds.
        var research = new ResearchSettings { SectorsFirstYear = 2025, Funds = funds, FundsFirstQuarter = new DateOnly(2019, 10, 1) };
        var worldClient = new WorldBankClient(new HttpClient(new DelegatingTestHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))));
        return ResearchServiceFactory.Create(db, client, worldClient, research, timeProvider, logs);
    }

    /// <summary>
    /// The requests of the funds refresh alone: those before the fund overlap refresh, which runs next, lists the
    /// first fund's filings a second time.
    /// </summary>
    private static List<string> FundsRefreshRequests(StubNportHandler handler, string firstFundList)
    {
        int first = handler.Requests.FindIndex(url => url.Contains(firstFundList, StringComparison.Ordinal));
        int overlapStart = first < 0
            ? -1
            : handler.Requests.FindIndex(first + 1, url => url.Contains(firstFundList, StringComparison.Ordinal));
        return overlapStart < 0 ? handler.Requests : handler.Requests.Take(overlapStart).ToList();
    }

    private static async Task<FundsSnapshotDto> RefreshAndReadAsync(ResearchService service)
    {
        ResearchRefreshSummary summary = await service.RefreshAsync();
        Assert.Contains(ResearchDatasets.Funds, summary.Built);
        return (await service.GetFundsAsync()).Snapshot!;
    }

    [Fact]
    public async Task GetFundsAsync_OnAnEmptyTable_ReturnsNeverLoaded()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");

        ResearchSnapshotResult<FundsSnapshotDto> result = await CreateService(db, new StubNportHandler(), Spy).GetFundsAsync();

        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task RefreshAsync_KeepsTheTop25HoldingsOfACategory_AndSumsTheRestIntoOther()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        var handler = new StubNportHandler();
        // 30 common stocks summing to 93% of net assets: holding i is i x 0.2%.
        List<Holding> stocks = Enumerable.Range(1, 30).Select(i => new Holding($"Stock {i}", "EC", i * 0.2m)).ToList();
        handler.AddFiling("884394", "0000884394-26-000001", "2026-08-28", "2026-06-30", seriesId: null,
            [.. stocks, new Holding("S&P 500 E-mini future", "DE", 0.05m)]);

        FundsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, handler, Spy));

        Assert.Equal(["2026 Q2"], snapshot.Periods);
        Assert.Equal(new DateOnly(2026, 6, 30), snapshot.AsOf);
        TrackedFundDto fund = Assert.Single(snapshot.Funds);
        Assert.Equal(("SPY", "SPDR S&P 500 ETF Trust"), (fund.Ticker, fund.Name));
        FundHoldingsDto spy = Assert.Single(Assert.Single(snapshot.Values).Funds);
        Assert.Equal(31, spy.HoldingCount);
        Assert.Equal(new DateOnly(2026, 6, 30), spy.ReportDate);

        FundCategoryDto equity = spy.Categories[0];
        Assert.Equal(("EC", "Common equity", 30), (equity.Code, equity.Name, equity.HoldingCount));
        Assert.Equal(93m, equity.Percent); // 0.2% x (1 + ... + 30)
        Assert.Equal(25, equity.Top.Count);
        Assert.Equal(("Stock 30", 6m), (equity.Top[0].Name, equity.Top[0].Percent));
        Assert.Equal(("Stock 6", 1.2m), (equity.Top[^1].Name, equity.Top[^1].Percent));
        Assert.Equal(5, equity.OtherCount);
        Assert.Equal(3m, equity.OtherPercent); // 0.2% x (1 + ... + 5)
        Assert.Equal(equity.Percent, equity.Top.Sum(h => h.Percent) + equity.OtherPercent);

        FundCategoryDto derivatives = spy.Categories[1];
        Assert.Equal(("DE", "Equity derivatives", 1, 0.05m), (derivatives.Code, derivatives.Name, derivatives.HoldingCount, derivatives.Percent));
        Assert.Equal(0, derivatives.OtherCount);
        Assert.Equal(0m, derivatives.OtherPercent);
    }

    [Fact]
    public async Task RefreshAsync_FindsASeriesThroughTheFundTickerList_AndReadsItsFilingsBySeriesId()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        var handler = new StubNportHandler();
        handler.Tickers.Add((36405, "S000002848", "C000007808", "VTI"));
        handler.AddFiling("S000002848", "0000036405-26-000010", "2026-05-28", "2026-03-31", "S000002848",
            [new Holding("Microsoft Corp", "EC", 6m), new Holding("Vanguard Market Liquidity Fund", "STIV", 0.4m)],
            cik: 36405);

        FundsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, handler, Vti));

        FundHoldingsDto vti = Assert.Single(Assert.Single(snapshot.Values).Funds);
        Assert.Equal("VTI", vti.Ticker);
        Assert.Equal(["Common equity", "Short-term investment vehicles"], vti.Categories.Select(c => c.Name));
        Assert.Contains(handler.Requests, url => url.Contains("CIK=S000002848", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshAsync_WhenOneFundIsMissingAQuarter_KeepsTheOtherFundsOfThatQuarter()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        var handler = new StubNportHandler();
        handler.Tickers.Add((36405, "S000002848", "C000007808", "VTI"));
        handler.Tickers.Add((1100663, "S000004344", "C000012074", "IWM"));
        handler.AddFiling("S000002848", "0000036405-26-000001", "2026-02-27", "2025-12-31", "S000002848",
            [new Holding("Apple Inc", "EC", 6m)], cik: 36405);
        handler.AddFiling("S000002848", "0000036405-26-000002", "2026-05-28", "2026-03-31", "S000002848",
            [new Holding("Apple Inc", "EC", 6.5m)], cik: 36405);
        // IWM's Q4 2025 filing is listed but its document answers 500; its Q1 2026 filing reads.
        handler.AddFiling("S000004344", "0001100663-26-000001", "2026-02-25", "2025-12-31", "S000004344",
            [new Holding("Bloom Energy", "EC", 0.6m)], cik: 1100663);
        handler.FailingDocuments.Add("0001100663-26-000001");
        handler.AddFiling("S000004344", "0001100663-26-000002", "2026-05-27", "2026-03-31", "S000004344",
            [new Holding("Bloom Energy", "EC", 0.7m)], cik: 1100663);

        FundsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, handler, Vti, Iwm));

        Assert.Equal(["2025 Q4", "2026 Q1"], snapshot.Periods);
        Assert.Equal(["VTI"], snapshot.Values[0].Funds.Select(f => f.Ticker));
        Assert.Equal(["VTI", "IWM"], snapshot.Values[1].Funds.Select(f => f.Ticker));
        Assert.Equal(["VTI", "IWM"], snapshot.Funds.Select(f => f.Ticker));
    }

    [Fact]
    public async Task RefreshAsync_LeavesOutReportsBeforeTheFirstQuarter_AndTakesAnAmendmentOverItsOriginal()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        var handler = new StubNportHandler();
        handler.AddFiling("884394", "0000884394-19-000001", "2019-11-18", "2019-09-30", null, [new Holding("Old", "EC", 1m)]);
        handler.AddFiling("884394", "0000884394-20-000001", "2020-02-28", "2019-12-31", null, [new Holding("Original", "EC", 1m)]);
        handler.AddFiling("884394", "0000884394-20-000002", "2020-03-15", "2019-12-31", null, [new Holding("Amended", "EC", 1m)],
            formType: "NPORT-P/A");

        FundsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, handler, Spy));

        Assert.Equal(["2019 Q4"], snapshot.Periods);
        Assert.Equal("Amended", snapshot.Values[0].Funds[0].Categories[0].Top[0].Name);
    }

    [Fact]
    public async Task RefreshAsync_PlacesAReportInTheCalendarQuarterOfItsDate_AndReadsTheAssetConditionalCategory()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        var handler = new StubNportHandler();
        // A fund whose fiscal quarters end in January, April, July and October (as VT's do).
        handler.AddFiling("884394", "0000884394-26-000001", "2026-09-28", "2026-07-31", null,
            [new Holding("Equity swap", "OTHER", 0.25m), new Holding("Toyota", "EC", 1m)]);

        FundsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, handler, Spy));

        Assert.Equal(["2026 Q3"], snapshot.Periods);
        Assert.Equal(["Common equity", "Other"], snapshot.Values[0].Funds[0].Categories.Select(c => c.Name));
    }

    [Fact]
    public async Task RefreshAsync_TidiesHoldingNames_CollapsingRunsOfSpaces_AndTitleCasingCapitals()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        var handler = new StubNportHandler();
        handler.AddFiling("884394", "0000884394-26-000001", "2026-09-28", "2026-07-31", null,
            [new Holding("MSCI EAFE         Sep26", "DE", 0.1m), new Holding("TOYOTA MOTOR CORP", "EC", 1m)]);

        FundsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, handler, Spy));

        Assert.Equal(["Toyota Motor Corp", "MSCI EAFE Sep26"],
            snapshot.Values[0].Funds[0].Categories.Select(c => c.Top[0].Name));
    }

    [Fact]
    public async Task RefreshAsync_ReadsEachFilingOnce_SoALaterRefreshFetchesOnlyNewFilings()
    {
        string database = $"Funds_{Guid.NewGuid():N}";
        var handler = new StubNportHandler();
        handler.AddFiling("884394", "0000884394-26-000001", "2026-02-26", "2025-12-31", null, [new Holding("Apple Inc", "EC", 7m)]);
        await using (AppDbContext db = CreateContext(database))
        {
            await RefreshAndReadAsync(CreateService(db, handler, Spy));
        }

        handler.AddFiling("884394", "0000884394-26-000002", "2026-05-28", "2026-03-31", null, [new Holding("Apple Inc", "EC", 6m)]);
        handler.Requests.Clear();
        await using AppDbContext later = CreateContext(database);
        FundsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(later, handler, Spy));

        Assert.Equal(["2025 Q4", "2026 Q1"], snapshot.Periods);
        Assert.Single(handler.Requests, url => url.EndsWith("primary_doc.xml", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, url => url.Contains("000088439426000002", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshAsync_ReadsAMutualFundThroughTheFundTickerList_AndKeepsEachFundsRole()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        var handler = new StubNportHandler();
        // A mutual fund share class is in SEC's fund ticker list like an ETF's, under its series.
        handler.Tickers.Add((819118, "S000006027", "C000100045", "FXAIX"));
        handler.AddFiling("S000006027", "0000819118-26-000001", "2026-05-28", "2026-03-31", "S000006027",
            [new Holding("Apple Inc", "EC", 7m)], cik: 819118);
        handler.AddFiling("884394", "0000884394-26-000001", "2026-05-28", "2026-03-31", null, [new Holding("Apple Inc", "EC", 7m)]);
        var fxaix = new TrackedFund("FXAIX", "Fidelity 500 Index Fund", null, "Index mutual funds");
        TrackedFund spy = Spy with { Role = "US broad" };

        FundsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, handler, spy, fxaix));

        Assert.Equal([("SPY", "US broad"), ("FXAIX", "Index mutual funds")],
            snapshot.Funds.Select(f => (f.Ticker, f.Role)));
        Assert.Equal(["SPY", "FXAIX"], Assert.Single(snapshot.Values).Funds.Select(f => f.Ticker));
        Assert.Contains(handler.Requests, url => url.Contains("CIK=S000006027", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshAsync_AFundAddedLater_IsReadBackToTheFirstQuarter_WithoutFetchingTheOthersAgain()
    {
        string database = $"Funds_{Guid.NewGuid():N}";
        var handler = new StubNportHandler();
        handler.Tickers.Add((36405, "S000002848", "C000007808", "VTI"));
        handler.AddFiling("884394", "0000884394-26-000001", "2026-02-26", "2025-12-31", null, [new Holding("Apple Inc", "EC", 7m)]);
        handler.AddFiling("S000002848", "0000036405-20-000001", "2020-02-27", "2019-12-31", "S000002848",
            [new Holding("Apple Inc", "EC", 4m)], cik: 36405);
        handler.AddFiling("S000002848", "0000036405-26-000001", "2026-02-27", "2025-12-31", "S000002848",
            [new Holding("Apple Inc", "EC", 6m)], cik: 36405);
        await using (AppDbContext db = CreateContext(database))
        {
            await RefreshAndReadAsync(CreateService(db, handler, Spy));
        }

        handler.Requests.Clear();
        await using AppDbContext later = CreateContext(database);
        FundsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(later, handler, Spy, Vti));

        Assert.Equal(["2019 Q4", "2025 Q4"], snapshot.Periods);
        Assert.Equal(["SPY", "VTI"], snapshot.Funds.Select(f => f.Ticker));
        // Only the new fund's two filings were fetched; SPY's came from the filings already read.
        List<string> fundsRequests = FundsRefreshRequests(handler, "CIK=884394");
        Assert.Equal(2, fundsRequests.Count(url => url.EndsWith("primary_doc.xml", StringComparison.Ordinal)));
        Assert.DoesNotContain(fundsRequests, url => url.Contains("000088439426000001", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshAsync_StoppedPartWay_KeepsTheFilingsOfTheFundsAlreadyRead()
    {
        string database = $"Funds_{Guid.NewGuid():N}";
        var handler = new StubNportHandler();
        handler.Tickers.Add((36405, "S000002848", "C000007808", "VTI"));
        handler.AddFiling("884394", "0000884394-26-000001", "2026-02-26", "2025-12-31", null, [new Holding("Apple Inc", "EC", 7m)]);
        handler.AddFiling("S000002848", "0000036405-26-000001", "2026-02-27", "2025-12-31", "S000002848",
            [new Holding("Apple Inc", "EC", 6m)], cik: 36405);
        using var stop = new CancellationTokenSource();
        // The run is stopped (as at the job's time limit) while it reads the second fund's filing.
        handler.OnRequest = url =>
        {
            if (url.Contains("000003640526000001", StringComparison.Ordinal))
            {
                stop.Cancel();
            }
        };
        await using (AppDbContext db = CreateContext(database))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CreateService(db, handler, Spy, Vti).RefreshAsync(cancellationToken: stop.Token));
        }

        handler.OnRequest = null;
        handler.Requests.Clear();
        await using AppDbContext later = CreateContext(database);
        FundsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(later, handler, Spy, Vti));

        Assert.Equal(["SPY", "VTI"], Assert.Single(snapshot.Values).Funds.Select(f => f.Ticker));
        // SPY's filing was saved before the stop, so only VTI's is fetched on the next run.
        List<string> fundsRequests = FundsRefreshRequests(handler, "CIK=884394");
        Assert.Single(fundsRequests, url => url.EndsWith("primary_doc.xml", StringComparison.Ordinal));
        Assert.Contains(fundsRequests, url => url.Contains("000003640526000001", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshAsync_WhenSecHasNoFilings_FailsAndStaysNeverLoaded()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        ResearchService service = CreateService(db, new StubNportHandler(), Spy);

        ResearchRefreshSummary summary = await service.RefreshAsync();

        Assert.Contains(ResearchDatasets.Funds, summary.Failed);
        Assert.Null((await service.GetFundsAsync()).Snapshot);
    }

    [Fact]
    public async Task RefreshAsync_WithNoFundsConfigured_NeitherBuildsNorFailsTheFunds()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");

        ResearchRefreshSummary summary = await CreateService(db, new StubNportHandler()).RefreshAsync();

        Assert.DoesNotContain(ResearchDatasets.Funds, summary.Built);
        Assert.DoesNotContain(ResearchDatasets.Funds, summary.Failed);
    }

    [Fact]
    public async Task RefreshAsync_SendsTheConfiguredUserAgent_OnEveryNportRequest()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        var handler = new StubNportHandler();
        handler.AddFiling("884394", "0000884394-26-000001", "2026-02-26", "2025-12-31", null, [new Holding("Apple Inc", "EC", 7m)]);

        await RefreshAndReadAsync(CreateService(db, handler, Spy));

        Assert.NotEmpty(handler.UserAgents);
        Assert.All(handler.UserAgents, ua => Assert.Equal(UserAgent, ua));
    }

    // ---- Fund overlap ----

    private const string VtiSeries = "S000002848";

    /// <summary>A handler with VTI in SEC's fund ticker list, so its filings are listed under its series.</summary>
    private static StubNportHandler OverlapHandler()
    {
        var handler = new StubNportHandler();
        handler.Tickers.Add((36405, VtiSeries, "C000007808", "VTI"));
        return handler;
    }

    private static void AddSpy(StubNportHandler handler, string accession, string filedOn, string reportDate,
        params Holding[] holdings) =>
        handler.AddFiling("884394", accession, filedOn, reportDate, null, holdings);

    private static void AddVti(StubNportHandler handler, string accession, string filedOn, string reportDate,
        params Holding[] holdings) =>
        handler.AddFiling(VtiSeries, accession, filedOn, reportDate, VtiSeries, holdings, cik: 36405);

    private static async Task<FundOverlapSnapshotDto> RefreshOverlapAsync(ResearchService service)
    {
        ResearchRefreshSummary summary = await service.RefreshAsync();
        Assert.Contains(ResearchDatasets.FundOverlap, summary.Built);
        return (await service.GetFundOverlapAsync()).Snapshot!;
    }

    [Fact]
    public async Task GetFundOverlapAsync_OnAnEmptyTable_ReturnsNeverLoaded()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");

        ResearchSnapshotResult<FundOverlapSnapshotDto> result =
            await CreateService(db, OverlapHandler(), Spy, Vti).GetFundOverlapAsync();

        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task RefreshAsync_BuildsTheOverlapByWeight_TheSumOfTheSmallerWeightOfEachSharedSecurity()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        StubNportHandler handler = OverlapHandler();
        AddSpy(handler, "0000884394-26-000001", "2026-05-28", "2026-03-31",
            new Holding("NVIDIA CORP", "EC", 7m, Cusip: "67066G104"),
            new Holding("Microsoft Corp", "EC", 6m, Cusip: "594918104"),
            new Holding("Apple Inc", "EC", 5m, Cusip: "037833100"),
            new Holding("Exxon Mobil Corp", "EC", 1m, Cusip: "30231G102"));
        AddVti(handler, "0000036405-26-000001", "2026-05-29", "2026-03-31",
            new Holding("NVIDIA CORP", "EC", 6m, Cusip: "67066G104"),
            new Holding("Microsoft Corp", "EC", 5.5m, Cusip: "594918104"),
            new Holding("Apple Inc", "EC", 4m, Cusip: "037833100"),
            new Holding("Bloom Energy", "EC", 0.1m, Cusip: "093712107"));

        FundOverlapSnapshotDto snapshot = await RefreshOverlapAsync(CreateService(db, handler, Spy, Vti));

        Assert.Equal(new DateOnly(2026, 3, 31), snapshot.AsOf);
        Assert.Equal(["SPY", "VTI"], snapshot.Funds.Select(f => f.Ticker));
        Assert.Equal(["0000884394-26-000001", "0000036405-26-000001"], snapshot.Funds.Select(f => f.Accession));
        FundOverlapPairDto pair = Assert.Single(snapshot.Pairs);
        Assert.Equal((0, 1), (pair.A, pair.B));
        Assert.Equal(15.5m, pair.Overlap); // 6 + 5.5 + 4
        Assert.Equal(18m, pair.CoveredA); // SPY's weight in what VTI also holds
        Assert.Equal(15.5m, pair.CoveredB);
        Assert.Equal(3, pair.SharedCount);
        Assert.Equal(["NVIDIA Corp", "Microsoft Corp", "Apple Inc"], pair.Shared.Select(h => snapshot.Holdings[h.Holding]));
        Assert.Equal([(7m, 6m), (6m, 5.5m), (5m, 4m)], pair.Shared.Select(h => (h.WeightA, h.WeightB)));
    }

    [Fact]
    public async Task RefreshAsync_TwoFundsThatHoldTheSameCompaniesInVeryDifferentAmounts_OverlapLittle()
    {
        // The count-vs-weight trap (VTI and SCHD on real data: 97% of SCHD's holdings by count, 7% by weight). Every
        // one of the first fund's holdings is in the second, but at a tiny weight there.
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        StubNportHandler handler = OverlapHandler();
        string[] cusips = ["191216100", "742718109", "717081103", "458140100"];
        AddSpy(handler, "0000884394-26-000001", "2026-05-28", "2026-03-31",
            cusips.Select((c, i) => new Holding($"Dividend Co {i}", "EC", 25m, Cusip: c)).ToArray());
        AddVti(handler, "0000036405-26-000001", "2026-05-29", "2026-03-31",
            cusips.Select((c, i) => new Holding($"Dividend Co {i}", "EC", 0.5m, Cusip: c))
                .Append(new Holding("Everything Else Inc", "EC", 98m, Cusip: "88160R101")).ToArray());

        FundOverlapPairDto pair = Assert.Single((await RefreshOverlapAsync(CreateService(db, handler, Spy, Vti))).Pairs);

        Assert.Equal(4, pair.SharedCount);
        Assert.Equal(2m, pair.Overlap);
        Assert.Equal(100m, pair.CoveredA);
    }

    [Fact]
    public async Task RefreshAsync_MatchesByIsinElseCusip_SumsRepeatedLines_AndLeavesOutCashCollateralAndUnidentifiedHoldings()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        StubNportHandler handler = OverlapHandler();
        AddSpy(handler, "0000884394-26-000001", "2026-05-28", "2026-03-31",
            // Matched by ISIN although the CUSIPs as reported differ.
            new Holding("Shell Plc", "EC", 2m, Cusip: "780259305", Isin: "GB00BP6MXD84"),
            // One security in two lines: 1 + 2.
            new Holding("Apple Inc", "EC", 1m, Cusip: "037833100"),
            new Holding("Apple Inc", "EC", 2m, Cusip: "037833100"),
            new Holding("State Street Navigator Securities Lending", "STIV", 4m, Cusip: "857492706", CashCollateral: true),
            new Holding("Unidentified Swap", "OTHER", 3m));
        AddVti(handler, "0000036405-26-000001", "2026-05-29", "2026-03-31",
            new Holding("Shell Plc", "EC", 1m, Cusip: "G80827101", Isin: "GB00BP6MXD84"),
            new Holding("Apple Inc", "EC", 5m, Cusip: "037833100"),
            new Holding("State Street Navigator Securities Lending", "STIV", 4m, Cusip: "857492706", CashCollateral: true),
            new Holding("Unidentified Swap", "OTHER", 3m));

        FundOverlapSnapshotDto snapshot = await RefreshOverlapAsync(CreateService(db, handler, Spy, Vti));

        FundOverlapPairDto pair = Assert.Single(snapshot.Pairs);
        Assert.Equal(2, pair.SharedCount);
        Assert.Equal(4m, pair.Overlap); // Apple min(3, 5) + Shell min(2, 1)
        Assert.Equal(["Apple Inc", "Shell Plc"], pair.Shared.Select(h => snapshot.Holdings[h.Holding]));
        Assert.Equal(3m, pair.Shared[0].WeightA);
    }

    [Fact]
    public async Task RefreshAsync_KeepsTwoSecuritiesWithOneNameApart_SuchAsTwoShareClasses()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        StubNportHandler handler = OverlapHandler();
        Holding[] alphabet =
        [
            new Holding("Alphabet Inc", "EC", 2m, Cusip: "02079K305"),
            new Holding("Alphabet Inc", "EC", 1.5m, Cusip: "02079K107"),
        ];
        AddSpy(handler, "0000884394-26-000001", "2026-05-28", "2026-03-31", alphabet);
        AddVti(handler, "0000036405-26-000001", "2026-05-29", "2026-03-31", alphabet);

        FundOverlapSnapshotDto snapshot = await RefreshOverlapAsync(CreateService(db, handler, Spy, Vti));

        FundOverlapPairDto pair = Assert.Single(snapshot.Pairs);
        Assert.Equal(2, pair.SharedCount);
        Assert.Equal(3.5m, pair.Overlap);
        Assert.Equal(["Alphabet Inc", "Alphabet Inc"], snapshot.Holdings);
        Assert.Equal([0, 1], pair.Shared.Select(h => h.Holding));
    }

    [Fact]
    public async Task RefreshAsync_UsesEachFundsLatestReport_AndRebuildsTheOverlapOnlyWhenAFundHasFiledANewerOne()
    {
        string database = $"Funds_{Guid.NewGuid():N}";
        StubNportHandler handler = OverlapHandler();
        AddSpy(handler, "0000884394-26-000001", "2026-02-26", "2025-12-31", new Holding("Apple Inc", "EC", 9m, Cusip: "037833100"));
        AddSpy(handler, "0000884394-26-000002", "2026-05-28", "2026-03-31", new Holding("Apple Inc", "EC", 7m, Cusip: "037833100"));
        AddVti(handler, "0000036405-26-000001", "2026-05-29", "2026-03-31", new Holding("Apple Inc", "EC", 6m, Cusip: "037833100"));
        await using (AppDbContext db = CreateContext(database))
        {
            FundOverlapSnapshotDto first = await RefreshOverlapAsync(CreateService(db, handler, Spy, Vti));
            Assert.Equal(6m, Assert.Single(first.Pairs).Overlap);
            Assert.Equal("0000884394-26-000002", first.Funds[0].Accession);
        }

        // Nothing new: the stored overlap is kept and no report is downloaded.
        handler.Requests.Clear();
        await using (AppDbContext db = CreateContext(database))
        {
            await CreateService(db, handler, Spy, Vti).RefreshAsync();
        }
        Assert.DoesNotContain(handler.Requests, url => url.EndsWith("primary_doc.xml", StringComparison.Ordinal));

        // SPY files a newer report: the overlap is rebuilt from it and VTI's latest.
        AddSpy(handler, "0000884394-26-000003", "2026-08-27", "2026-06-30", new Holding("Apple Inc", "EC", 5m, Cusip: "037833100"));
        await using AppDbContext later = CreateContext(database);
        FundOverlapSnapshotDto rebuilt = await RefreshOverlapAsync(CreateService(later, handler, Spy, Vti));

        Assert.Equal(5m, Assert.Single(rebuilt.Pairs).Overlap);
        Assert.Equal(["0000884394-26-000003", "0000036405-26-000001"], rebuilt.Funds.Select(f => f.Accession));
        Assert.Equal(new DateOnly(2026, 6, 30), rebuilt.AsOf);
    }

    [Fact]
    public async Task RefreshAsync_WithFewerThanTwoReadableFunds_FailsTheOverlapAndLeavesItNeverLoaded()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        StubNportHandler handler = OverlapHandler();
        AddSpy(handler, "0000884394-26-000001", "2026-05-28", "2026-03-31", new Holding("Apple Inc", "EC", 7m, Cusip: "037833100"));
        AddVti(handler, "0000036405-26-000001", "2026-05-29", "2026-03-31", new Holding("Apple Inc", "EC", 6m, Cusip: "037833100"));
        handler.FailingDocuments.Add("0000036405-26-000001");
        ResearchService service = CreateService(db, handler, Spy, Vti);

        ResearchRefreshSummary summary = await service.RefreshAsync();

        Assert.Contains(ResearchDatasets.FundOverlap, summary.Failed);
        Assert.Null((await service.GetFundOverlapAsync()).Snapshot);
    }

    [Fact]
    public async Task RefreshAsync_NeverStoresLogsOrReturnsASecurityIdentifier()
    {
        await using AppDbContext db = CreateContext($"Funds_{Guid.NewGuid():N}");
        StubNportHandler handler = OverlapHandler();
        Holding[] holdings =
        [
            new Holding("Apple Inc", "EC", 7m, Cusip: "037833100", Isin: "US0378331005"),
            new Holding("Shell Plc", "EC", 2m, Cusip: "780259305", Isin: "GB00BP6MXD84"),
            new Holding("Microsoft Corp", "EC", 6m, Cusip: "594918104"),
        ];
        AddSpy(handler, "0000884394-26-000001", "2026-05-28", "2026-03-31", holdings);
        AddVti(handler, "0000036405-26-000001", "2026-05-29", "2026-03-31", holdings);
        // VTI's older report fails, so warnings are logged on the way.
        AddVti(handler, "0000036405-26-000000", "2026-02-27", "2025-12-31", holdings);
        handler.FailingDocuments.Add("0000036405-26-000000");
        var logs = new AllLogs();
        ResearchService service = CreateService(db, handler, logs, Spy, Vti);

        FundOverlapSnapshotDto overlap = await RefreshOverlapAsync(service);
        FundsSnapshotDto funds = (await service.GetFundsAsync()).Snapshot!;

        List<string> texts = await db.ResearchSnapshots.AsNoTracking().Select(r => r.Payload).ToListAsync();
        texts.Add(JsonSerializer.Serialize(overlap, JsonSerializerOptions.Web));
        texts.Add(JsonSerializer.Serialize(funds, JsonSerializerOptions.Web));
        Assert.NotEmpty(logs.Messages);
        texts.AddRange(logs.Messages);
        string[] identifiers = holdings.SelectMany(h => new[] { h.Cusip, h.Isin }).OfType<string>().ToArray();
        var cusipShaped = new Regex(@"\b[0-9A-Z]{8}[0-9]\b");
        var isinShaped = new Regex(@"\b[A-Z]{2}[0-9A-Z]{9}[0-9]\b");
        Assert.All(texts, text =>
        {
            Assert.DoesNotContain(identifiers, text.Contains);
            Assert.False(cusipShaped.IsMatch(text), "A CUSIP-shaped value was stored, logged or returned.");
            Assert.False(isinShaped.IsMatch(text), "An ISIN-shaped value was stored, logged or returned.");
        });
    }

    /// <summary>Records the message of every log entry from this solution's code.</summary>
    private sealed class AllLogs : ILoggerProvider
    {
        private readonly RecordingLogger<AllLogs> _logger = new();

        public List<string> Messages => _logger.Entries
            .Select(e => e.Exception is null ? e.Message : $"{e.Message} {e.Exception}")
            .ToList();

        public ILogger CreateLogger(string categoryName) =>
            categoryName.StartsWith("PersonalFinance", StringComparison.Ordinal) ? _logger : NullLogger.Instance;

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// A holding of a canned N-PORT filing: its name, asset category code and % of net assets, with its CUSIP and ISIN
    /// when it has them and whether it is securities-lending cash collateral.
    /// </summary>
    private sealed record Holding(string Name, string Category, decimal Percent, string? Cusip = null,
        string? Isin = null, bool CashCollateral = false);

    private sealed record Filing(string ListedUnder, string Accession, string FiledOn, string FormType, long Cik,
        string Document);

    /// <summary>
    /// Answers SEC's fund ticker list, EDGAR's filing lists (atom, by CIK or series id) and N-PORT primary documents
    /// from canned data; anything else is a 404.
    /// </summary>
    private sealed class StubNportHandler : HttpMessageHandler
    {
        private const string TickerListUrl = "https://www.sec.gov/files/company_tickers_mf.json";
        private const string BrowsePrefix = "https://www.sec.gov/cgi-bin/browse-edgar?";

        private readonly List<Filing> _filings = [];

        public List<(long Cik, string SeriesId, string ClassId, string Ticker)> Tickers { get; } = [];
        public HashSet<string> FailingDocuments { get; } = [];
        public List<string> Requests { get; } = [];
        public List<string> UserAgents { get; } = [];

        /// <summary>Called with each request's URL before it is answered, e.g. to stop the run part-way.</summary>
        public Action<string>? OnRequest { get; set; }

        public void AddFiling(string listedUnder, string accession, string filedOn, string reportDate, string? seriesId,
            IReadOnlyList<Holding> holdings, string formType = "NPORT-P", long cik = 884394) =>
            _filings.Add(new Filing(listedUnder, accession, filedOn, formType, cik,
                NportDocument(reportDate, seriesId, holdings)));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            Requests.Add(url);
            UserAgents.Add(request.Headers.UserAgent.ToString());
            OnRequest?.Invoke(url);
            cancellationToken.ThrowIfCancellationRequested();
            if (url == TickerListUrl)
            {
                var body = new
                {
                    fields = new[] { "cik", "seriesId", "classId", "symbol" },
                    data = Tickers.Select(t => new object[] { t.Cik, t.SeriesId, t.ClassId, t.Ticker }),
                };
                return Respond(JsonSerializer.Serialize(body), "application/json");
            }

            if (url.StartsWith(BrowsePrefix, StringComparison.Ordinal))
            {
                return Respond(FilingList(url), "application/atom+xml");
            }

            Filing? filing = _filings.FirstOrDefault(f => url == DocumentUrl(f));
            if (filing is not null && FailingDocuments.Contains(filing.Accession))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            return filing is null
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))
                : Respond(filing.Document, "text/xml");
        }

        private static Task<HttpResponseMessage> Respond(string body, string mediaType) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType)
            });

        private static string DocumentUrl(Filing f) => string.Create(CultureInfo.InvariantCulture,
            $"https://www.sec.gov/Archives/edgar/data/{f.Cik}/{f.Accession.Replace("-", string.Empty, StringComparison.Ordinal)}/primary_doc.xml");

        /// <summary>EDGAR's atom list of the N-PORT filings listed under the CIK or series id in the URL, newest first.</summary>
        private string FilingList(string url)
        {
            string id = url.Split('&').Single(p => p.StartsWith("CIK=", StringComparison.Ordinal))[4..];
            IEnumerable<string> entries = _filings
                .Where(f => f.ListedUnder.TrimStart('0') == id.TrimStart('0'))
                .OrderByDescending(f => f.FiledOn, StringComparer.Ordinal)
                .Select(f => $"""
                    <entry>
                      <category label="form type" scheme="https://www.sec.gov/" term="{f.FormType}" />
                      <content type="text/xml">
                        <accession-number>{f.Accession}</accession-number>
                        <filing-date>{f.FiledOn}</filing-date>
                        <filing-href>https://www.sec.gov/Archives/edgar/data/{f.Cik}/{f.Accession.Replace("-", string.Empty, StringComparison.Ordinal)}/{f.Accession}-index.htm</filing-href>
                        <filing-type>{f.FormType}</filing-type>
                      </content>
                    </entry>
                    """);
            return $"""
                <?xml version="1.0" encoding="ISO-8859-1" ?>
                <feed xmlns="http://www.w3.org/2005/Atom">
                  <company-info><cik>0000000001</cik></company-info>
                  {string.Join('\n', entries)}
                </feed>
                """;
        }

        /// <summary>A Form N-PORT primary document as EDGAR serves it, cut down to the fields the snapshot reads.</summary>
        private static string NportDocument(string reportDate, string? seriesId, IReadOnlyList<Holding> holdings)
        {
            string series = seriesId is null ? string.Empty : $"<seriesClassInfo><seriesId>{seriesId}</seriesId></seriesClassInfo>";
            IEnumerable<string> rows = holdings.Select(h =>
            {
                string category = h.Category == "OTHER"
                    ? """<assetConditional assetCat="OTHER" desc="Swap"/>"""
                    : $"<assetCat>{h.Category}</assetCat>";
                return string.Create(CultureInfo.InvariantCulture, $"""
                    <invstOrSec>
                      <name>{SecurityElement.Escape(h.Name)}</name>
                      <title>{SecurityElement.Escape(h.Name)}</title>
                      <cusip>{h.Cusip ?? "000000000"}</cusip>
                      {(h.Isin is null ? string.Empty : $"<identifiers><isin value=\"{h.Isin}\"/></identifiers>")}
                      <valUSD>{h.Percent * 1000m}</valUSD>
                      <pctVal>{h.Percent}</pctVal>
                      <payoffProfile>Long</payoffProfile>
                      {category}
                      <issuerCat>CORP</issuerCat>
                      <securityLending><isCashCollateral>{(h.CashCollateral ? "Y" : "N")}</isCashCollateral><isNonCashCollateral>N</isNonCashCollateral><isLoanByFund>N</isLoanByFund></securityLending>
                    </invstOrSec>
                    """);
            });
            return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <edgarSubmission xmlns="http://www.sec.gov/edgar/nport" xmlns:com="http://www.sec.gov/edgar/common">
                  <headerData><submissionType>NPORT-P</submissionType>{series}</headerData>
                  <formData>
                    <genInfo><regName>Test Trust</regName><seriesName>N/A</seriesName><repPdEnd>2026-09-30</repPdEnd><repPdDate>{reportDate}</repPdDate></genInfo>
                    <fundInfo><totAssets>1000000</totAssets><netAssets>100000</netAssets></fundInfo>
                    <invstOrSecs>
                      {string.Join('\n', rows)}
                    </invstOrSecs>
                  </formData>
                </edgarSubmission>
                """;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
