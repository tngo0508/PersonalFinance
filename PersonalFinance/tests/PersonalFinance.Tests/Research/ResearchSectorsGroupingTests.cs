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
/// How the Sectors snapshot groups filers for the sunburst: SEC industry office, then SIC industry, then company,
/// with the filers outside the largest 500 summed into "Other" per industry. Built through the real SEC EDGAR client
/// over a stub HTTP handler that answers both the Public Float frames and the company submissions.
/// </summary>
public class ResearchSectorsGroupingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateContext(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static ResearchService CreateService(AppDbContext db, StubSecHandler handler,
        int submissionsPerRefresh = 5_000)
    {
        var settings = new SecEdgarSettings { UserAgent = "PersonalFinance tests contact@example.com", MaxRequestsPerSecond = 1000 };
        var timeProvider = new FixedTimeProvider(Now);
        var client = new SecEdgarClient(new HttpClient(handler), settings, new SecRequestGate(settings, timeProvider));
        var research = new ResearchSettings { SectorsFirstYear = 2025, SubmissionsPerRefresh = submissionsPerRefresh };
        // The World Bank is down here: these tests are about the Sectors dataset, which is built on its own.
        var worldClient = new WorldBankClient(new HttpClient(new DelegatingTestHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))));
        return new ResearchService(db, client, worldClient, research, timeProvider, NullLogger<ResearchService>.Instance);
    }

    private static async Task<SectorsSnapshotDto> RefreshAndReadAsync(ResearchService service)
    {
        ResearchRefreshSummary summary = await service.RefreshAsync();
        Assert.Contains("sectors", summary.Built);
        return (await service.GetSectorsAsync()).Snapshot!;
    }

    private static string OfficeOf(SectorsSnapshotDto snapshot, long cik)
    {
        SectorsCompanyDto company = snapshot.Companies.Single(c => c.Cik == cik);
        return snapshot.Offices[snapshot.Industries[company.Industry].Office];
    }

    [Fact]
    public async Task RefreshAsync_SumsTheFilersOutsideTheLargest500_IntoOtherPerSicIndustry()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        var handler = new StubSecHandler();
        // 500 banks worth 1,000,000 dollars and up, then four smaller filers: two banks and two software companies.
        List<Filer> filers = Enumerable.Range(1, 500)
            .Select(i => new Filer(i, $"BANK {i}", 1_000_000m + i))
            .ToList();
        filers.AddRange([new Filer(601, "SMALL BANK A", 300m), new Filer(602, "SMALL BANK B", 200m),
            new Filer(701, "SMALL SOFTWARE A", 70m), new Filer(702, "SMALL SOFTWARE B", 30m)]);
        handler.Frames["CY2025Q2I"] = filers;
        foreach (Filer filer in filers.Where(f => f.Cik <= 602))
        {
            handler.Submissions[filer.Cik] = new Submission(filer.Name, "6022", "STATE COMMERCIAL BANKS", "02 Finance");
        }

        handler.Submissions[701] = new Submission("SMALL SOFTWARE A", "7372", "SERVICES-PREPACKAGED SOFTWARE", "06 Technology");
        handler.Submissions[702] = new Submission("SMALL SOFTWARE B", "7372", "SERVICES-PREPACKAGED SOFTWARE", "06 Technology");

        SectorsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, handler));
        SectorsPeriodDto year = Assert.Single(snapshot.Values);

        Assert.Equal(500, year.Ranked.Count);
        Assert.Equal(
            [("Finance", "State Commercial Banks", 500m, 2), ("Technology", "Services-Prepackaged Software", 100m, 2)],
            year.Other.Select(o => (snapshot.Offices[snapshot.Industries[o.Industry].Office],
                snapshot.Industries[o.Industry].Name, o.PublicFloat, o.Count)));
        Assert.Equal(600m, year.RestTotal);
        Assert.Equal(4, year.RestCount);
        Assert.DoesNotContain(snapshot.Companies, c => c.Cik > 600);
    }

    [Fact]
    public async Task RefreshAsync_TakesTheOfficeFromOwnerOrg_ThenTheSicCode_ThenTheMajorGroup_ElseUnassigned()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        var handler = new StubSecHandler();
        handler.Frames["CY2025Q2I"] = Enumerable.Range(1, 7).Select(i => new Filer(i, $"FILER {i}", i * 10m)).ToList();
        handler.Submissions[1] = new Submission("CHIP ONE", "3674", "SEMICONDUCTORS & RELATED DEVICES", "06 Technology");
        handler.Submissions[2] = new Submission("CHIP TWO", "3674", "SEMICONDUCTORS & RELATED DEVICES", null);
        handler.Submissions[3] = new Submission("RADIO ONE", "3663", "RADIO & TV BROADCASTING & COMMUNICATIONS EQUIPMENT", "04 Manufacturing");
        handler.Submissions[4] = new Submission("AUDIO ONE", "3651", "HOUSEHOLD AUDIO & VIDEO EQUIPMENT", "04 Manufacturing");
        handler.Submissions[5] = new Submission("PARTS ONE", "3690", "MISC ELECTRICAL MACHINERY, EQUIPMENT & SUPPLIES", "");
        handler.Submissions[6] = new Submission("SHELL ONE", "9995", "NON-OPERATING ESTABLISHMENTS", null);
        // Filer 7 has no submissions (404), so nothing is known about it.

        SectorsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, handler));

        Assert.Equal("Technology", OfficeOf(snapshot, 1));
        Assert.Equal("Technology", OfficeOf(snapshot, 2)); // the most common office of SIC 3674
        Assert.Equal("Manufacturing", OfficeOf(snapshot, 5)); // no other 3690: the most common office of major group 36
        Assert.Equal("Unassigned", OfficeOf(snapshot, 6));
        Assert.Equal("Unassigned", OfficeOf(snapshot, 7));
        SectorsCompanyDto unknown = snapshot.Companies.Single(c => c.Cik == 7);
        Assert.Equal("Unclassified", snapshot.Industries[unknown.Industry].Name);
        Assert.Equal(["Manufacturing", "Technology", "Unassigned"], snapshot.Offices);
    }

    [Fact]
    public async Task RefreshAsync_TitleCasesNames_AndTakesTheTickerFromTheSubmissions()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        var handler = new StubSecHandler();
        handler.Frames["CY2025Q2I"] =
        [
            new Filer(1, "BANK OF THE OZARKS", 30m),
            new Filer(2, "Mixed Case Inc.", 20m),
            new Filer(3, "NO SUBMISSION CO", 10m),
            new Filer(4, "UNIVERSAL DISPLAY CORP \\PA\\", 5m),
            new Filer(5, "APPLE INC /CA/", 4m),
        ];
        handler.Submissions[1] = new Submission("BANK OF THE OZARKS", "6022", "STATE COMMERCIAL BANKS", "02 Finance", ["OZRK", "OZRKP"]);
        handler.Submissions[2] = new Submission("Mixed Case Inc.", "6022", "STATE COMMERCIAL BANKS", "02 Finance");

        SectorsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, handler));

        // SEC's state-of-incorporation suffix (/CA/, \PA\) is left off.
        Assert.Equal([("Bank of the Ozarks", "OZRK"), ("Mixed Case Inc.", null), ("No Submission Co", null),
                ("Universal Display Corp", null), ("Apple Inc", null)],
            snapshot.Companies.Select(c => (c.Name, c.Ticker)));
    }

    [Fact]
    public async Task RefreshAsync_RemembersFilersOutsideTheLargest500_SoTheNextRefreshDoesNotFetchThemAgain()
    {
        string database = $"Research_{Guid.NewGuid():N}";
        var handler = new StubSecHandler();
        handler.Frames["CY2025Q2I"] = Enumerable.Range(1, 501).Select(i => new Filer(i, $"FILER {i}", 1_000m + i)).ToList();
        foreach (int cik in Enumerable.Range(1, 501))
        {
            handler.Submissions[cik] = new Submission($"FILER {cik}", "6022", "STATE COMMERCIAL BANKS", "02 Finance");
        }

        await using (AppDbContext db = CreateContext(database))
        {
            await RefreshAndReadAsync(CreateService(db, handler));
        }

        await using AppDbContext later = CreateContext(database);
        SectorsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(later, handler));

        // The smallest filer (CIK 1) is outside the 500: read once. The ranked filers are re-read on every refresh.
        Assert.Equal(1, handler.SubmissionRequests.Count(c => c == 1));
        Assert.Equal(2, handler.SubmissionRequests.Count(c => c == 501));
        Assert.Equal("State Commercial Banks", snapshot.Industries[Assert.Single(snapshot.Values[0].Other).Industry].Name);
    }

    [Fact]
    public async Task RefreshAsync_ReadsNewFilersOutsideTheLargest500_LargestFirst_UpToTheConfiguredBudget()
    {
        await using AppDbContext db = CreateContext($"Research_{Guid.NewGuid():N}");
        var handler = new StubSecHandler();
        handler.Frames["CY2025Q2I"] = Enumerable.Range(1, 502).Select(i => new Filer(i, $"FILER {i}", 1_000m + i)).ToList();
        foreach (int cik in Enumerable.Range(1, 502))
        {
            handler.Submissions[cik] = new Submission($"FILER {cik}", "6022", "STATE COMMERCIAL BANKS", "02 Finance");
        }

        SectorsSnapshotDto snapshot = await RefreshAndReadAsync(CreateService(db, handler, submissionsPerRefresh: 1));

        // CIK 2 is the larger of the two filers outside the 500; CIK 1 waits for a later refresh, until then unassigned.
        Assert.Contains(2L, handler.SubmissionRequests);
        Assert.DoesNotContain(1L, handler.SubmissionRequests);
        Assert.Equal([("Finance", 1_002m), ("Unassigned", 1_001m)],
            snapshot.Values[0].Other.Select(o => (snapshot.Offices[snapshot.Industries[o.Industry].Office], o.PublicFloat)));
    }

    /// <summary>A filer's row in a <c>dei:EntityPublicFloat</c> frame, as of 30 June 2025.</summary>
    private sealed record Filer(long Cik, string Name, decimal Value);

    /// <summary>The fields of a filer's SEC submissions the Sectors snapshot reads.</summary>
    private sealed record Submission(string Name, string Sic, string SicDescription, string? OwnerOrg,
        string[]? Tickers = null);

    /// <summary>
    /// Answers SEC's frames API from canned frames keyed by <c>CY{year}Q{quarter}I</c>, and its submissions API from
    /// canned submissions keyed by CIK; anything else is a 404, as SEC answers.
    /// </summary>
    private sealed class StubSecHandler : HttpMessageHandler
    {
        private const string FramePrefix = "https://data.sec.gov/api/xbrl/frames/dei/EntityPublicFloat/USD/";
        private const string SubmissionsPrefix = "https://data.sec.gov/submissions/CIK";

        public Dictionary<string, List<Filer>> Frames { get; } = new();
        public Dictionary<long, Submission> Submissions { get; } = new();
        public List<long> SubmissionRequests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            object? body = null;
            if (url.StartsWith(FramePrefix, StringComparison.Ordinal))
            {
                string frame = url[FramePrefix.Length..].Replace(".json", string.Empty, StringComparison.Ordinal);
                if (Frames.TryGetValue(frame, out List<Filer>? filers))
                {
                    body = new
                    {
                        taxonomy = "dei",
                        tag = "EntityPublicFloat",
                        data = filers.Select(f => new { cik = f.Cik, entityName = f.Name, end = "2025-06-30", val = f.Value }),
                    };
                }
            }
            else if (url.StartsWith(SubmissionsPrefix, StringComparison.Ordinal))
            {
                long cik = long.Parse(url[SubmissionsPrefix.Length..].Replace(".json", string.Empty, StringComparison.Ordinal),
                    CultureInfo.InvariantCulture);
                SubmissionRequests.Add(cik);
                if (Submissions.TryGetValue(cik, out Submission? s))
                {
                    body = new
                    {
                        cik = cik.ToString(CultureInfo.InvariantCulture),
                        entityType = "operating",
                        sic = s.Sic,
                        sicDescription = s.SicDescription,
                        ownerOrg = s.OwnerOrg,
                        name = s.Name,
                        tickers = s.Tickers ?? [],
                        exchanges = (s.Tickers ?? []).Select(_ => "Nasdaq"),
                        filings = new { recent = new { accessionNumber = new[] { "0000000000-25-000001" } } },
                    };
                }
            }

            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
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
