using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Features.Research.Domain;
using PersonalFinance.Data;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The Research Feature's macro snapshot, built through the real BLS, Fed and Treasury clients over recorded excerpts
/// of each source's file (in <c>MacroSamples/</c>) served by a stub HTTP handler, and stored in an InMemory database:
/// monthly CPI-U, effective federal funds rate and 3-month Treasury bill rate.
/// </summary>
public class ResearchMacroServiceTests
{
    private const string UserAgent = "PersonalFinance tests contact@example.org";
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly string Samples =
        Path.Combine(RepositoryRoot.Find(), "PersonalFinance", "tests", "PersonalFinance.Tests", "Research", "MacroSamples");

    private static AppDbContext CreateContext(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    /// <summary>The service with no funds configured, over the recorded sources; SEC and the World Bank answer 503.</summary>
    private static ResearchService CreateService(AppDbContext db, RecordedSources sources,
        IReadOnlyList<RecessionPeriod>? recessions = null)
    {
        var secSettings = new SecEdgarSettings { UserAgent = UserAgent, MaxRequestsPerSecond = 1000 };
        var timeProvider = new FixedTimeProvider(Now);
        SecEdgarClient secClient = ResearchServiceFactory.SecClient(sources, secSettings);
        var worldClient = new WorldBankClient(new HttpClient(sources));
        // The funds' first quarter puts the dataset's first month at January 2025.
        var research = new ResearchSettings
        {
            SectorsFirstYear = 2025, FundsFirstQuarter = new DateOnly(2026, 1, 1), Recessions = recessions ?? [],
        };
        return ResearchServiceFactory.Create(db, secClient, worldClient, research, timeProvider,
            fedClient: new FedRatesClient(new HttpClient(sources), secSettings),
            usAgencyClient: new UsAgencyClient(new HttpClient(sources), secSettings));
    }

    [Fact]
    public async Task UsAgencyClient_ReadsCpiUFromTheRecordedBlsFile_LeavingOutOtherSeriesTheAnnualAverageAndUnpublishedMonths()
    {
        using var reader = new StreamReader(Path.Combine(Samples, "bls-cu-data-excerpt.txt"));

        IReadOnlyList<(DateOnly Month, decimal Index)> prices = await UsAgencyClient.ParseConsumerPricesAsync(reader, 2025,
            CancellationToken.None);

        // July 2025 to September 2026, without October 2025 (not published: "-" with footnote X), the 2025 annual
        // average (M13), December 2024 (before the first year) or the seasonally adjusted and regional series.
        Assert.Equal(14, prices.Count);
        Assert.Equal((new DateOnly(2025, 7, 1), 323.048m), prices[0]);
        Assert.Equal((new DateOnly(2026, 9, 1), 336.940m), prices[^1]);
        Assert.DoesNotContain(prices, p => p.Month == new DateOnly(2025, 10, 1));
        Assert.DoesNotContain(prices, p => p.Index == 321.943m);
    }

    [Fact]
    public async Task FedRatesClient_ReadsBothMonthlySeriesFromTheRecordedH15File_InOnePass_SkippingDaysWithNoData()
    {
        await using FileStream file = File.OpenRead(Path.Combine(Samples, "h15-data-excerpt.xml"));

        IReadOnlyDictionary<string, IReadOnlyList<(DateOnly Month, decimal Rate)>> series = await FedRatesClient.ReadSeriesAsync(
            file, [FedRatesClient.FedFundsSeries, FedRatesClient.TreasuryBillSeries, "RIFSPFF_N.B"], CancellationToken.None);

        Assert.Equal(9, series[FedRatesClient.FedFundsSeries].Count);
        Assert.Equal((new DateOnly(2026, 1, 1), 3.64m), series[FedRatesClient.FedFundsSeries][0]);
        Assert.Equal((new DateOnly(2026, 9, 1), 3.75m), series[FedRatesClient.FedFundsSeries][^1]);
        Assert.Equal((new DateOnly(2026, 9, 1), 3.94m), series[FedRatesClient.TreasuryBillSeries][^1]);
        // The daily series' 12 October 2026 is "ND" with the value -9999: left out.
        Assert.All(series["RIFSPFF_N.B"], r => Assert.Equal(3.88m, r.Rate));
    }

    [Fact]
    public async Task UsAgencyClient_AveragesTheRecordedTreasuryDailyBillRatesPerMonth()
    {
        await using FileStream file = File.OpenRead(Path.Combine(Samples, "treasury-bill-rates-excerpt.xml"));

        IReadOnlyList<(DateOnly Month, decimal Rate)> rates = await UsAgencyClient.ParseTreasuryBillRatesAsync(file,
            CancellationToken.None);

        // 2 to 9 January 2026: 3.54, 3.54, 3.53, 3.52, 3.52, 3.52.
        Assert.Equal([(new DateOnly(2026, 1, 1), 3.53m)], rates);
    }

    [Fact]
    public async Task RefreshAsync_BuildsTheMacroSnapshotFromTheRecordedFiles_OneRowPerMonth()
    {
        await using AppDbContext db = CreateContext($"Macro_{Guid.NewGuid():N}");
        var sources = new RecordedSources();
        ResearchService service = CreateService(db, sources);

        ResearchRefreshSummary summary = await service.RefreshAsync();
        MacroSnapshotDto snapshot = (await service.GetMacroAsync()).Snapshot!;

        Assert.Contains(ResearchDatasets.Macro, summary.Built);
        Assert.Equal(new DateOnly(2025, 1, 1), snapshot.Months[0].Month);
        Assert.Equal(new DateOnly(2026, 9, 1), snapshot.Months[^1].Month);
        Assert.Equal(21, snapshot.Months.Count);
        Assert.Equal(new DateOnly(2026, 9, 30), snapshot.AsOf);
        Assert.Equal("H.15", snapshot.TreasuryBillSource);
        MacroMonthDto september = snapshot.Months[^1];
        Assert.Equal((336.940m, 3.75m, 3.94m), (september.ConsumerPrices, september.FedFunds, september.TreasuryBill));
        Assert.Null(snapshot.Months.Single(m => m.Month == new DateOnly(2025, 10, 1)).ConsumerPrices);
        Assert.Null(snapshot.Months[0].ConsumerPrices);
        Assert.All(sources.UserAgents, ua => Assert.Equal(UserAgent, ua));
    }

    [Fact]
    public async Task RefreshAsync_WhenTheFedCannotBeRead_TakesTheTreasuryBillFromTheTreasury_AndKeepsTheStoredFedFunds()
    {
        string database = $"Macro_{Guid.NewGuid():N}";
        var sources = new RecordedSources();
        await using (AppDbContext db = CreateContext(database))
        {
            await CreateService(db, sources).RefreshAsync();
        }

        sources.FedDown = true;
        await using AppDbContext later = CreateContext(database);
        ResearchService service = CreateService(later, sources);
        ResearchRefreshSummary summary = await service.RefreshAsync();
        MacroSnapshotDto snapshot = (await service.GetMacroAsync()).Snapshot!;

        Assert.Contains(ResearchDatasets.Macro, summary.Built);
        Assert.Equal("Treasury", snapshot.TreasuryBillSource);
        Assert.Equal(3.53m, snapshot.Months.Single(m => m.Month == new DateOnly(2026, 1, 1)).TreasuryBill);
        Assert.Equal(3.75m, snapshot.Months.Single(m => m.Month == new DateOnly(2026, 9, 1)).FedFunds);
    }

    [Fact]
    public async Task RefreshAsync_WhenBlsCannotBeRead_FailsTheMacroDataset_AndLeavesItNeverLoaded()
    {
        await using AppDbContext db = CreateContext($"Macro_{Guid.NewGuid():N}");
        ResearchService service = CreateService(db, new RecordedSources { BlsDown = true });

        ResearchRefreshSummary summary = await service.RefreshAsync();

        Assert.Contains(ResearchDatasets.Macro, summary.Failed);
        Assert.Null((await service.GetMacroAsync()).Snapshot);
    }

    [Fact]
    public async Task HikingCycles_OnTheRecordedH15FedFundsRate_FindThe2022CycleFromMarch2022ToAugust2023()
    {
        await using FileStream file = File.OpenRead(Path.Combine(Samples, "h15-fedfunds-2021-2024-excerpt.xml"));
        IReadOnlyList<(DateOnly Month, decimal Rate)> fedFunds = await FedRatesClient.ReadSeriesAsync(file,
            FedRatesClient.FedFundsSeries, CancellationToken.None);

        IReadOnlyList<(DateOnly Start, DateOnly End)> cycles = RateCycles.Hikes(fedFunds);

        // The Fed's first hike was on 16 March 2022 and its last on 26 July 2023; as monthly averages the rate rises
        // from March 2022 to August 2023, pausing in June and July 2023 (rises of 0.02 and 0.04). The 2024 cuts end it.
        Assert.Equal(48, fedFunds.Count);
        Assert.Equal([(new DateOnly(2022, 3, 1), new DateOnly(2023, 8, 1))], cycles);
    }

    [Fact]
    public void HikingCycles_LeaveOutSmallRuns_SplitAfterALongPause_AndEndAtAFall()
    {
        static List<(DateOnly Month, decimal Rate)> Rates(params decimal[] values) =>
            values.Select((v, i) => (new DateOnly(2020, 1, 1).AddMonths(i), v)).ToList();

        // One 0.25 rise only: too small to be a cycle.
        Assert.Empty(RateCycles.Hikes(Rates(1m, 1.25m, 1.25m, 1.25m)));
        // Two runs of 0.75 three flat months apart: two cycles, each ending at its last rise.
        Assert.Equal(
            [(new DateOnly(2020, 2, 1), new DateOnly(2020, 4, 1)), (new DateOnly(2020, 8, 1), new DateOnly(2020, 10, 1))],
            RateCycles.Hikes(Rates(1m, 1.25m, 1.5m, 1.75m, 1.75m, 1.75m, 1.75m, 2m, 2.25m, 2.5m)));
        // A cut right after the rises ends the cycle at once.
        Assert.Equal([(new DateOnly(2020, 2, 1), new DateOnly(2020, 3, 1))],
            RateCycles.Hikes(Rates(1m, 1.5m, 2m, 1.75m, 2m)));
    }

    [Fact]
    public async Task GetMacroAsync_AddsTheConfiguredRecessions_AsTheyAreConfiguredNow_LeavingOutUnreadableEntries()
    {
        await using AppDbContext db = CreateContext($"Macro_{Guid.NewGuid():N}");
        var sources = new RecordedSources();
        await CreateService(db, sources).RefreshAsync();

        // Configured after the refresh: the next read shows them, with no refresh and no code change.
        ResearchService later = CreateService(db, sources,
        [
            new RecessionPeriod { Start = "2020-02", End = "2020-04", Label = "2020 recession" },
            new RecessionPeriod { Start = "2008-01", End = "2009-06", Label = "2008 recession" },
            new RecessionPeriod { Start = "not a month", End = "2001-11", Label = "Broken" },
        ]);
        MacroSnapshotDto snapshot = (await later.GetMacroAsync()).Snapshot!;

        Assert.Equal(["2008 recession", "2020 recession"], snapshot.Recessions.Select(r => r.Label));
        Assert.Equal((new DateOnly(2020, 2, 1), new DateOnly(2020, 4, 1)), (snapshot.Recessions[1].Start, snapshot.Recessions[1].End));
    }

    [Fact]
    public void Build_LabelsEachDerivedHikingCycle()
    {
        var rates = new List<(DateOnly Month, decimal Rate)>
        {
            (new DateOnly(2022, 2, 1), 0.08m), (new DateOnly(2022, 3, 1), 0.33m), (new DateOnly(2022, 4, 1), 0.77m),
        };

        MacroSnapshotDto snapshot = MacroRefresher.Build(new DateOnly(2022, 1, 1), [(new DateOnly(2022, 1, 1), 280m)],
            rates, rates, "H.15");

        MacroPeriodDto cycle = Assert.Single(snapshot.HikingCycles);
        Assert.Equal(("Fed rate rises", new DateOnly(2022, 3, 1), new DateOnly(2022, 4, 1)), (cycle.Label, cycle.Start, cycle.End));
    }

    /// <summary>
    /// Serves the recorded BLS file, the H.15 bundle (the recorded data file zipped as the Fed serves it) and the
    /// recorded Treasury feed (for any year); anything else, and a source set down, answers 503.
    /// </summary>
    private sealed class RecordedSources : HttpMessageHandler
    {
        public bool FedDown { get; set; }
        public bool BlsDown { get; set; }
        public List<string> UserAgents { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            if (url == UsAgencyClient.CpiUrl && !BlsDown)
            {
                UserAgents.Add(request.Headers.UserAgent.ToString());
                return Ok(new StringContent(await File.ReadAllTextAsync(Path.Combine(Samples, "bls-cu-data-excerpt.txt"),
                    cancellationToken), Encoding.UTF8, "text/plain"));
            }

            if (url == FedRatesClient.BundleUrl && !FedDown)
            {
                UserAgents.Add(request.Headers.UserAgent.ToString());
                return Ok(new ByteArrayContent(await BundleAsync(cancellationToken)));
            }

            if (url.StartsWith("https://home.treasury.gov/", StringComparison.Ordinal))
            {
                UserAgents.Add(request.Headers.UserAgent.ToString());
                return Ok(new StringContent(await File.ReadAllTextAsync(Path.Combine(Samples, "treasury-bill-rates-excerpt.xml"),
                    cancellationToken), Encoding.UTF8, "application/xml"));
            }

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }

        private static HttpResponseMessage Ok(HttpContent content) => new(HttpStatusCode.OK) { Content = content };

        private static async Task<byte[]> BundleAsync(CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                await using Stream entry = zip.CreateEntry("H15_data.xml").Open();
                await using FileStream file = File.OpenRead(Path.Combine(Samples, "h15-data-excerpt.xml"));
                await file.CopyToAsync(entry, cancellationToken);
            }

            return buffer.ToArray();
        }
    }
}
