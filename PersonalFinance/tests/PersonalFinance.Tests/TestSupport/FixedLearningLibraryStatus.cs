using PersonalFinance.Web.Services;

namespace PersonalFinance.Tests.TestSupport;

/// <summary>
/// An <see cref="ILearningLibraryStatus"/> with a fixed answer, for the hosts that render the layout: it decides whether
/// the nav shows "Learning".
/// </summary>
public sealed class FixedLearningLibraryStatus(bool hasEntries) : ILearningLibraryStatus
{
    public Task<bool> HasEntriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(hasEntries);
}
