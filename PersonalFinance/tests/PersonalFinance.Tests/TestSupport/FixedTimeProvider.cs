namespace PersonalFinance.Tests.TestSupport;

/// <summary>
/// A clock that reads <see cref="Now"/> until a test moves it, so expected values hold on any day the test runs
/// (#350). Frozen by default; <see cref="Advance"/> or set <see cref="Now"/> to cross a cache or grace deadline.
/// </summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <summary>The instant every read returns.</summary>
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    /// <summary>Moves the clock forward by <paramref name="by"/>.</summary>
    public void Advance(TimeSpan by) => Now += by;
}
