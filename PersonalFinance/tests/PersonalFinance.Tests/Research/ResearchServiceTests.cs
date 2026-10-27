using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
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
/// The Research Feature's Sectors snapshot, built through the real SEC EDGAR client over a stub HTTP handler and
/// stored in an InMemory database: the largest filers by Public Float per year, the rest summed, and the previous
/// snapshot kept when a refresh fails.
/// </summary>
public class ResearchServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateContext(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static ResearchService CreateService(AppDbContext db, StubSecHandler handler, int firstYear = 2024,
        IReadOnlyList<FilerYear>? exclusions = null, ILoggerProvider? logs = null,
        IReadOnlyList<FilerYear>? keeps = null)
    {
        var settings = new SecEdgarSettings { UserAgent = "PersonalFinance tests contact@example.com", MaxRequestsPerSecond = 1000 };
        var timeProvider = new FixedTimeProvider(Now);
        SecEdgarClient client = ResearchServiceFactory.SecClient(handler, settings);
        var research = new ResearchSettings
        {
            SectorsFirstYear = firstYear, PublicFloatExclusions = exclusions ?? [], PublicFloatKeeps = keeps ?? [],
        };
        // The World Bank is down here: these tests are about the Sectors dataset, which is built on its own.
        var worldClient = new WorldBankClient(new HttpClient(new DelegatingTestHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))));
        return ResearchServiceFactory.Create(db, client, worldClient, research, timeProvider, logs);
    }

    [Fact]
    public async Task RefreshAsync_TakesEachFilersLatestQuarterlyFrame_SoAFilerWhoseFiscalQ2IsNotCalendarQ2IsIncluded()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        var handler = new StubSecHandler();
        // Apple-like filer: September fiscal year, so its fiscal Q2 ends in March (calendar Q1). Values stay under
        // the 1 trillion a filer with no other year may have.
        handler.Frames["CY2025Q1I"] = [new Filer(320193, "APPLE INC", "2025-03-28", 900_000_000_000m)];
        handler.Frames["CY2025Q2I"] =
        [
            new Filer(789019, "MICROSOFT CORP", "2025-06-30", 950_000_000_000m),
            new Filer(1, "TWO FRAMES CO", "2025-06-30", 10_000_000m),
        ];
        // A later value for the same filer in the same year replaces the earlier one.
        handler.Frames["CY2025Q4I"] = [new Filer(1, "TWO FRAMES CO", "2025-12-31", 20_000_000m)];

        ResearchService service = CreateService(db, handler, firstYear: 2025);
        ResearchRefreshSummary summary = await service.RefreshAsync();
        SectorsSnapshotDto snapshot = (await service.GetSectorsAsync()).Snapshot!;

        Assert.Contains("sectors", summary.Built);
        Assert.DoesNotContain("sectors", summary.Failed);
        Assert.Equal(["2025"], snapshot.Periods);
        SectorsPeriodDto year = Assert.Single(snapshot.Values);
        Assert.Equal(
            [("Microsoft Corp", 950_000_000_000m), ("Apple Inc", 900_000_000_000m), ("Two Frames Co", 20_000_000m)],
            year.Ranked.Select(r => (snapshot.Companies[r.Company].Name, r.PublicFloat)));
        Assert.Equal(0m, year.RestTotal);
        Assert.Equal(0, year.RestCount);
        Assert.Equal(new DateOnly(2025, 12, 31), snapshot.AsOf);
    }

    [Fact]
    public async Task RefreshAsync_CoversEveryYearFromTheFirstToLastYear_LeavingOutYearsWithNoFilers()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        var handler = new StubSecHandler();
        handler.Frames["CY2023Q2I"] = [new Filer(1, "ALPHA", "2023-06-30", 10m)];
        handler.Frames["CY2025Q2I"] = [new Filer(1, "ALPHA RENAMED", "2025-06-30", 30m)];
        handler.Frames["CY2026Q2I"] = [new Filer(1, "ALPHA RENAMED", "2026-06-30", 99m)]; // this year: still filing

        ResearchService service = CreateService(db, handler, firstYear: 2023);
        await service.RefreshAsync();
        SectorsSnapshotDto snapshot = (await service.GetSectorsAsync()).Snapshot!;

        Assert.Equal(["2023", "2025"], snapshot.Periods);
        Assert.Equal(["2023", "2025"], snapshot.Values.Select(v => v.Period));
        Assert.Equal("Alpha Renamed", Assert.Single(snapshot.Companies).Name);
        Assert.Equal(new DateOnly(2025, 6, 30), snapshot.AsOf);
    }

    [Fact]
    public async Task RefreshAsync_RanksEqualValuesByCik_SoReadsAreDeterministic()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        var handler = new StubSecHandler();
        handler.Frames["CY2025Q2I"] =
        [
            new Filer(30, "THIRTY", "2025-06-30", 5m),
            new Filer(10, "TEN", "2025-06-30", 5m),
            new Filer(20, "TWENTY", "2025-06-30", 5m),
        ];

        ResearchService service = CreateService(db, handler, firstYear: 2025);
        await service.RefreshAsync();
        SectorsSnapshotDto snapshot = (await service.GetSectorsAsync()).Snapshot!;

        Assert.Equal(["Ten", "Twenty", "Thirty"],
            snapshot.Values[0].Ranked.Select(r => snapshot.Companies[r.Company].Name));
        Assert.Equal([10L, 20L, 30L], snapshot.Companies.Select(c => c.Cik));
    }

    [Fact]
    public async Task RefreshAsync_WhenSecFails_KeepsThePreviousSnapshotAndItsAsOfDate()
    {
        string database = $"Research_{Guid.NewGuid():N}";
        var handler = new StubSecHandler();
        handler.Frames["CY2025Q2I"] = [new Filer(1, "ALPHA", "2025-06-30", 10m)];
        await using (AppDbContext db = CreateContext(database))
        {
            await CreateService(db, handler, firstYear: 2025).RefreshAsync();
        }

        handler.Fail = true;
        await using AppDbContext later = CreateContext(database);
        ResearchService service = CreateService(later, handler, firstYear: 2025);
        ResearchRefreshSummary summary = await service.RefreshAsync();
        ResearchSnapshotResult<SectorsSnapshotDto> result = await service.GetSectorsAsync();

        Assert.Contains("sectors", summary.Failed);
        Assert.DoesNotContain("sectors", summary.Built);
        Assert.Equal(new DateOnly(2025, 6, 30), result.Snapshot!.AsOf);
        Assert.Equal(10m, Assert.Single(result.Snapshot.Values[0].Ranked).PublicFloat);
    }

    [Fact]
    public async Task RefreshAsync_WhenSecHasNoData_StaysNeverLoaded()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");

        ResearchService service = CreateService(db, new StubSecHandler(), firstYear: 2025);
        ResearchRefreshSummary summary = await service.RefreshAsync();

        Assert.Contains("sectors", summary.Failed);
        Assert.Null((await service.GetSectorsAsync()).Snapshot);
    }

    [Fact]
    public async Task RefreshAsync_SendsTheConfiguredUserAgent_OnEveryRequest()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        var handler = new StubSecHandler();
        handler.Frames["CY2025Q2I"] = [new Filer(1, "ALPHA", "2025-06-30", 10m)];

        await CreateService(db, handler, firstYear: 2025).RefreshAsync();

        Assert.NotEmpty(handler.UserAgents);
        Assert.All(handler.UserAgents, ua => Assert.Equal("PersonalFinance tests contact@example.com", ua));
    }

    [Fact]
    public async Task RefreshAsync_DropsAValueOver200TimesTheFilersTotalAssets_AndKeepsAFastGrowingCompany()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        StubSecHandler handler = HandlerWithNvidia();
        // Reports far too high every year, so its other years do not give it away.
        AddFloat(handler, 2024, new Filer(1, "INFLATED CO", "2024-06-30", 680_000_000_000m));
        AddFloat(handler, 2025, new Filer(1, "INFLATED CO", "2025-06-30", 700_000_000_000m));
        AddAssets(handler, 2024, new Filer(1, "INFLATED CO", "2024-06-30", 3_500_000_000m)); // 194x: kept
        AddAssets(handler, 2025, new Filer(1, "INFLATED CO", "2025-06-30", 3_000_000_000m)); // 233x: dropped

        SectorsSnapshotDto snapshot = await RefreshAndReadAsync(db, handler);

        AssertNvidiaKept(snapshot);
        Assert.Equal([680_000_000_000m], ValuesOf(snapshot, 1));
    }

    [Fact]
    public async Task RefreshAsync_DropsAValueOver30TimesTheFilersNearestOtherYear_AndKeepsAFastGrowingCompany()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        StubSecHandler handler = HandlerWithNvidia();
        // One year about 1000x too high; SEC has no total assets for it.
        AddFloat(handler, 2023, new Filer(2, "ONE BAD YEAR CO", "2023-06-30", 9_000_000_000m));
        AddFloat(handler, 2024, new Filer(2, "ONE BAD YEAR CO", "2024-06-30", 4_750_000_000_000m));
        AddFloat(handler, 2025, new Filer(2, "ONE BAD YEAR CO", "2025-06-30", 10_000_000_000m));
        // Grew 29x in a year: kept.
        AddFloat(handler, 2024, new Filer(3, "BREAKOUT CO", "2024-06-30", 1_000_000_000m));
        AddFloat(handler, 2025, new Filer(3, "BREAKOUT CO", "2025-06-30", 29_000_000_000m));

        SectorsSnapshotDto snapshot = await RefreshAndReadAsync(db, handler);

        AssertNvidiaKept(snapshot);
        Assert.Equal([9_000_000_000m, 10_000_000_000m], ValuesOf(snapshot, 2));
        Assert.Equal([1_000_000_000m, 29_000_000_000m], ValuesOf(snapshot, 3));
    }

    [Fact]
    public async Task RefreshAsync_TreatsAYearReportedAsZeroAsNoOtherYear()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        StubSecHandler handler = HandlerWithNvidia();
        // SEC holds a zero for years a filer reported no Public Float, e.g. the year it merged into a new CIK.
        AddFloat(handler, 2024, new Filer(8, "NEWLY MERGED CO", "2024-06-30", 0m));
        AddFloat(handler, 2025, new Filer(8, "NEWLY MERGED CO", "2025-06-30", 7_400_000_000m));
        AddFloat(handler, 2024, new Filer(9, "ZERO THEN GIANT CO", "2024-06-30", 0m));
        AddFloat(handler, 2025, new Filer(9, "ZERO THEN GIANT CO", "2025-06-30", 1_500_000_000_000m));

        SectorsSnapshotDto snapshot = await RefreshAndReadAsync(db, handler);

        AssertNvidiaKept(snapshot);
        Assert.Equal([0m, 7_400_000_000m], ValuesOf(snapshot, 8));
        Assert.Equal([0m], ValuesOf(snapshot, 9));
    }

    [Fact]
    public async Task RefreshAsync_DropsAValueOver6Trillion_AndKeepsAFastGrowingCompany()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        StubSecHandler handler = HandlerWithNvidia();
        // Over 6 trillion every year, steady, and SEC has no total assets for it: nothing else gives it away.
        AddFloat(handler, 2024, new Filer(4, "SIX TRILLION CO", "2024-06-30", 6_000_000_000_000m)); // kept
        AddFloat(handler, 2025, new Filer(4, "SIX TRILLION CO", "2025-06-30", 6_800_000_000_000m)); // dropped

        SectorsSnapshotDto snapshot = await RefreshAndReadAsync(db, handler);

        AssertNvidiaKept(snapshot);
        Assert.Equal([6_000_000_000_000m], ValuesOf(snapshot, 4));
    }

    [Fact]
    public async Task RefreshAsync_DropsAValueOver1TrillionFromAFilerWithNoOtherYear_AndKeepsAFastGrowingCompany()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        StubSecHandler handler = HandlerWithNvidia();
        // Filers SEC has in one year only, with no total assets.
        AddFloat(handler, 2025, new Filer(5, "ONE YEAR GIANT CO", "2025-06-30", 1_200_000_000_000m)); // dropped
        AddFloat(handler, 2025, new Filer(6, "ONE YEAR LARGE CO", "2025-06-30", 1_000_000_000_000m)); // kept

        SectorsSnapshotDto snapshot = await RefreshAndReadAsync(db, handler);

        AssertNvidiaKept(snapshot);
        Assert.Empty(ValuesOf(snapshot, 5));
        Assert.Equal([1_000_000_000_000m], ValuesOf(snapshot, 6));
    }

    [Fact]
    public async Task RefreshAsync_DropsAFilerYearOnTheExclusionList_AndKeepsAFastGrowingCompany()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        StubSecHandler handler = HandlerWithNvidia();
        // Too high in 2024 but within every rule: only the exclusion list catches it.
        AddFloat(handler, 2023, new Filer(7, "HAND PICKED CO", "2023-06-30", 10_000_000_000m));
        AddFloat(handler, 2024, new Filer(7, "HAND PICKED CO", "2024-06-30", 250_000_000_000m));
        AddFloat(handler, 2025, new Filer(7, "HAND PICKED CO", "2025-06-30", 11_000_000_000m));

        SectorsSnapshotDto snapshot = await RefreshAndReadAsync(db, handler, exclusions: [new FilerYear(7, 2024)]);

        AssertNvidiaKept(snapshot);
        Assert.Equal([10_000_000_000m, 11_000_000_000m], ValuesOf(snapshot, 7));
    }

    [Fact]
    public async Task RefreshAsync_KeepsAFilerYearOnTheKeepList_ThatGrewOver30Times()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        StubSecHandler handler = HandlerWithNvidia();
        // A real company that grew 40x in a year (e.g. a spin-off's first full year): only the keep list saves it.
        AddFloat(handler, 2024, new Filer(10, "SPUN OFF CO", "2024-06-30", 1_000_000_000m));
        AddFloat(handler, 2025, new Filer(10, "SPUN OFF CO", "2025-06-30", 40_000_000_000m));
        // The same growth, not on the list: dropped.
        AddFloat(handler, 2024, new Filer(12, "NOT LISTED CO", "2024-06-30", 1_000_000_000m));
        AddFloat(handler, 2025, new Filer(12, "NOT LISTED CO", "2025-06-30", 40_000_000_000m));

        SectorsSnapshotDto snapshot = await RefreshAndReadAsync(db, handler, keeps: [new FilerYear(10, 2025)]);

        AssertNvidiaKept(snapshot);
        Assert.Equal([1_000_000_000m, 40_000_000_000m], ValuesOf(snapshot, 10));
        Assert.Equal([1_000_000_000m], ValuesOf(snapshot, 12));
    }

    [Fact]
    public async Task RefreshAsync_StillDropsAFilerYearOnTheKeepList_ThatIsOver200TimesItsTotalAssets()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        StubSecHandler handler = HandlerWithNvidia();
        AddFloat(handler, 2024, new Filer(11, "KEPT BUT INFLATED CO", "2024-06-30", 1_000_000_000m));
        AddFloat(handler, 2025, new Filer(11, "KEPT BUT INFLATED CO", "2025-06-30", 40_000_000_000m));
        AddAssets(handler, 2025, new Filer(11, "KEPT BUT INFLATED CO", "2025-06-30", 100_000_000m)); // 400x

        SectorsSnapshotDto snapshot = await RefreshAndReadAsync(db, handler, keeps: [new FilerYear(11, 2025)]);

        AssertNvidiaKept(snapshot);
        Assert.Equal([1_000_000_000m], ValuesOf(snapshot, 11));
    }

    [Fact]
    public async Task RefreshAsync_LogsEachDroppedValueByFilerAndYearOnly()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        StubSecHandler handler = HandlerWithNvidia();
        AddFloat(handler, 2025, new Filer(4321, "SECRETIVE NAME CO", "2025-06-30", 6_800_000_000_000m));
        var logs = new SectorsLogs();

        await RefreshAndReadAsync(db, handler, logs: logs);

        // Only the Sectors refresher's entries: the World dataset fails here (the stub World Bank is down) and logs
        // on its own.
        string entry = Assert.Single(logs.Sectors.Entries).Message;
        Assert.Contains("4321", entry);
        Assert.Contains("2025", entry);
        Assert.DoesNotContain("SECRETIVE", entry);
        Assert.DoesNotContain("6800", entry);
        Assert.DoesNotContain("6,800", entry);
    }

    [Fact]
    public void SnapshotPayloadQuery_TranslatesToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();

        string sql = AzureSqlTranslation.AssertTranslates(ResearchSnapshotStore.PayloadQuery(db, "sectors"));

        Assert.Contains("ResearchSnapshots", sql);
    }

    private const long NvidiaCik = 1045810;

    /// <summary>
    /// A stub holding NVIDIA as SEC reports it: Public Float nearly tripling from July 2023 to July 2024 and rising
    /// again by July 2025, at 23 to 44 times its total assets of the quarter before. Every cleaning rule must keep it.
    /// </summary>
    private static StubSecHandler HandlerWithNvidia()
    {
        var handler = new StubSecHandler();
        AddFloat(handler, 2023, new Filer(NvidiaCik, "NVIDIA CORP", "2023-07-28", 1_040_000_000_000m));
        AddFloat(handler, 2024, new Filer(NvidiaCik, "NVIDIA CORP", "2024-07-26", 2_900_000_000_000m));
        AddFloat(handler, 2025, new Filer(NvidiaCik, "NVIDIA CORP", "2025-07-25", 4_100_000_000_000m));
        AddAssets(handler, 2023, new Filer(NvidiaCik, "NVIDIA CORP", "2023-04-30", 44_460_000_000m));
        AddAssets(handler, 2024, new Filer(NvidiaCik, "NVIDIA CORP", "2024-04-28", 65_728_000_000m));
        AddAssets(handler, 2025, new Filer(NvidiaCik, "NVIDIA CORP", "2025-04-27", 125_254_000_000m));
        return handler;
    }

    /// <summary>Adds a filer to the Public Float frame of the calendar quarter its date falls in.</summary>
    private static void AddFloat(StubSecHandler handler, int year, Filer filer) => Add(handler.Frames, year, filer);

    /// <summary>Adds a filer to the total-assets frame of the calendar quarter its date falls in.</summary>
    private static void AddAssets(StubSecHandler handler, int year, Filer filer) =>
        Add(handler.AssetFrames, year, filer);

    private static void Add(Dictionary<string, List<Filer>> frames, int year, Filer filer)
    {
        int quarter = (DateOnly.ParseExact(filer.End, "yyyy-MM-dd", CultureInfo.InvariantCulture).Month + 2) / 3;
        string key = string.Create(CultureInfo.InvariantCulture, $"CY{year}Q{quarter}I");
        if (!frames.TryGetValue(key, out List<Filer>? filers))
        {
            filers = [];
            frames[key] = filers;
        }

        filers.Add(filer);
    }

    /// <summary>Refreshes 2023 to 2025 and reads the stored snapshot back.</summary>
    private static async Task<SectorsSnapshotDto> RefreshAndReadAsync(AppDbContext db, StubSecHandler handler,
        IReadOnlyList<FilerYear>? exclusions = null, ILoggerProvider? logs = null,
        IReadOnlyList<FilerYear>? keeps = null)
    {
        ResearchService service = CreateService(db, handler, firstYear: 2023, exclusions, logs, keeps);
        ResearchRefreshSummary summary = await service.RefreshAsync();
        Assert.Contains("sectors", summary.Built);
        return (await service.GetSectorsAsync()).Snapshot!;
    }

    /// <summary>One company's ranked Public Float in each period it is ranked in, oldest first.</summary>
    private static List<decimal> ValuesOf(SectorsSnapshotDto snapshot, long cik)
    {
        int index = snapshot.Companies.FindIndex(c => c.Cik == cik);
        return snapshot.Values
            .SelectMany(p => p.Ranked.Where(r => r.Company == index).Select(r => r.PublicFloat))
            .ToList();
    }

    private static void AssertNvidiaKept(SectorsSnapshotDto snapshot) =>
        Assert.Equal([1_040_000_000_000m, 2_900_000_000_000m, 4_100_000_000_000m], ValuesOf(snapshot, NvidiaCik));

    /// <summary>A filer's row in a <c>dei:EntityPublicFloat</c> or <c>us-gaap:Assets</c> frame.</summary>
    private sealed record Filer(long Cik, string Name, string End, decimal Value);

    /// <summary>
    /// Answers the SEC frames API from canned frames keyed by <c>CY{year}Q{quarter}I</c>; a frame with no entry is a
    /// 404, as SEC answers for a frame that does not exist.
    /// </summary>
    private sealed class StubSecHandler : HttpMessageHandler
    {
        private const string FramePrefix = "https://data.sec.gov/api/xbrl/frames/dei/EntityPublicFloat/USD/";
        private const string AssetsPrefix = "https://data.sec.gov/api/xbrl/frames/us-gaap/Assets/USD/";

        /// <summary>The <c>dei:EntityPublicFloat</c> frames.</summary>
        public Dictionary<string, List<Filer>> Frames { get; } = new();

        /// <summary>The <c>us-gaap:Assets</c> frames.</summary>
        public Dictionary<string, List<Filer>> AssetFrames { get; } = new();
        public bool Fail { get; set; }
        public List<string?> UserAgents { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            UserAgents.Add(request.Headers.UserAgent.ToString());
            if (Fail)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            string url = request.RequestUri!.ToString();
            (string prefix, Dictionary<string, List<Filer>> frames, string taxonomy, string tag) =
                url.StartsWith(AssetsPrefix, StringComparison.Ordinal)
                    ? (AssetsPrefix, AssetFrames, "us-gaap", "Assets")
                    : (FramePrefix, Frames, "dei", "EntityPublicFloat");
            string frame = url.StartsWith(prefix, StringComparison.Ordinal)
                ? url[prefix.Length..].Replace(".json", string.Empty, StringComparison.Ordinal)
                : string.Empty;
            if (!frames.TryGetValue(frame, out List<Filer>? filers))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var body = new
            {
                taxonomy,
                tag,
                ccp = frame,
                uom = "USD",
                data = filers.Select(f => new
                {
                    accn = $"0000000000-26-{f.Cik:000000}",
                    cik = f.Cik,
                    entityName = f.Name,
                    loc = "US-CA",
                    end = f.End,
                    val = f.Value,
                }),
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>Records the Sectors refresher's log entries; every other category goes nowhere.</summary>
    private sealed class SectorsLogs : ILoggerProvider
    {
        public RecordingLogger<SectorsRefresher> Sectors { get; } = new();

        public ILogger CreateLogger(string categoryName) =>
            categoryName == typeof(SectorsRefresher).FullName ? Sectors : NullLogger.Instance;

        public void Dispose()
        {
        }
    }
}
