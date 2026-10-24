using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.ApiService.Features.Research;
using PersonalFinance.ApiService.Features.Research.Adapters;
using PersonalFinance.ApiService.Infrastructure;
using PersonalFinance.Shared.DTOs.Research;
using PersonalFinance.Tests.TestSupport;

namespace PersonalFinance.Tests.Research;

/// <summary>
/// The press releases of a Company Profile, through the real SEC EDGAR client over a stub SEC that serves the made-up
/// samples in <c>CompanySamples/</c>. Sample Widgets Inc (SWI) has, within the 12 months before 24 October 2026, the
/// 8-Ks of 20 October 2026, 1 August 2026 and 26 to 28 November 2025 and the 8-K/A of 30 September 2026. Each filing
/// attaches EX-99.1 and EX-99.2, except the 8-K of 28 November 2025, which attaches no exhibit 99. Exhibit 99.1 of the
/// 1 August 2026 8-K has nothing readable in its first 64 KB, and exhibit 99.1 of the 8-K/A starts with EDGAR's SGML
/// document header, as some exhibits on sec.gov do.
/// </summary>
public class CompanyPressReleasesServiceTests
{
    private const string UserId = "account-1";
    private const string Archives = "https://www.sec.gov/Archives/edgar/data/1234567/";
    private const string NoExhibitAccession = "0001234567-25-000001";
    private const string UnreadableAccession = "0001234567-26-000037";
    private const string SgmlHeaderAccession = "0001234567-26-000039";
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A filing's folder on EDGAR: its accession number without dashes.</summary>
    private static string Folder(string accession) => accession.Replace("-", string.Empty, StringComparison.Ordinal);

    private sealed class StubSec : HttpMessageHandler
    {
        private static readonly string Samples =
            Path.Combine(RepositoryRoot.Find(), "PersonalFinance", "tests", "PersonalFinance.Tests", "Research",
                "CompanySamples");

        public ConcurrentDictionary<string, int> Requests { get; } = new();

        /// <summary>The filing whose index lists no exhibit 99, or null for every filing to list two.</summary>
        public string? NoExhibitFiling { get; set; } = NoExhibitAccession;

        /// <summary>True to answer 500 for the company's submissions and filings (SEC down).</summary>
        public bool CompanyDocumentsFail { get; set; }

        /// <summary>True to answer 500 for the filings' documents only.</summary>
        public bool ArchivesFail { get; set; }

        /// <summary>The bytes of exhibit documents the client read, by URL.</summary>
        public ConcurrentDictionary<string, long> BytesRead { get; } = new();

        public int CompanyRequests => Requests.Where(r =>
                r.Key.Contains("CIK", StringComparison.Ordinal) || r.Key.StartsWith(Archives, StringComparison.Ordinal))
            .Sum(r => r.Value);

        public IEnumerable<string> ArchiveRequests =>
            Requests.Keys.Where(k => k.StartsWith(Archives, StringComparison.Ordinal));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            Requests.AddOrUpdate(url, 1, (_, n) => n + 1);
            Assert.Contains("contact@example.com", request.Headers.UserAgent.ToString(), StringComparison.Ordinal);

