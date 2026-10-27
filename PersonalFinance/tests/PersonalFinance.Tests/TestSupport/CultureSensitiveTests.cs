namespace PersonalFinance.Tests.TestSupport;

/// <summary>
/// The tests that set <see cref="System.Globalization.CultureInfo.DefaultThreadCurrentCulture"/> and
/// <see cref="System.Globalization.CultureInfo.DefaultThreadCurrentUICulture"/>. Those are process-wide, so while one of
/// these tests holds them at Invariant any other test formatting with the current culture would see "¤" for the
/// currency and month-first dates. Naming the collection alone only kept these tests from running at the same time as
/// each other; it now runs with nothing else in parallel (#350).
/// </summary>
[CollectionDefinition("Culture", DisableParallelization = true)]
public sealed class CultureSensitiveTests;
