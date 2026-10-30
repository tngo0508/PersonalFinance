using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Web.Services;
using Refit;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// Every page's layout asks <see cref="LearningLibraryStatus"/>, so a slow or cold ApiService must hide the Learning
/// entry rather than fail the page. Only the browser's own cancellation propagates.
/// </summary>
public class LearningLibraryStatusTests
{
    [Fact]
    public async Task HasEntries_WhenApiServiceTimesOut_ReturnsFalse()
    {
        // ApiService still scaling from zero: the HttpClient.Timeout elapses before it answers.
        LearningLibraryStatus status = BuildStatus(TimeSpan.FromMilliseconds(50));

        bool hasEntries = await status.HasEntriesAsync();

        Assert.False(hasEntries);
    }

    [Fact]
    public async Task HasEntries_WhenTheRequestIsAborted_Throws()
    {
        LearningLibraryStatus status = BuildStatus(TimeSpan.FromSeconds(30));
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => status.HasEntriesAsync(aborted.Token));
    }

    private static LearningLibraryStatus BuildStatus(TimeSpan clientTimeout)
    {
        var httpClient = new HttpClient(new NeverAnswersHandler())
        {
            BaseAddress = new Uri("http://apiservice.test"),
            Timeout = clientTimeout,
        };
        ILearningApi learningApi = RestService.For<ILearningApi>(httpClient);
        return new LearningLibraryStatus(learningApi, new MemoryCache(new MemoryCacheOptions()),
            NullLogger<LearningLibraryStatus>.Instance);
    }

    private sealed class NeverAnswersHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }
}