            bool archive = url.StartsWith(Archives, StringComparison.Ordinal);
            string? file = archive ? ArchiveSample(url) : url switch
            {
                "https://www.sec.gov/files/company_tickers_exchange.json" => "company-tickers-sample.json",
                "https://www.sec.gov/files/company_tickers_mf.json" => "company-tickers-mf-sample.json",
                "https://data.sec.gov/submissions/CIK0001234567.json" => "submissions-sample-widgets.json",
                _ => null,
            };
            bool fails = (CompanyDocumentsFail && (archive || url.Contains("CIK", StringComparison.Ordinal)))
                         || (ArchivesFail && archive);
            if (fails)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            if (file is null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            byte[] bytes = File.ReadAllBytes(Path.Combine(Samples, file));
            var stream = new CountingStream(bytes, read => BytesRead.AddOrUpdate(url, read, (_, n) => n + read));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream),
            });
        }

        /// <summary>The sample a filing archive URL is answered with: its index headers or one of its exhibits.</summary>
        private string? ArchiveSample(string url)
        {
            string[] parts = url[Archives.Length..].Split('/');
            if (parts.Length != 2)
            {
                return null;
            }

            string name = parts[1];
            if (name.EndsWith("-index-headers.html", StringComparison.Ordinal))
            {
                string accession = name[..^"-index-headers.html".Length];
                return accession == NoExhibitFiling
                    ? "index-headers-no-exhibit.html"
                    : "index-headers-two-exhibits.html";
            }

            string filing = parts[0];
            return name switch
            {
                "swi-20261020xex99d1.htm" when filing == Folder(UnreadableAccession) => "exhibit-nothing-readable.htm",
                "swi-20261020xex99d1.htm" when filing == Folder(SgmlHeaderAccession) => "exhibit-sgml-header.htm",
                "swi-20261020xex99d1.htm" => "exhibit-headline-after-boilerplate.htm",
                "swi-20261020xex99d2.htm" => "exhibit-title-then-headline.htm",
                _ => null,
            };
        }
    }

    /// <summary>
    /// A response body that reports how many bytes were read from it. MemoryStream's async reads of an array-backed
    /// buffer go through <see cref="Read(byte[], int, int)"/>, so each byte is counted once.
    /// </summary>
    private sealed class CountingStream(byte[] bytes, Action<long> onRead) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = base.Read(buffer, offset, count);
            onRead(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            int read = base.Read(buffer);
            onRead(read);
            return read;
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
            new CompanyTickerIndexCache(new ResearchSettings(), timeProvider,
                NullLogger<CompanyTickerIndexCache>.Instance),
            new KeptWhileUnreachableCache<CompanyProfileDto>(timeProvider),
            new KeptWhileUnreachableCache<List<CompanyPressReleaseDto>>(timeProvider),
            rateLimiter ??
            new RateLimiterService(CompanyLookupRateLimit.MaxLookupsPerWindow, CompanyLookupRateLimit.Window),
            research ?? new ResearchSettings(),
            timeProvider,
            NullLogger<CompanyProfileService>.Instance);
    }

    private static async Task<List<CompanyPressReleaseDto>> ReleasesOf(CompanyProfileService service,
        string ticker = "SWI")
    {
        CompanyLookupOutcome<CompanyPressReleasesResultDto> outcome = await service.GetPressReleasesAsync(UserId, ticker);
        Assert.NotNull(outcome.Result);
        Assert.Equal(CompanyLookupKinds.Profile, outcome.Result.Kind);
        Assert.Equal(ticker, outcome.Result.Ticker);
        return outcome.Result.Releases;
    }

    private static string DocumentUrl(string accession, string file) =>
        $"{Archives}{accession.Replace("-", string.Empty, StringComparison.Ordinal)}/{file}";

    [Fact]
    public async Task Releases_ComeFromTheEightKsOfTheLastTwelveMonths_NewestFirst()
    {
        var sec = new StubSec();

        List<CompanyPressReleaseDto> releases = await ReleasesOf(CreateService(sec));

        Assert.Equal(
        [
            new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 20),
            new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), // The 8-K/A.
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 1),
            new DateOnly(2025, 11, 27), new DateOnly(2025, 11, 27),
            new DateOnly(2025, 11, 26), new DateOnly(2025, 11, 26),
        ], releases.Select(r => r.FiledOn));
        // Only 8-K and 8-K/A filings of the last 12 months are opened: never the 10-Qs, the Form 4, the S-8 or the
        // 8-Ks of September 2025.
        Assert.Equal(6, sec.ArchiveRequests.Count(u => u.EndsWith("-index-headers.html", StringComparison.Ordinal)));
        Assert.DoesNotContain(sec.ArchiveRequests, u => u.Contains("0001234567-25-000003", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Releases_ListEveryExhibit99OfAFiling_InTheFilingsOrder_WithTheirDocumentLinks()
    {
        List<CompanyPressReleaseDto> releases = await ReleasesOf(CreateService(new StubSec()));

        CompanyPressReleaseDto first = releases[0];
        Assert.Equal("Sample Widgets Reports Record Fiscal 2026 Results", first.Headline);
        Assert.True(first.HeadlineRead);
        Assert.Equal(["9.99"], first.Events);
        Assert.Equal(DocumentUrl("0001234567-26-000041", "swi-20261020xex99d1.htm"), first.Url);
        Assert.Equal("Sample Widgets Names Jane Doe Chief Financial Officer", releases[1].Headline);
        Assert.Equal(DocumentUrl("0001234567-26-000041", "swi-20261020xex99d2.htm"), releases[1].Url);
        // The exhibit-10 attachment of the 28 November 2025 8-K is not a press release.
        Assert.DoesNotContain(releases, r => r.FiledOn == new DateOnly(2025, 11, 28));
        Assert.DoesNotContain(releases, r => r.Url.Contains("ex10", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Headline_SkipsTheExhibitNumber_TheFileName_StylesAndScripts()
    {
        List<CompanyPressReleaseDto> releases = await ReleasesOf(CreateService(new StubSec()));

        // The sample's earlier lines are a title of "EX-99.1", "Exhibit 99.1", "Exhibit 99.1 . . . .", a style rule
        // and a script, each skipped; the headline is split across two font runs.
        Assert.Equal("Sample Widgets Reports Record Fiscal 2026 Results", releases[0].Headline);
        // A title of the file name, "Exhibit 99.2" and a bold "EX-99.2" are skipped for the paragraph after them.
        Assert.Equal("Sample Widgets Names Jane Doe Chief Financial Officer", releases[5].Headline);
    }

    [Fact]
    public async Task Headline_WrappedAcrossSourceLines_IsReadWhole()
    {
        List<CompanyPressReleaseDto> releases = await ReleasesOf(CreateService(new StubSec()));

        // Both samples wrap their headline paragraph onto a second line of the document's source, which a browser
        // shows as one line. "Sample Widgets Names Jane Doe" alone is long enough to pass for a headline.
        Assert.Equal("Sample Widgets Reports Record Fiscal 2026 Results", releases[0].Headline);
        Assert.Equal("Sample Widgets Names Jane Doe Chief Financial Officer", releases[1].Headline);
    }

    [Fact]
    public async Task Headline_SkipsTheSgmlDocumentHeader()
    {
        List<CompanyPressReleaseDto> releases = await ReleasesOf(CreateService(new StubSec()));

        // The header's TYPE, SEQUENCE, FILENAME and DESCRIPTION are each a line of their own, never run together
        // into one long enough to pass for a headline (as "EX-99.1 2 msft-ex99_1.htm EX-99.1" did for Microsoft).
        CompanyPressReleaseDto acquisition = releases[2];
        Assert.Equal(new DateOnly(2026, 9, 30), acquisition.FiledOn);
        Assert.Equal("Sample Widgets Completes the Acquisition of Example Gadgets", acquisition.Headline);
        Assert.True(acquisition.HeadlineRead);
    }

    [Fact]
    public async Task Headline_WhenNothingIsReadable_FallsBackToTheEventWords()
    {
        var sec = new StubSec();

        List<CompanyPressReleaseDto> releases = await ReleasesOf(CreateService(sec));

        CompanyPressReleaseDto slides = releases[4];
        Assert.Equal(new DateOnly(2026, 8, 1), slides.FiledOn);
        Assert.Equal("Results of operations · Financial statements and exhibits", slides.Headline);
        Assert.False(slides.HeadlineRead);
        Assert.Equal(["Results of operations", "Financial statements and exhibits"], slides.Events);
    }

    [Fact]
    public async Task Headline_IsReadFromTheFirst64KbOnly()
    {
        var sec = new StubSec();

        List<CompanyPressReleaseDto> releases = await ReleasesOf(CreateService(sec));

        // The sample's only headline lies past its first 64 KB.
        Assert.False(releases[4].HeadlineRead);
        long read = sec.BytesRead[DocumentUrl(UnreadableAccession, "swi-20261020xex99d1.htm")];
        Assert.InRange(read, 1, 64 * 1024);
    }

    [Fact]
    public async Task Releases_StopAtTen_AndStopReading()
    {
        var sec = new StubSec { NoExhibitFiling = null };

        List<CompanyPressReleaseDto> releases = await ReleasesOf(CreateService(sec));

        Assert.Equal(10, releases.Count);
        Assert.Equal(new DateOnly(2025, 11, 27), releases[^1].FiledOn);
        // The fifth filing gave the tenth release, so the sixth (26 November 2025) is never opened.
        Assert.DoesNotContain(sec.ArchiveRequests, u => u.Contains("000123456725000025", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Releases_Twice_ReadSecOnce()
    {
        var sec = new StubSec();
        CompanyProfileService service = CreateService(sec);

        await ReleasesOf(service);
        int afterFirst = sec.CompanyRequests;
        await ReleasesOf(service);

        Assert.Equal(afterFirst, sec.CompanyRequests);
    }

    [Fact]
    public async Task Releases_AfterADay_ReadSecAgain()
    {
        var sec = new StubSec();
        var time = new MovableTimeProvider(Now);
        CompanyProfileService service = CreateService(sec, time);

        await ReleasesOf(service);
        int afterFirst = sec.CompanyRequests;
        time.Now = Now + KeptWhileUnreachableCache<List<CompanyPressReleaseDto>>.FreshFor;
        await ReleasesOf(service);

        Assert.Equal(2 * afterFirst, sec.CompanyRequests);
    }

    [Fact]
    public async Task Releases_WhenSecFails_ServeTheKeptList()
    {
        var sec = new StubSec();
        var time = new MovableTimeProvider(Now);
        CompanyProfileService service = CreateService(sec, time);
        List<CompanyPressReleaseDto> first = await ReleasesOf(service);

        time.Now = Now + TimeSpan.FromDays(2);
        sec.ArchivesFail = true;
        List<CompanyPressReleaseDto> served = await ReleasesOf(service);

        Assert.Same(first, served);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Releases_WhenSecFails_WithNoKeptList_AreUnavailable(bool companyFails, bool archivesFail)
    {
        var sec = new StubSec { CompanyDocumentsFail = companyFails, ArchivesFail = archivesFail };

        CompanyLookupOutcome<CompanyPressReleasesResultDto> outcome = await CreateService(sec).GetPressReleasesAsync(UserId, "SWI");

        Assert.True(outcome.Unavailable);
        Assert.False(outcome.RateLimited);
    }

    [Fact]
    public async Task Releases_CountTowardTheLookupLimit()
    {
        CompanyProfileService service =
            CreateService(new StubSec(), rateLimiter: new RateLimiterService(2, TimeSpan.FromMinutes(1)));

        Assert.False((await service.LookUpAsync(UserId, "SWI")).RateLimited);
        Assert.False((await service.GetPressReleasesAsync(UserId, "SWI")).RateLimited);
        CompanyLookupOutcome<CompanyPressReleasesResultDto> refused = await service.GetPressReleasesAsync(UserId, "SWI");

        Assert.True(refused.RateLimited);
        Assert.Null(refused.Result);
    }

    [Theory]
    [InlineData("SWFD", CompanyLookupKinds.Fund)]
    [InlineData("ZZZZ", CompanyLookupKinds.NotFound)]
    public async Task Releases_OfAFundOrAnUnlistedTicker_AnswerAsTheLookupDoes(string ticker, string kind)
    {
        var sec = new StubSec();

        CompanyLookupOutcome<CompanyPressReleasesResultDto> outcome = await CreateService(sec).GetPressReleasesAsync(UserId, ticker);

        Assert.Equal(kind, outcome.Result?.Kind);
        Assert.Empty(outcome.Result!.Releases);
        Assert.Equal(0, sec.CompanyRequests);
    }

    [Fact]
    public async Task Releases_SwitchedOff_AreDisabled_WithoutReadingSecOrCountingALookup()
    {
        var sec = new StubSec();
        CompanyProfileService service = CreateService(sec,
            rateLimiter: new RateLimiterService(1, TimeSpan.FromMinutes(1)),
            research: new ResearchSettings { PressReleasesEnabled = false });

        CompanyLookupOutcome<CompanyPressReleasesResultDto> outcome = await service.GetPressReleasesAsync(UserId, "SWI");

        Assert.True(outcome.SwitchedOff);
        Assert.Null(outcome.Result);
        Assert.False(outcome.Unavailable);
        Assert.Empty(sec.Requests);
        Assert.False((await service.LookUpAsync(UserId, "SWI")).RateLimited);
    }
}
