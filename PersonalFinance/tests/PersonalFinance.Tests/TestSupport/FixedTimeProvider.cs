namespace PersonalFinance.Tests.TestSupport;

/// <summary>A clock stopped at <paramref name="now"/>, so a test's expected values hold on any day it runs (#350).</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
