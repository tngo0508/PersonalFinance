using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Research;

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

    private static ResearchService CreateService(AppDbContext db, StubSecHandler handler, int firstYear = 2024)
    {
        var settings = new SecEdgarSettings { UserAgent = "PersonalFinance tests contact@example.com", MaxRequestsPerSecond = 1000 };
        var timeProvider = new FixedTimeProvider(Now);
        var client = new SecEdgarClient(new HttpClient(handler), settings, new SecRequestGate(settings, timeProvider));
        return new ResearchService(db, client, new ResearchSettings { SectorsFirstYear = firstYear }, timeProvider,
            NullLogger<ResearchService>.Instance);
    }

    [Fact]
    public async Task GetSectorsAsync_OnAnEmptyTable_ReturnsNeverLoaded()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");

        ResearchSnapshotResult<SectorsSnapshotDto> result = await CreateService(db, new StubSecHandler()).GetSectorsAsync();

        Assert.True(result.NeverLoaded);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task RefreshAsync_TakesEachFilersLatestQuarterlyFrame_SoAFilerWhoseFiscalQ2IsNotCalendarQ2IsIncluded()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        var handler = new StubSecHandler();
        // Apple-like filer: September fiscal year, so its fiscal Q2 ends in March (calendar Q1).
        handler.Frames["CY2025Q1I"] = [new Filer(320193, "APPLE INC", "2025-03-28", 2_600_000_000_000m)];
        handler.Frames["CY2025Q2I"] =
        [
            new Filer(789019, "MICROSOFT CORP", "2025-06-30", 3_000_000_000_000m),
            new Filer(1, "TWO FRAMES CO", "2025-06-30", 10_000_000m),
        ];
        // A later value for the same filer in the same year replaces the earlier one.
        handler.Frames["CY2025Q4I"] = [new Filer(1, "TWO FRAMES CO", "2025-12-31", 20_000_000m)];

        ResearchService service = CreateService(db, handler, firstYear: 2025);
        ResearchRefreshSummary summary = await service.RefreshAsync();
        SectorsSnapshotDto snapshot = (await service.GetSectorsAsync()).Snapshot!;

        Assert.Equal(["sectors"], summary.Built);
        Assert.Empty(summary.Failed);
        Assert.Equal(["2025"], snapshot.Periods);
        SectorsPeriodDto year = Assert.Single(snapshot.Values);
        Assert.Equal(
            [("MICROSOFT CORP", 3_000_000_000_000m), ("APPLE INC", 2_600_000_000_000m), ("TWO FRAMES CO", 20_000_000m)],
            year.Ranked.Select(r => (snapshot.Companies[r.Company].Name, r.PublicFloat)));
        Assert.Equal(0m, year.RestTotal);
        Assert.Equal(0, year.RestCount);
        Assert.Equal(new DateOnly(2025, 12, 31), snapshot.AsOf);
    }

    [Fact]
    public async Task RefreshAsync_KeepsExactly500Filers_AndSumsTheRest()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        var handler = new StubSecHandler();
        // 503 filers worth 1,000 to 503,000 dollars: the three smallest (1,000 + 2,000 + 3,000) are the rest.
        handler.Frames["CY2025Q2I"] = Enumerable.Range(1, 503)
            .Select(i => new Filer(i, $"FILER {i}", "2025-06-30", i * 1_000m))
            .ToList();

        ResearchService service = CreateService(db, handler, firstYear: 2025);
        await service.RefreshAsync();
        SectorsPeriodDto year = Assert.Single((await service.GetSectorsAsync()).Snapshot!.Values);

        Assert.Equal(500, year.Ranked.Count);
        Assert.Equal(503_000m, year.Ranked[0].PublicFloat);
        Assert.Equal(4_000m, year.Ranked[^1].PublicFloat);
        Assert.Equal(6_000m, year.RestTotal);
        Assert.Equal(3, year.RestCount);
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
        Assert.Equal("ALPHA RENAMED", Assert.Single(snapshot.Companies).Name);
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

        Assert.Equal(["TEN", "TWENTY", "THIRTY"],
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

        Assert.Equal(["sectors"], summary.Failed);
        Assert.Empty(summary.Built);
        Assert.False(result.NeverLoaded);
        Assert.Equal(new DateOnly(2025, 6, 30), result.Snapshot!.AsOf);
        Assert.Equal(10m, Assert.Single(result.Snapshot.Values[0].Ranked).PublicFloat);
    }

    [Fact]
    public async Task RefreshAsync_WhenSecHasNoData_StaysNeverLoaded()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");

        ResearchService service = CreateService(db, new StubSecHandler(), firstYear: 2025);
        ResearchRefreshSummary summary = await service.RefreshAsync();

        Assert.Equal(["sectors"], summary.Failed);
        Assert.True((await service.GetSectorsAsync()).NeverLoaded);
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
    public void SnapshotPayloadQuery_TranslatesToAzureSql()
    {
        using AzureSqlAppDbContext db = AzureSqlTranslation.CreateContext();

        string sql = AzureSqlTranslation.AssertTranslates(ResearchService.SnapshotPayloadQuery(db, "sectors"));

        Assert.Contains("ResearchSnapshots", sql);
    }

    /// <summary>A filer's row in a <c>dei:EntityPublicFloat</c> frame.</summary>
    private sealed record Filer(long Cik, string Name, string End, decimal Value);

    /// <summary>
    /// Answers the SEC frames API from canned frames keyed by <c>CY{year}Q{quarter}I</c>; a frame with no entry is a
    /// 404, as SEC answers for a frame that does not exist.
    /// </summary>
    private sealed class StubSecHandler : HttpMessageHandler
    {
        private const string FramePrefix = "https://data.sec.gov/api/xbrl/frames/dei/EntityPublicFloat/USD/";

        public Dictionary<string, List<Filer>> Frames { get; } = new();
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
            string frame = url.StartsWith(FramePrefix, StringComparison.Ordinal)
                ? url[FramePrefix.Length..].Replace(".json", string.Empty, StringComparison.Ordinal)
                : string.Empty;
            if (!Frames.TryGetValue(frame, out List<Filer>? filers))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var body = new
            {
                taxonomy = "dei",
                tag = "EntityPublicFloat",
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
