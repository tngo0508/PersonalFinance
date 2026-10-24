using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
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
    private static ResearchService CreateService(AppDbContext db, RecordedSources sources)
    {
        var secSettings = new SecEdgarSettings { UserAgent = UserAgent, MaxRequestsPerSecond = 1000 };
        var timeProvider = new FixedTimeProvider(Now);
        var secClient = new SecEdgarClient(new HttpClient(sources), secSettings, new SecRequestGate(secSettings, timeProvider));
        var worldClient = new WorldBankClient(new HttpClient(sources));
        // The funds' first quarter puts the dataset's first month at January 2025.
        var research = new ResearchSettings { SectorsFirstYear = 2025, FundsFirstQuarter = new DateOnly(2026, 1, 1) };
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
